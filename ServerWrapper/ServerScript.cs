using CitizenMP.Server;
using CitizenMP.Server.Game;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ServerWrapper
{
    public struct Player
    {
        Client c;

        public Player(Client client)
        {
            c = client;
        }

        public static implicit operator Client(Player p)
        {
            return p.c;
        }

        public static implicit operator Player(Client c)
        {
            return new Player(c);
        }

        public int Base { get { return c.Base; } set { c.Base = value; } }
        public string Guid { get { return c.Guid; } set { c.Guid = value; } }
        public IEnumerable<string> Identifiers { get { return c.Identifiers; } set { c.Identifiers = value; } }
        public long LastSeen { get { return c.LastSeen; } }
        public string Name { get { return c.Name; } set { c.Name = value; } }
        public ushort NetID { get { return c.NetID; } set { c.NetID = value; } }
        public object NetImplData { get { return c.NetImplData; } }
        public int Ping { get { return c.Ping; } }
        public uint ProtocolVersion { get { return c.ProtocolVersion; } set { c.ProtocolVersion = value; } }
        public NetEndPoint RemoteEP { get { return c.RemoteEP; } set { c.RemoteEP = value; } }
        public bool SentData { get { return c.SentData; } set { c.SentData = value; } }
        public NetEndPoint TempEP { get { return c.TempEP; } set { c.TempEP = value; } }
        public int TempID { get { return c.TempID; } set { c.TempID = value; } }
        public string Token { get { return c.Token; } set { c.Token = value; } }

        public void Touch()
        {
            c.Touch();
        }
    }

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

        public ServerScript(string name) : base()
        {
            Name = name;

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
            if (w != null) w.Print(new object[] { "ServerWrapper script \"" + Name + "\":" }.Concat(args).ToArray());
        }

        public void RconPrint(string str)
        {
            if (w != null) w.RconPrint("ServerWrapper script \"" + Name + "\": " + str);
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

            return default(Player);
        }

        public void TriggerClientEvent(string eventname, int netID, params object[] args)
        {
            if (w != null) w.TriggerClientEvent(eventname, netID, args);
        }

        public void RegisterServerEvent(string eventname)
        {
            if (w != null) w.RegisterServerEvent(eventname);
        }

        public bool TriggerEvent(string eventname, params object[] args)
        {
            if (w != null) return w.TriggerEvent(eventname, args);

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
            if(w != null)
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

            foreach (Delegate handler in EventHandlers[eventname].ToArray())
            {
                try
                {
                    if (handler.Method.GetParameters().Length != args.Length) continue;

                    handler.DynamicInvoke(args);
                }
                catch (Exception e)
                {
                    RconPrint("Error executing event handler for event " + eventname + " in resource ServerWrapper (" + Name + "): \n");
                    w.PrintException(e);

                    //EventHandlers[eventname].Clear();

                    break;
                }
            }
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
    }
}