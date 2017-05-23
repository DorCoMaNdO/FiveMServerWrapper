using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ServerWrapper
{
    public abstract class ServerScript : MarshalByRefObject, IServerScript
    {
        public string Name { get; private set; }
        public string TypeName { get { return this.GetType().FullName; } }
        private List<string> dependencies = new List<string>();
        public ReadOnlyCollection<string> Dependencies { get; private set; }

        public ReadOnlyDictionary<string, Player> Players { get { return w.Players; } }
        public ReadOnlyDictionary<ushort, Player> PlayersByNetId { get { return w.PlayersByNetId; } }

        internal ScriptTimer timer { get; set; }

        private Dictionary<ScriptTimer, ScriptTimerHandler> TimerHandlers = new Dictionary<ScriptTimer, ScriptTimerHandler>();
        private Dictionary<string, List<Delegate>> EventHandlers = new Dictionary<string, List<Delegate>>();

        private static Dictionary<Delegate, string> DelegateReferences = new Dictionary<Delegate, string>();

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

            SetTimeout(0, (t) => { w.RconPrint("ServerWrapper: Proxy created for script \"" + Name + "\""); w.RconPrint(""); t.Loop = false; w.ETPhoneHome(this, scripts); }); // First ScriptTimer with Print and a call to Loop (usually) causes a small hiccup of 32~150ms for whatever reason, better have it done ahead of time.
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
            if (w != null) w.Print(PrintPrefix.Concat(args).ToArray());
        }

        /*public void RconPrint(string str)
        {
            if (w != null) w.RconPrint("ServerWrapper script \"" + Name + "\": " + str);
        }*/

        public void RconPrint(params object[] args)
        {
            if (w != null) w.RconPrint(PrintPrefix.Concat(args).ToArray());
        }

        public ushort[] GetPlayers()
        {
            if (w != null) return w.GetPlayers();

            return null;
        }

        public string GetPlayerName(int ID)
        {
            if (w != null) return w.GetPlayerName(ID);

            return null;
        }

        public IEnumerable<string> GetPlayerIdentifiers(int ID)
        {
            if (w != null) return w.GetPlayerIdentifiers(ID);

            return null;
        }

        public int GetPlayerPing(int ID)
        {
            if (w != null) return w.GetPlayerPing(ID);

            return -1;
        }

        public string GetPlayerEP(int ID)
        {
            if (w != null) return w.GetPlayerEP(ID);

            return null;
        }

        public double GetPlayerLastMsg(int ID)
        {
            if (w != null) return w.GetPlayerLastMsg(ID);

            return 99999999;
        }

        public int GetHostID()
        {
            if (w != null) return w.GetHostID();

            return -1;
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
            if (w != null) return w.GetPlayerFromID(ID);

            //return default(Player);
            return null;
        }

        public void TriggerClientEvent(string eventname, int netID, params object[] args)
        {
            if (w != null) w.TriggerClientEvent(eventname, netID, ConvertArgsFromLocal(args));

            lock (DelegateReferences) foreach (object arg in args) if (arg.GetType().IsSubclassOf(typeof(Delegate))) if (DelegateReferences.ContainsKey((Delegate)arg)) DelegateReferences.Remove((Delegate)arg);
        }

        public void RegisterServerEvent(string eventname)
        {
            if (w != null) w.RegisterServerEvent(eventname);
        }

        public bool TriggerEvent(string eventname, params object[] args)
        {
            if (w != null) return w.TriggerEvent(eventname, ConvertArgsFromLocal(args));

            lock (DelegateReferences) foreach (object arg in args) if (arg.GetType().IsSubclassOf(typeof(Delegate))) if (DelegateReferences.ContainsKey((Delegate)arg)) DelegateReferences.Remove((Delegate)arg);

            return false;
        }

        public void CancelEvent()
        {
            if (w != null) w.CancelEvent();
        }

        public bool WasEventCanceled()
        {
            if (w != null) return w.WasEventCanceled();

            return false;
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
                    w.RconPrint("Error executing event handler for event " + eventname + " in resource ServerWrapper (" + Name + "): \n");
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
                        if (DelegateReferences.ContainsValue(s))
                        {
                            Delegate d = DelegateReferences.Where(kv => kv.Value == s).ElementAt(0).Key;

                            //DelegateReferences.Remove(d);

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
                        if (!DelegateReferences.ContainsKey(d)) DelegateReferences.Add(d, type.ToString() + "|" + Guid.NewGuid().ToString());

                        Converted.Add(DelegateReferences[d]);
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
            if (w != null) return w.GetInstanceID();

            return -1;
        }

        /*public string GetInvokingResource()
        {
            if (w != null) return w.GetInvokingResource();

            return null;
        }*/

        public bool StopResource(string resourceName)
        {
            if (w != null) return w.StopResource(resourceName);

            return false;
        }

        public bool StartResource(string resourceName)
        {
            if (w != null) return w.StartResource(resourceName);

            return false;
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