using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Lifetime;

namespace ServerWrapper
{
    public abstract class ServerScript : MarshalByRefObject, IServerScript
    {
        public string Name { get; private set; }
        public string TypeName { get { return this.GetType().FullName; } }
        private List<string> dependencies = new List<string>();
        public ReadOnlyCollection<string> Dependencies { get; private set; }

        public int EventSource { get { return GetEventSource(); } }

        public ReadOnlyDictionary<string, Player> Players { get { return w.Players; } }
        public ReadOnlyDictionary<ushort, Player> PlayersByNetId { get { return w.PlayersByNetId; } }

        internal ScriptTimer timer { get; set; }

        private Dictionary<ScriptTimer, ScriptTimerHandler> TimerHandlers = new Dictionary<ScriptTimer, ScriptTimerHandler>();
        private Dictionary<string, List<Delegate>> EventHandlers = new Dictionary<string, List<Delegate>>();

        private static Dictionary<string, Delegate> DelegateReferences = new Dictionary<string, Delegate>();

        public int Interval { get { return timer != null ? timer.Interval : 100; } set { if (timer != null) timer.Interval = value; } }

        public bool TickTimer
        {
            get
            {
                return timer != null ? timer.Loop : true;
            }
            set
            {
                if (timer != null)
                {
                    timer.Loop = value;

                    if (!value) timer.Cancel();
                }
            }
        }

        private Wrapper w = null;

        private object[] PrintPrefix;

        public ServerScript(string name) : base()
        {
            Name = name;

            PrintPrefix = new object[] { "ServerWrapper script \"" + Name + "\":" };

            Dependencies = new ReadOnlyCollection<string>(dependencies);
        }

        internal void CreateProxy(Wrapper wrapper, Queue<IServerScript> scripts)
        {
            w = wrapper;

            SetTimeout(0, (t) =>
            {
                w.Print("Proxy created for script \"" + Name + "\"");
                w.Print("");

                t.Loop = false; // First ScriptTimer with Print and a call to Loop (usually) causes a small hiccup of 32~150ms for whatever reason, better have it done ahead of time.

                w.ETPhoneHome(this, scripts);

                t.Dispose();
            });
        }

        public abstract void Load();

        public abstract void Unload();

        public abstract void Tick();

        public void AddDependency(Type type)
        {
            if (type.IsSubclassOf(typeof(ServerScript)) && !dependencies.Contains(type.FullName)) dependencies.Add(type.FullName);
        }

        public void Print(params object[] args)
        {
            InternalPrint(args);
        }

        private void InternalPrint(object[] args,
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (w != null) w.Print(this, "Print (" + Name + ")", "ServerWrapper\\ServerScript.cs", sourceLineNumber, PrintType.Info, args);
        }

        public ushort[] GetPlayers()
        {
            return w != null ? w.GetPlayers() : null;
        }

        public string GetPlayerName(int ID)
        {
            return w != null ? w.GetPlayerName(ID) : null;
        }

        public IEnumerable<string> GetPlayerIdentifiers(int ID)
        {
            return w != null ? w.GetPlayerIdentifiers(ID) : null;
        }

        public int GetPlayerPing(int ID)
        {
            return w != null ? w.GetPlayerPing(ID) : -1;
        }

        public string GetPlayerEP(int ID)
        {
            return w != null ? w.GetPlayerEP(ID) : null;
        }

        public double GetPlayerLastMsg(int ID)
        {
            return w != null ? w.GetPlayerLastMsg(ID) : 99999999;
        }

        public int GetHostID()
        {
            return w != null ? w.GetHostID() : -1;
        }

        public void DropPlayer(int ID, string reason)
        {
            if (w != null) w.DropPlayer(ID, reason);
        }

        public void TempBanPlayer(int ID, string reason)
        {
            if (w != null) w.TempBanPlayer(ID, reason);
        }

        public Player GetPlayerFromID(int ID)
        {
            return w != null ? w.GetPlayerFromID(ID) : /*default(Player)*/ null;
        }

        public void TriggerClientEvent(string eventname, int netID, params object[] args)
        {
            object[] cargs = ConvertArgsFromLocal(args);
            if (w != null) w.TriggerClientEvent(eventname, netID, cargs);

            lock (DelegateReferences) foreach (object arg in cargs) if (arg.GetType().IsSubclassOf(typeof(String))) if (DelegateReferences.ContainsKey((string)arg)) DelegateReferences.Remove((string)arg);
        }

        public void RegisterServerEvent(string eventname)
        {
            if (w != null) w.RegisterServerEvent(eventname);
        }

        public bool TriggerEvent(string eventname, params object[] args)
        {
            bool notcanceled = false;
            object[] cargs = ConvertArgsFromLocal(args);
            if (w != null) notcanceled = w.TriggerEvent(eventname, cargs);

            lock (DelegateReferences) foreach (object arg in cargs) if (arg.GetType().IsSubclassOf(typeof(String))) if (DelegateReferences.ContainsKey((string)arg)) DelegateReferences.Remove((string)arg);

            return notcanceled;
        }

        public void CancelEvent()
        {
            if (w != null) w.CancelEvent();
        }

        public bool WasEventCanceled()
        {
            return w != null ? w.WasEventCanceled() : false;
        }

        internal void RemoveScriptTimerHandler(ScriptTimer timer)
        {
            if (TimerHandlers.ContainsKey(timer)) TimerHandlers.Remove(timer);
        }

        internal bool CallScriptTimerHandler(ScriptTimer timer)
        {
            if (TimerHandlers.ContainsKey(timer))
            {
                TimerHandlers[timer](timer);

                return true;
            }

            return false;
        }

        public string SetTimeout(int delay, ScriptTimerHandler callback, bool loop = false)
        {
            if (w != null)
            {
                ScriptTimer timer = w.CreateTimer(this, delay, loop);

                if (timer == null) return null;

                ((ILease)timer.GetLifetimeService()).Register(w);

                if (!TimerHandlers.ContainsKey(timer)) TimerHandlers.Add(timer, callback);
                //TimerHandlers[timer] = callback;

                timer.Start();

                return timer.ID;
            }

            return null;
        }

        private bool HasTimeoutFinished(string id)
        {
            return w != null ? w.HasTimeoutFinished(this, id) : true;
        }

        public void CancelTimeout(string id)
        {
            if (w != null) w.CancelTimeout(this, id);
        }

        internal void TriggerLocalEvent(string eventname, params object[] args)
        {
            if (!EventHandlers.ContainsKey(eventname)) return;

            args = ConvertArgsToLocal(args);

            foreach (Delegate handler in EventHandlers[eventname].ToArray())
            {
                try
                {
                    if (handler.Method.GetParameters().Length != args.Length) continue;

                    handler.DynamicInvoke(args);
                }
                catch (Exception e)
                {
                    w.Print(PrintType.Error, "Error executing event handler for event " + eventname + " in script " + Name + ": \n");
                    w.PrintException(e);

                    //EventHandlers[eventname].Clear();

                    break;
                }
            }
        }

        internal object[] ConvertArgsToLocal(params object[] args)
        {
            List<object> Converted = new List<object>();

            foreach (object arg in args)
            {
                Type type = arg.GetType();

                if (type == typeof(String))
                {
                    string s = (string)arg;

                    lock (DelegateReferences)
                    {
                        if (DelegateReferences.ContainsKey(s))
                        {
                            Delegate d = DelegateReferences[s];

                            //DelegateReferences.Remove(s);

                            Converted.Add(d);

                            continue;
                        }
                    }
                }

                Converted.Add(arg);
            }

            return Converted.ToArray();
        }

        internal object[] ConvertArgsFromLocal(params object[] args)
        {
            List<object> Converted = new List<object>();

            foreach (object arg in args)
            {
                Type type = arg.GetType();

                if (type.IsSubclassOf(typeof(Delegate)))
                {
                    Delegate d = (Delegate)arg;

                    lock (DelegateReferences)
                    {
                        string dref = type.ToString() + "|" + Guid.NewGuid().ToString() + "|" + d.GetHashCode();
                        while (DelegateReferences.ContainsKey(dref)) dref = type.ToString() + "|" + Guid.NewGuid().ToString() + "|" + d.GetHashCode();

                        DelegateReferences.Add(dref, d);

                        Converted.Add(dref);
                    }

                    continue;
                }

                Converted.Add(arg);
            }

            return Converted.ToArray();
        }

        public void AddEventHandler(string eventname, Delegate eventhandler)
        {
            if (!EventHandlers.ContainsKey(eventname)) EventHandlers.Add(eventname, new List<Delegate>());
            if (!EventHandlers[eventname].Contains(eventhandler)) EventHandlers[eventname].Add(eventhandler);

            if (w != null) w.AddEventHandler(this, eventname, eventhandler.Method.GetParameters().Length);
        }

        public void RemoveEventHandler(string eventname, Delegate eventhandler)
        {
            if (EventHandlers.ContainsKey(eventname) && EventHandlers[eventname].Contains(eventhandler)) EventHandlers[eventname].RemoveAll(h => h == eventhandler);
        }

        public void RemoveAllEventHandlers(string eventname)
        {
            if (EventHandlers.ContainsKey(eventname)) EventHandlers[eventname].Clear();
        }

        public int GetInstanceID()
        {
            return w != null ? w.GetInstanceID() : -1;
        }

        public int GetEventSource()
        {
            return w != null ? w.GetEventSource() : -1;
        }

        public bool StopResource(string resourceName)
        {
            return w != null ? w.StopResource(resourceName) : false;
        }

        public bool StartResource(string resourceName)
        {
            return w != null ? w.StartResource(resourceName) : false;
        }

        public void SetGameType(string gameType)
        {
            if (w != null) w.SetGameType(gameType);
        }

        public void SetMapName(string mapName)
        {
            if (w != null) w.SetMapName(mapName);
        }
    }
}