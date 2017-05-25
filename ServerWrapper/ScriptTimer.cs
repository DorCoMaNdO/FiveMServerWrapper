using CitizenMP.Server;
using System;
using System.Linq;
using System.Runtime.Remoting.Lifetime;

namespace ServerWrapper
{
    public delegate void ScriptTimerHandler(ScriptTimer timer);

    public class ScriptTimer : MarshalByRefObject, IDisposable
    {
        internal readonly object SEScriptTimer = null;

        private Delegate Function { get { return SEScriptTimer != null && Wrapper.SEScriptTimerFunction != null ? (Delegate)Wrapper.SEScriptTimerFunction.GetValue(SEScriptTimer) : null; } set { if (SEScriptTimer != null && Wrapper.SEScriptTimerFunction != null) Wrapper.SEScriptTimerFunction.SetValue(SEScriptTimer, value); } }
        private long TickFrom { get { return SEScriptTimer != null && Wrapper.SEScriptTimerTickFrom != null ? (long)Wrapper.SEScriptTimerTickFrom.GetValue(SEScriptTimer) : 0; } set { if (SEScriptTimer != null && Wrapper.SEScriptTimerTickFrom != null) Wrapper.SEScriptTimerTickFrom.SetValue(SEScriptTimer, value); } }

        internal ScriptTimerHandler Handler = null;

        internal ServerScript caller = null;

        private int interval = 100;
        public int Interval { get { return interval; } set { if (value >= 0) interval = value; } }

        private bool loop = false;
        public bool Loop
        {
            get
            {
                return loop;
            }
            set
            {
                loop = value;

                lock (Wrapper.SEScriptTimerList) if (value && !Wrapper.SEScriptTimerList.Contains(this)) Start();
            }
        }

        public string ID { get; private set; }

        internal ScriptTimer(ServerScript caller, int interval, ScriptTimerHandler callback, bool loop = false)
        {
            if (Wrapper.SEScriptTimer == null || Wrapper.SEScriptTimerFunction == null || Wrapper.SEScriptTimerTickFrom == null || Wrapper.SEScriptTimerList == null) return;

            this.caller = caller;

            ID = Guid.NewGuid().ToString();

            lock (Wrapper.ScriptTimers)
            {
                while (Wrapper.ScriptTimers.Any(st => st.ID == ID)) ID = Guid.NewGuid().ToString(); // Not taking any chances.

                Wrapper.ScriptTimers.Add(this);
            }

            SEScriptTimer = AppDomain.CurrentDomain.CreateInstanceAndUnwrap(Wrapper.SEScriptTimer.Assembly.FullName, Wrapper.SEScriptTimer.FullName);

            Interval = interval;

            this.loop = loop;

            Handler = callback;

            Function = new Action(() =>
            {
                if (Handler != null) Handler(this);

                if (Loop)
                {
                    Start();
                }
                else
                {
                    Cancel();
                }
            });
        }

        public void Start()
        {
            TickFrom = Time.CurrentTime + Interval;

            //lock (Wrapper.ScriptTimers) if (!Wrapper.ScriptTimers.Contains(this)) Wrapper.ScriptTimers.Add(this);

            lock (Wrapper.SEScriptTimerList) Wrapper.SEScriptTimerList.Add(SEScriptTimer);
        }

        public void Cancel()
        {
            Loop = false;

            lock (Wrapper.SEScriptTimerList) while (Wrapper.SEScriptTimerList.Contains(SEScriptTimer)) Wrapper.SEScriptTimerList.Remove(SEScriptTimer);

            //lock (Wrapper.ScriptTimers) Wrapper.ScriptTimers.RemoveAll(st => st == this || st.ID == ID);
        }

        public void Dispose()
        {
            Cancel();

            lock (Wrapper.ScriptTimers) Wrapper.ScriptTimers.RemoveAll(st => st == this || st.ID == ID);

            //RemotingServices.Disconnect(this);

            ((ILease)GetLifetimeService()).Unregister(Wrapper.instance);

            caller.RemoveScriptTimerHandler(this);
        }
    }
}