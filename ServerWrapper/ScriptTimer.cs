using CitizenMP.Server;
using System;
using System.Linq;

namespace ServerWrapper
{
    public delegate void ScriptTimerHandler(ScriptTimer timer);

    public class ScriptTimer : MarshalByRefObject, IDisposable
    {
        internal readonly object ScriptEnvironmentScriptTimer = null;

        private Delegate Function { get { return ScriptEnvironmentScriptTimer != null && Wrapper.ScriptEnvironmentScriptTimerFunction != null ? (Delegate)Wrapper.ScriptEnvironmentScriptTimerFunction.GetValue(ScriptEnvironmentScriptTimer) : null; } set { if (ScriptEnvironmentScriptTimer != null && Wrapper.ScriptEnvironmentScriptTimerFunction != null) Wrapper.ScriptEnvironmentScriptTimerFunction.SetValue(ScriptEnvironmentScriptTimer, value); } }
        private long TickFrom { get { return ScriptEnvironmentScriptTimer != null && Wrapper.ScriptEnvironmentScriptTimerTickFrom != null ? (long)Wrapper.ScriptEnvironmentScriptTimerTickFrom.GetValue(ScriptEnvironmentScriptTimer) : 0; } set { if (ScriptEnvironmentScriptTimer != null && Wrapper.ScriptEnvironmentScriptTimerTickFrom != null) Wrapper.ScriptEnvironmentScriptTimerTickFrom.SetValue(ScriptEnvironmentScriptTimer, value); } }

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

                lock (Wrapper.ScriptEnvironmentScriptTimerList) if (value && !Wrapper.ScriptEnvironmentScriptTimerList.Contains(this)) Start();
            }
        }

        public string ID { get; private set; }

        internal ScriptTimer(ServerScript caller, int interval, ScriptTimerHandler callback, bool loop = false)
        {
            if (Wrapper.ScriptEnvironmentScriptTimer == null || Wrapper.ScriptEnvironmentScriptTimerFunction == null || Wrapper.ScriptEnvironmentScriptTimerTickFrom == null || Wrapper.ScriptEnvironmentScriptTimerList == null) return;

            this.caller = caller;

            ID = Guid.NewGuid().ToString();

            lock (Wrapper.ScriptTimers) while (Wrapper.ScriptTimers.Any(st => st.ID == ID)) ID = Guid.NewGuid().ToString(); // Not taking any chances.

            ScriptEnvironmentScriptTimer = AppDomain.CurrentDomain.CreateInstanceAndUnwrap(Wrapper.ScriptEnvironmentScriptTimer.Assembly.FullName, Wrapper.ScriptEnvironmentScriptTimer.FullName);

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

            lock (Wrapper.ScriptTimers) if (!Wrapper.ScriptTimers.Contains(this)) Wrapper.ScriptTimers.Add(this);

            lock (Wrapper.ScriptEnvironmentScriptTimerList) Wrapper.ScriptEnvironmentScriptTimerList.Add(ScriptEnvironmentScriptTimer);
        }

        public void Cancel()
        {
            Loop = false;

            lock (Wrapper.ScriptEnvironmentScriptTimerList) while (Wrapper.ScriptEnvironmentScriptTimerList.Contains(ScriptEnvironmentScriptTimer)) Wrapper.ScriptEnvironmentScriptTimerList.Remove(ScriptEnvironmentScriptTimer);

            lock (Wrapper.ScriptTimers) Wrapper.ScriptTimers.RemoveAll(st => st == this || st.ID == ID);
        }

        public void Dispose()
        {
            Cancel();

            caller.RemoveScriptTimerHandler(this);
        }
    }
}