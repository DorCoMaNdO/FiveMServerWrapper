using CitizenMP.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ServerWrapper
{
    internal class Wrapper : MarshalByRefObject
    {
        private static bool initialized = false;

        private static Assembly serverasm = null;

        private static Type LogScriptFunctions = null, RconScriptFunctions = null, PlayerScriptFunctions = null, EventScriptFunctions = null, ScriptEnvironment = null, ClientInstances = null;
        internal static Type ScriptEnvironmentScriptTimer = null;
        internal static PropertyInfo ScriptEnvironmentScriptTimerFunction = null, ScriptEnvironmentScriptTimerTickFrom = null;
        internal static IList ScriptEnvironmentScriptTimerList = null;
        private static MethodInfo LogScriptFunctionsPrint = null, RconScriptFunctionsRconPrint = null, PlayerScriptFunctionsDropPlayer = null, PlayerScriptFunctionsTempBanPlayer = null, PlayerScriptFunctionsGetHostId = null, EventScriptFunctionsTriggerClientEvent = null, EventScriptFunctionsRegisterServerEvent = null, EventScriptFunctionsTriggerEvent = null, EventScriptFunctionsCancelEvent = null, EventScriptFunctionsWasEventCanceled = null, ScriptEnvironmentSetTimeout = null, ScriptEnvironmentAddEventHandler = null, ScriptEnvironmentGetInstanceId = null;

        internal Type scriptEnvironmentScriptTimer { get { return ScriptEnvironmentScriptTimer; } }
        internal PropertyInfo scriptEnvironmentScriptTimerFunction { get { return ScriptEnvironmentScriptTimerFunction; } }
        internal PropertyInfo scriptEnvironmentScriptTimerTickFrom { get { return ScriptEnvironmentScriptTimerTickFrom; } }
        internal IList scriptEnvironmentScriptTimerList { get { return ScriptEnvironmentScriptTimerList; } }

        internal static ReadOnlyDictionary<string, Client> Clients { get; private set; }
        internal static ReadOnlyDictionary<ushort, Client> ClientsByNetId { get; private set; }

        private static Dictionary<string, List<IServerScript>> _scripts = new Dictionary<string, List<IServerScript>>();

        private static Dictionary<string, List<Delegate>> EventHandlers = null;

        private static Dictionary<IServerScript, Dictionary<string, List<Delegate>>> scripteventhandlers = new Dictionary<IServerScript, Dictionary<string, List<Delegate>>>();

        internal static List<ScriptTimer> ScriptTimers = new List<ScriptTimer>();

        //static Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();

        internal static Wrapper instance = null;

        internal static string ScriptsFolder;

        internal ReadOnlyDictionary<string, Player> Players { get { return new ReadOnlyDictionary<string, Player>(Clients.ToDictionary(kv => kv.Key, kv => (Player)kv.Value)); } }
        internal ReadOnlyDictionary<ushort, Player> PlayersByNetId { get { return new ReadOnlyDictionary<ushort, Player>(ClientsByNetId.ToDictionary(kv => kv.Key, kv => (Player)kv.Value)); } }

        public Wrapper()
        {
        }

        public static void Initialize()
        {
            if (initialized) return;

            initialized = true;

            instance = new Wrapper();

            serverasm = Assembly.GetAssembly(typeof(CitizenMP.Server.Resources.Resource));

            if (serverasm != null)
            {
                LogScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.LogScriptFunctions");

                if (LogScriptFunctions != null)
                {
                    LogScriptFunctionsPrint = LogScriptFunctions.GetMethod("Print_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                RconScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.RconScriptFunctions");

                if (RconScriptFunctions != null)
                {
                    RconScriptFunctionsRconPrint = RconScriptFunctions.GetMethod("RconPrint_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                PlayerScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.PlayerScriptFunctions");

                if (PlayerScriptFunctions != null)
                {
                    PlayerScriptFunctionsDropPlayer = PlayerScriptFunctions.GetMethod("DropPlayer", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    PlayerScriptFunctionsTempBanPlayer = PlayerScriptFunctions.GetMethod("TempBanPlayer", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    PlayerScriptFunctionsGetHostId = PlayerScriptFunctions.GetMethod("GetHostId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                EventScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.EventScriptFunctions");

                if (EventScriptFunctions != null)
                {
                    EventScriptFunctionsTriggerClientEvent = EventScriptFunctions.GetMethod("TriggerClientEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    EventScriptFunctionsRegisterServerEvent = EventScriptFunctions.GetMethod("RegisterServerEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    EventScriptFunctionsTriggerEvent = EventScriptFunctions.GetMethod("TriggerEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    EventScriptFunctionsCancelEvent = EventScriptFunctions.GetMethod("CancelEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    EventScriptFunctionsWasEventCanceled = EventScriptFunctions.GetMethod("WasEventCanceled_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                ScriptEnvironment = serverasm.GetType("CitizenMP.Server.Resources.ScriptEnvironment");

                if (ScriptEnvironment != null)
                {
                    ScriptEnvironmentSetTimeout = ScriptEnvironment.GetMethod("SetTimeout_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ScriptEnvironmentAddEventHandler = ScriptEnvironment.GetMethod("AddEventHandler_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ScriptEnvironmentGetInstanceId = ScriptEnvironment.GetMethod("GetInstanceId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    object CurrentEnvironmentInstance = ScriptEnvironment.GetProperty("CurrentEnvironment", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                    EventHandlers = (Dictionary<string, List<Delegate>>)ScriptEnvironment.GetField("m_eventHandlers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(CurrentEnvironmentInstance);

                    ScriptEnvironmentScriptTimer = ScriptEnvironment.GetNestedType("ScriptTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (ScriptEnvironmentScriptTimer != null)
                    {
                        ScriptEnvironmentScriptTimerList = (IList)ScriptEnvironment.GetField("m_timers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(CurrentEnvironmentInstance);

                        ScriptEnvironmentScriptTimerFunction = ScriptEnvironmentScriptTimer.GetProperty("Function", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        ScriptEnvironmentScriptTimerTickFrom = ScriptEnvironmentScriptTimer.GetProperty("TickFrom", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    }
                }

                ClientInstances = serverasm.GetType("CitizenMP.Server.ClientInstances");

                if (ClientInstances != null)
                {
                    Clients = (ReadOnlyDictionary<string, Client>)ClientInstances.GetProperty("Clients", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                    ClientsByNetId = (ReadOnlyDictionary<ushort, Client>)ClientInstances.GetProperty("ClientsByNetId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                }
            }

            ScriptsFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "resources", "ServerWrapper", "Scripts");

            if (!Directory.Exists(ScriptsFolder)) Directory.CreateDirectory(ScriptsFolder);

            /*AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                if (!assemblies.ContainsKey(e.Name))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(path, "*.dll"))
                        {
                            if (e.Name == AssemblyName.GetAssemblyName(file).FullName)
                            {
                                assemblies.Add(AssemblyName.GetAssemblyName(file).FullName, Assembly.Load(File.ReadAllBytes(file)));

                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        instance.PrintException(ex);
                    }
                }

                return assemblies.ContainsKey(e.Name) ? assemblies[e.Name] : null;
            };*/

            Load(ScriptsFolder);

            instance.AddEventHandler("rconCommand", new Action<string, dynamic>((string command, dynamic args) =>
            {
                if (command == "reloadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count > 0) Reload(ScriptsFolder);
                }
                else if (command == "loadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count == 0) Load(ScriptsFolder);
                }
                else if (command == "unloadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count > 0) Unload(ScriptsFolder);
                }
            }));
        }

        internal void PrintException(Exception e)
        {
            string msg = e.ToString();
            while(e.InnerException != null)
            {
                msg += "\r\n" + e.InnerException.ToString();

                e = e.InnerException;
            }

            foreach (string msgs in msg.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) RconPrint(msgs);
        }

        private static void Load(string path)
        {
            instance.RconPrint("ServerWrapper: Loading scripts in \"" + path + "\"...");

            MefLoader mefLoader = SeparateAppDomain.CreateInstance<MefLoader>(path, path);

            mefLoader.Domain.UnhandledException += (sender, e) => 
            {
                instance.RconPrint("ServerWrapper: Unhandled exception occured in script.");
                instance.PrintException((Exception)e.ExceptionObject);
            };

            List<IServerScript> scripts = mefLoader.Load<IServerScript>();

            if (scripts.Count == 0)
            {
                SeparateAppDomain.Delete(path);

                instance.RconPrint("ServerWrapper: No scripts found in \"" + path + "\".");

                return;
            }

            //assemblies.Clear();

            _scripts.Add(path, scripts);

            instance.RconPrint("ServerWrapper: " + scripts.Count + " script(s) found in \"" + path + "\".");
            instance.RconPrint("");

            // Sorting logic from ScriptHookVDotNet
            Dictionary<IServerScript, List<string>> graph = new Dictionary<IServerScript, List<string>>();
            Queue<IServerScript> sorted = new Queue<IServerScript>();

            foreach (IServerScript script in scripts) graph.Add(script, new List<string>(script.Dependencies));

            while (graph.Count > 0)
            {
                IServerScript s = null;

                foreach (var kv in graph)
                {
                    if (kv.Value.Count == 0)
                    {
                        s = kv.Key;

                        break;
                    }
                }

                if (s == null)
                {
                    instance.RconPrint("ServerWrapper: Detected a circular script dependency. Aborting...");
                    instance.RconPrint("");

                    return;
                }

                sorted.Enqueue(s);
                graph.Remove(s);

                foreach (var kv in graph) kv.Value.Remove(s.TypeName);
            }

            LoadScripts(sorted);
        }

        private static void LoadScripts(Queue<IServerScript> scripts)
        {
            if (scripts.Count > 0)
            {
                IServerScript script = scripts.Dequeue();

                ServerScript ss = ((ServerScript)script);

                ss.timer = new ScriptTimer(ss, 100, (timer) =>
                {
                    try
                    {
                        script.Tick();
                    }
                    catch (Exception e)
                    {
                        instance.RconPrint("ServerWrapper: \"" + script.Name + "\"'s Tick() failed.");

                        instance.PrintException(e);
                    }
                }, true);

                instance.RconPrint("ServerWrapper: Creating proxy for script \"" + script.Name + "\"...");

                ss.CreateProxy(instance, scripts);
            }
        }

        internal void ETPhoneHome(ServerScript script, Queue<IServerScript> scripts)
        {
            try
            {
                script.Load();
            }
            catch (Exception e)
            {
                RconPrint("ServerWrapper: \"" + script.Name + "\"'s Load() failed.");
                PrintException(e);
            }

            if (script.timer.Loop) script.timer.Start();

            LoadScripts(scripts);
        }

        private static void Reload(string path)
        {
            instance.RconPrint("ServerWrapper: Reloading scripts in \"" + path + "\"... (Unloading...)");
            instance.RconPrint("");

            Unload(path);

            instance.RconPrint("ServerWrapper: Reloading scripts in \"" + path + "\"... (Loading...)");
            instance.RconPrint("");

            Load(path);
        }

        private static void Unload(string path)
        {
            instance.RconPrint("ServerWrapper: Unloading scripts in \"" + path + "\"...");

            AppDomain oldAppDomain = null;
            List<IServerScript> oldscripts = new List<IServerScript>();

            if (_scripts.ContainsKey(path))
            {
                oldscripts = _scripts[path];

                _scripts.Remove(path);

                oldAppDomain = SeparateAppDomain.Extract(path);
            }

            instance.RconPrint("ServerWrapper: " + oldscripts.Count + " script(s) unloaded from \"" + path + "\".");
            instance.RconPrint("");

            ScriptTimer[] timers;
            lock (ScriptTimers) timers = ScriptTimers.ToArray();

            foreach (IServerScript script in oldscripts)
            {
                foreach (ScriptTimer st in timers.Where(st => st.caller == script)) st.Dispose();

                if (scripteventhandlers.ContainsKey(script))
                {
                    foreach (string eventname in scripteventhandlers[script].Keys)
                    {
                        instance.RemoveAllEventHandlers((ServerScript)script, eventname);

                        ((ServerScript)script).RemoveAllEventHandlers(eventname);
                    }

                    scripteventhandlers[script].Clear();

                    scripteventhandlers.Remove(script);
                }

                try
                {
                    ((ServerScript)script).timer.Dispose();

                    script.Unload();
                }
                catch (Exception e)
                {
                    instance.RconPrint("ServerWrapper: \"" + script.Name + "\"'s Unload() failed.");
                    instance.PrintException(e);
                }
            }

            scripteventhandlers.Clear();
            //assemblies.Clear();

            if (oldAppDomain != null) AppDomain.Unload(oldAppDomain);
        }

        internal void Print(params object[] args)
        {
            if (LogScriptFunctionsPrint != null) LogScriptFunctionsPrint.Invoke(null, new object[] { args });
        }

        internal void RconPrint(string str)
        {
            if (RconScriptFunctionsRconPrint != null) RconScriptFunctionsRconPrint.Invoke(null, new object[] { str });
        }

        internal ushort[] GetPlayers()
        {
            return PlayersByNetId.Keys.ToArray();
        }

        internal string GetPlayerName(int ID)
        {
            Player c = GetPlayerFromID(ID);

            return c.Name;
        }

        internal IEnumerable<string> GetPlayerIdentifiers(int ID)
        {
            Player c = GetPlayerFromID(ID);

            return c.Identifiers;
        }

        internal int GetPlayerPing(int ID)
        {
            Player c = GetPlayerFromID(ID);

            return c.Ping;
        }

        internal string GetPlayerEP(int ID)
        {
            Player c = GetPlayerFromID(ID);

            return c.RemoteEP.ToString();
        }

        internal double GetPlayerLastMsg(int ID)
        {
            Player c = GetPlayerFromID(ID);

            return Time.CurrentTime - c.LastSeen;
        }

        internal int GetHostID()
        {
            return PlayerScriptFunctions != null ? (int)PlayerScriptFunctionsGetHostId.Invoke(null, new object[] { }) : -1;
        }

        internal void DropPlayer(int ID, string reason)
        {
            if (PlayerScriptFunctionsDropPlayer != null) PlayerScriptFunctionsDropPlayer.Invoke(null, new object[] { ID, reason });
        }

        internal void TempBanPlayer(int ID, string reason)
        {
            if (PlayerScriptFunctionsTempBanPlayer != null) PlayerScriptFunctionsTempBanPlayer.Invoke(null, new object[] { ID, reason });
        }

        internal Player GetPlayerFromID(int ID)
        {
            return Players.Where(a => a.Value.NetID == ID).Select(a => a.Value).FirstOrDefault();
        }

        internal void TriggerClientEvent(string eventname, int netID, params object[] args)
        {
            if (EventScriptFunctionsTriggerClientEvent != null) EventScriptFunctionsTriggerClientEvent.Invoke(null, new object[] { eventname, netID, args });
        }

        internal void RegisterServerEvent(string eventname)
        {
            if (EventScriptFunctionsRegisterServerEvent != null) EventScriptFunctionsRegisterServerEvent.Invoke(null, new object[] { eventname });
        }

        internal bool TriggerEvent(string eventname, params object[] args)
        {
            if (EventScriptFunctionsTriggerEvent != null) return (bool)EventScriptFunctionsTriggerEvent.Invoke(null, new object[] { eventname, args });

            return false;
        }

        internal void CancelEvent()
        {
            if (EventScriptFunctionsCancelEvent != null) EventScriptFunctionsCancelEvent.Invoke(null, new object[] { });
        }

        internal bool WasEventCanceled()
        {
            if (EventScriptFunctionsWasEventCanceled != null) return (bool)EventScriptFunctionsWasEventCanceled.Invoke(null, new object[] { });

            return false;
        }

        internal ScriptTimer CreateTimer(ServerScript caller, int delay, bool loop = false)
        {
            ScriptTimer st = new ScriptTimer(caller, delay, (timer) => { caller.CallScriptTimerHandler(timer); }, loop);

            return st;
        }

        internal bool HasTimeoutFinished(ServerScript caller, string id)
        {
            lock (ScriptTimers) return !ScriptTimers.Any(st => st.ID == id);
        }

        internal void CancelTimeout(ServerScript caller, string id)
        {
            lock (ScriptTimers) foreach (ScriptTimer st in ScriptTimers.ToArray().Where(st => st.caller == caller && st.ID == id)) st.Cancel();
        }

        internal void AddEventHandler(ServerScript caller, string eventname, int args)
        {
            IServerScript icaller = (IServerScript)caller;
            lock (scripteventhandlers)
            {
                if (!scripteventhandlers.ContainsKey(icaller)) scripteventhandlers.Add(icaller, new Dictionary<string, List<Delegate>>());
                if (!scripteventhandlers[icaller].ContainsKey(eventname)) scripteventhandlers[icaller].Add(eventname, new List<Delegate>());

                if (scripteventhandlers[icaller][eventname].Any(h => h.Method.GetParameters().Length == args)) return;

                Delegate[] handlers = { new Action(() => { caller.TriggerLocalEvent(eventname); }), new Action<object>((a1) => { caller.TriggerLocalEvent(eventname, a1); }), new Action<object, object>((a1, a2) => { caller.TriggerLocalEvent(eventname, a1, a2); }), new Action<object, object, object>((a1, a2, a3) => { caller.TriggerLocalEvent(eventname, a1, a2, a3); }), new Action<object, object, object, object>((a1, a2, a3, a4) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4); }), new Action<object, object, object, object, object>((a1, a2, a3, a4, a5) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5); }), new Action<object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6); }), new Action<object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7); }), new Action<object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8); }), new Action<object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9); }), new Action<object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10); }), new Action<object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11); }), new Action<object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12); }), new Action<object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13); }), new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14); }), new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15); }), new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16) => { caller.TriggerLocalEvent(eventname, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16); }) };

                foreach (Delegate handler in handlers)
                {
                    if (handler.Method.GetParameters().Length != args) continue;

                    scripteventhandlers[icaller][eventname].Add(handler);

                    AddEventHandler(eventname, handler);
                }
            }
        }

        private void AddEventHandler(string eventname, Delegate eventhandler)
        {
            if (ScriptEnvironmentAddEventHandler != null) ScriptEnvironmentAddEventHandler.Invoke(null, new object[] { eventname, eventhandler });
        }

        internal void RemoveAllEventHandlers(ServerScript caller, string eventname)
        {
            IServerScript icaller = (IServerScript)caller;
            lock (scripteventhandlers)
            {
                if (!scripteventhandlers.ContainsKey(icaller)) scripteventhandlers.Add(icaller, new Dictionary<string, List<Delegate>>());
                if (!scripteventhandlers[icaller].ContainsKey(eventname)) scripteventhandlers[icaller].Add(eventname, new List<Delegate>());

                if (EventHandlers != null && EventHandlers.ContainsKey(eventname)) foreach (Delegate handler in scripteventhandlers[icaller][eventname]) EventHandlers[eventname].RemoveAll(h => h == handler);

                scripteventhandlers[icaller][eventname].Clear();
            }
        }

        internal int GetInstanceID()
        {
            return ScriptEnvironmentGetInstanceId != null ? (int)ScriptEnvironmentGetInstanceId.Invoke(null, new object[] { }) : -1;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}