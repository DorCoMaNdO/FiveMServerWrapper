using CitizenMP.Server;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace ServerWrapper
{
    internal enum PrintType
    {
        Info,
        Debug,
        Warning,
        Error,
        Fatal
    }

    internal class Wrapper : MarshalByRefObject
    {
        private static bool initialized = false;

        private static Assembly serverasm = null;

        private static Type PlayerScriptFunctions = null, EventScriptFunctions = null, ScriptEnvironment = null, ClientInstances = null, ResourceScriptFunctions = null;
        internal static Type SEScriptTimer = null;
        internal static PropertyInfo SEScriptTimerFunction = null, SEScriptTimerTickFrom = null;
        internal static IList SEScriptTimerList = null;
        private static MethodInfo PSFDropPlayer = null, PSFTempBanPlayer = null, PSFGetHostId = null;
        private static MethodInfo ESFTriggerClientEvent = null, ESFRegisterServerEvent = null, ESFTriggerEvent = null, ESFCancelEvent = null, ESFWasEventCanceled = null;
        private static object SECurrentEnvironment = null, SELuaEnvironment = null;
        private static MethodInfo SESetTimeout = null, SEAddEventHandler = null, SEGetInstanceId = null;
        private static MethodInfo RSFStopResource = null, RSFStartResource = null, RSFSetGameType = null, RSFSetMapName = null;

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
                PlayerScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.PlayerScriptFunctions");

                if (PlayerScriptFunctions != null)
                {
                    PSFDropPlayer = PlayerScriptFunctions.GetMethod("DropPlayer", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    PSFTempBanPlayer = PlayerScriptFunctions.GetMethod("TempBanPlayer", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    PSFGetHostId = PlayerScriptFunctions.GetMethod("GetHostId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                EventScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.EventScriptFunctions");

                if (EventScriptFunctions != null)
                {
                    ESFTriggerClientEvent = EventScriptFunctions.GetMethod("TriggerClientEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ESFRegisterServerEvent = EventScriptFunctions.GetMethod("RegisterServerEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ESFTriggerEvent = EventScriptFunctions.GetMethod("TriggerEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ESFCancelEvent = EventScriptFunctions.GetMethod("CancelEvent_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    ESFWasEventCanceled = EventScriptFunctions.GetMethod("WasEventCanceled_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                ScriptEnvironment = serverasm.GetType("CitizenMP.Server.Resources.ScriptEnvironment");

                if (ScriptEnvironment != null)
                {
                    SESetTimeout = ScriptEnvironment.GetMethod("SetTimeout_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    SEAddEventHandler = ScriptEnvironment.GetMethod("AddEventHandler_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    SEGetInstanceId = ScriptEnvironment.GetMethod("GetInstanceId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    SECurrentEnvironment = ScriptEnvironment.GetProperty("CurrentEnvironment", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                    EventHandlers = (Dictionary<string, List<Delegate>>)ScriptEnvironment.GetField("m_eventHandlers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(SECurrentEnvironment);

                    SELuaEnvironment = ScriptEnvironment.GetField("m_luaEnvironment", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(SECurrentEnvironment);

                    SEScriptTimer = ScriptEnvironment.GetNestedType("ScriptTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    if (SEScriptTimer != null)
                    {
                        SEScriptTimerList = (IList)ScriptEnvironment.GetField("m_timers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(SECurrentEnvironment);

                        SEScriptTimerFunction = SEScriptTimer.GetProperty("Function", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        SEScriptTimerTickFrom = SEScriptTimer.GetProperty("TickFrom", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    }
                }

                ClientInstances = serverasm.GetType("CitizenMP.Server.ClientInstances");

                if (ClientInstances != null)
                {
                    Clients = (ReadOnlyDictionary<string, Client>)ClientInstances.GetProperty("Clients", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                    ClientsByNetId = (ReadOnlyDictionary<ushort, Client>)ClientInstances.GetProperty("ClientsByNetId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                }

                ResourceScriptFunctions = serverasm.GetType("CitizenMP.Server.Resources.ResourceScriptFunctions");

                if (ResourceScriptFunctions != null)
                {
                    RSFStopResource = ScriptEnvironment.GetMethod("StopResource_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    RSFStartResource = ScriptEnvironment.GetMethod("StartResource_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    RSFSetGameType = ScriptEnvironment.GetMethod("SetGameType_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    RSFSetMapName = ScriptEnvironment.GetMethod("SetMapName_f", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                /*foreach (Type t in serverasm.GetTypes())
                {
                    foreach (MethodInfo mi in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        foreach (object attr in mi.GetCustomAttributes()) if (attr.GetType().ToString().Contains("LuaMember")) instance.Print(t + " (" + mi.ReturnType + ") " + mi.Name, string.Join(", ", mi.GetParameters().Select(pi => "(" + pi.ParameterType + ") " + pi.Name)));

                        //instance.Print(t + " (" + mi.ReturnType + ") " + mi.Name, string.Join(", ", mi.GetParameters().Select(pi => "(" + pi.ParameterType + ") " + pi.Name)));
                    }

                    //foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) instance.Print(t + " (" + fi.FieldType + ") " + fi.Name);

                    //foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) instance.Print(t + " (" + pi.PropertyType + ") " + pi.Name);
                }*/
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

            instance.AddEventHandler("rconCommand", new Action<string, object>((command, args) =>
            {
                string c = command.ToLower();

                if (c == "reloadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count > 0) Reload(ScriptsFolder);
                }
                else if (c == "loadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count == 0) Load(ScriptsFolder);
                }
                else if (c == "unloadscripts")
                {
                    instance.CancelEvent();

                    if (_scripts.Count > 0) Unload(ScriptsFolder);
                }
                else if (c == "swupdate")
                {
                    instance.CancelEvent();

                    Process.Start("explorer.exe", "https://forum.fivem.net/t/release-c-net-wrapper-for-server-side-scripts/20325");
                }
                else
                {
                    lock (scripteventhandlers) foreach (IServerScript script in scripteventhandlers.Keys) if (scripteventhandlers[script].ContainsKey("rconCommand")) foreach (Delegate handler in scripteventhandlers[script]["rconCommand"]) handler.DynamicInvoke(command, args);
                }

                /*new Action<string, List<object>>((c, a) =>
                {
                    if (c == "reloadscripts")
                    {
                        instance.CancelEvent();

                        if (_scripts.Count > 0) Reload(ScriptsFolder);
                    }
                    else if (c == "loadscripts")
                    {
                        instance.CancelEvent();

                        if (_scripts.Count == 0) Load(ScriptsFolder);
                    }
                    else if (c == "unloadscripts")
                    {
                        instance.CancelEvent();

                        if (_scripts.Count > 0) Unload(ScriptsFolder);
                    }
                    else if (c == "swupdate")
                    {
                        instance.CancelEvent();

                        Process.Start("explorer.exe", "https://forum.fivem.net/t/release-c-net-wrapper-for-server-side-scripts/20325");
                    }
                })(command, (List<object>)ConvertArgsFromNLua(args)[0]);*/
            }));


            new Thread(() =>
            {
                int CurrentMajor, CurrentMinor, CurrentBuild, CurrentRevision, NewMajor, NewMinor, NewBuild;
                string[] currentver = Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.');
                if (currentver.Length < 1 || !int.TryParse(currentver[0], out CurrentMajor)) CurrentMajor = 1;
                if (currentver.Length < 2 || !int.TryParse(currentver[1], out CurrentMinor)) CurrentMinor = 2;
                if (currentver.Length < 3 || !int.TryParse(currentver[2], out CurrentBuild)) CurrentBuild = 0;
                if (currentver.Length < 4 || !int.TryParse(currentver[3], out CurrentRevision)) CurrentRevision = 0;
                string current = CurrentMajor + "." + CurrentMinor + "." + CurrentBuild, latest = "";

                //CurrentMajor = 0;

                bool update = false;

                while (true)
                {
                    try
                    {
                        if (!update)
                        {
                            using (WebClient w = new WebClient())
                            {
                                w.Proxy = null;
                                w.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)"); // Fails without a user-agent header.

                                JObject data = JObject.Parse(w.DownloadString("https://api.github.com/repos/DorCoMaNdO/FiveMServerWrapper/releases/latest"));
                                string[] ver = data["tag_name"].ToString().Split('.');
                                if (ver.Length < 1 || !int.TryParse(ver[0], out NewMajor)) NewMajor = 1;
                                if (ver.Length < 2 || !int.TryParse(ver[1], out NewMinor)) NewMinor = 2;
                                if (ver.Length < 3 || !int.TryParse(ver[2], out NewBuild)) NewBuild = 0;
                                latest = NewMajor + "." + NewMinor + "." + NewBuild;

                                update = NewMajor > CurrentMajor || NewMajor == CurrentMajor && NewMinor > CurrentMinor || NewMajor == CurrentMajor && NewMinor == CurrentMinor && NewBuild > CurrentBuild;
                            }
                        }

                        if (update)
                        {
                            instance.Print("---------------------------------------------------------------------------------------------------");
                            instance.Print("A ServerWrapper update is available!");
                            instance.Print("Current version: " + current + ", latest version: " + latest + ".");
                            instance.Print("For more info, visit https://forum.fivem.net/t/release-c-net-wrapper-for-server-side-scripts/20325.");
                            instance.Print("Enter the command \"swupdate\" to open the link above in your default browser.");
                            instance.Print("---------------------------------------------------------------------------------------------------");
                        }
                    }
                    catch (Exception e)
                    {
                        instance.Print(PrintType.Error, "Update check failed, will try again in 10 minutes.");
                        instance.PrintException(e);
                    }

                    Thread.Sleep(600000);
                }
            }).Start();
        }

        internal void PrintException(Exception e)
        {
            string msg = e.ToString();
            while (e.InnerException != null)
            {
                msg += "\r\n" + e.InnerException.ToString();

                e = e.InnerException;
            }

            foreach (string m in msg.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) Print(PrintType.Error, m);
        }

        private static void Load(string path)
        {
            instance.Print("Loading scripts in \"" + path + "\"...");

            foreach (string file in Directory.GetFiles(path, "*.dll"))
            {
                if (AssemblyName.GetAssemblyName(file).Name == "ServerWrapper")
                {
                    instance.Print("Found ServerWrapper assembly in Scripts folder. Removing...");
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        instance.Print(PrintType.Warning, "Failed to remove ServerWrapper assembly from Scripts folder, this may cause issues.");
                        instance.PrintException(e);
                    }
                }
            }

            /*if (Directory.GetFiles(path, "*.cs").Length > 0)
            {
                string localassembly = typeof(ServerScript).Assembly.Location;
                AppDomain domain = AppDomain.CreateDomain("Test");
                domain.DoCallBack(() =>
                {
                    System.CodeDom.Compiler.CompilerParameters options = new System.CodeDom.Compiler.CompilerParameters();
                    options.CompilerOptions = "/optimize /unsafe";
                    options.GenerateInMemory = true;
                    //options.GenerateInMemory = false;
                    options.IncludeDebugInformation = true;
                    options.ReferencedAssemblies.AddRange(new string[]
                    {
                        "System.dll",
                        "System.Core.dll",
                        "System.Drawing.dll",
                        "System.Windows.Forms.dll",
                        "System.XML.dll",
                        "System.XML.Linq.dll",
                        localassembly,
                        "System.ComponentModel.Composition.dll"
                    });

                    System.CodeDom.Compiler.CodeDomProvider compiler = new Microsoft.CSharp.CSharpCodeProvider();

                    foreach (string file in Directory.GetFiles(path, "*.cs"))
                    {
                        options.OutputAssembly = Path.ChangeExtension(file, ".dll");

                        System.CodeDom.Compiler.CompilerResults result = compiler.CompileAssemblyFromFile(options, file);

                        if (!result.Errors.HasErrors)
                        {
                            instance.RconPrint((result.CompiledAssembly == null).ToString());
                            instance.RconPrint(result.PathToAssembly);
                        }
                        else
                        {
                            string errors = "";

                            foreach (System.CodeDom.Compiler.CompilerError error in result.Errors) errors += "   at line " + error.Line + ": " + error.ErrorText + "\r\n";

                            instance.RconPrint("[ERROR] Failed to compile '" + Path.GetFileName(file) + "' with " + result.Errors.Count + " error(s):\r\n" + errors.ToString());
                        }
                    }
                });

                AppDomain.Unload(domain);
            }*/

            MefLoader mefLoader = SeparateAppDomain.CreateInstance<MefLoader>(path, path);

            mefLoader.Domain.UnhandledException += (sender, e) =>
            {
                instance.Print(PrintType.Error, "Unhandled exception occured in script.");
                instance.PrintException((Exception)e.ExceptionObject);
            };

            /*mefLoader.Domain.AssemblyResolve += (sender, e) =>
            {

            };*/

            List<IServerScript> scripts = mefLoader.Load<IServerScript>();

            if (scripts.Count == 0)
            {
                SeparateAppDomain.Delete(path);

                instance.Print("No scripts found in \"" + path + "\".");

                return;
            }

            //assemblies.Clear();

            _scripts.Add(path, scripts);

            instance.Print(scripts.Count + " script(s) found in \"" + path + "\".");
            instance.Print("");

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
                    instance.Print(PrintType.Fatal, "Detected a circular script dependency. Aborting...");
                    instance.Print("");

                    return;
                }

                sorted.Enqueue(s);
                graph.Remove(s);

                foreach (var kv in graph) kv.Value.Remove(s.TypeName);
            }

            if (graph.Count == 0) LoadScripts(sorted);
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
                        instance.Print(PrintType.Error, "\"" + script.Name + "\"'s Tick() failed.");
                        instance.PrintException(e);
                    }
                }, true);

                instance.Print("Creating proxy for script \"" + script.Name + "\"...");

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
                Print(PrintType.Error, "\"" + script.Name + "\"'s Load() failed.");
                PrintException(e);
            }

            if (script.timer.Loop) script.timer.Start();

            LoadScripts(scripts);
        }

        private static void Reload(string path)
        {
            instance.Print("Reloading scripts in \"" + path + "\"... (Unloading...)");
            instance.Print("");

            Unload(path);

            instance.Print("Reloading scripts in \"" + path + "\"... (Loading...)");
            instance.Print("");

            Load(path);
        }

        private static void Unload(string path)
        {
            instance.Print("Unloading scripts in \"" + path + "\"...");

            AppDomain oldAppDomain = null;
            List<IServerScript> oldscripts = new List<IServerScript>();

            if (_scripts.ContainsKey(path))
            {
                oldscripts = _scripts[path];

                _scripts.Remove(path);

                oldAppDomain = SeparateAppDomain.Extract(path);
            }

            instance.Print(oldscripts.Count + " script(s) unloaded from \"" + path + "\".");
            instance.Print("");

            ScriptTimer[] timers;
            lock (ScriptTimers) timers = ScriptTimers.ToArray();

            foreach (IServerScript script in oldscripts)
            {
                foreach (ScriptTimer st in timers.Where(st => st.caller == script)) st.Dispose();

                lock (scripteventhandlers)
                {
                    if (scripteventhandlers.ContainsKey(script))
                    {
                        foreach (string eventname in scripteventhandlers[script].Keys)
                        {
                            try
                            {
                                instance.RemoveAllEventHandlers((ServerScript)script, eventname);

                                ((ServerScript)script).RemoveAllEventHandlers(eventname);
                            }
                            catch (Exception e)
                            {
                                instance.Print(PrintType.Error, "Failed to remove \"" + script.Name + "\"'s event handlers for event \"" + eventname + "\".");
                                instance.PrintException(e);
                            }
                        }

                        scripteventhandlers[script].Clear();

                        scripteventhandlers.Remove(script);
                    }
                }

                ScriptTimer t = ((ServerScript)script).timer;
                if (t != null) t.Dispose();

                try
                {
                    script.Unload();
                }
                catch (Exception e)
                {
                    instance.Print(PrintType.Error, "\"" + script.Name + "\"'s Unload() failed.");
                    instance.PrintException(e);
                }
            }

            lock (scripteventhandlers) scripteventhandlers.Clear();
            //assemblies.Clear();

            if (oldAppDomain != null) AppDomain.Unload(oldAppDomain);
        }

        internal void Print(params object[] args)
        {
            Print(PrintType.Info, args);
        }

        internal void Print(PrintType type, params object[] args)
        {
            Print(this.Log("Print", "ServerWrapper\\Wrapper.cs"), type, args);
        }

        internal void Print(string prefix, string file, int line, PrintType type, params object[] args)
        {
            Print(this.Log(prefix, file, line), type, args);
        }

        internal void Print(ServerScript script, string prefix, string file, int line, PrintType type, params object[] args)
        {
            Print(LogExtensions.Log(script, prefix, file, line), type, args);
        }

        internal void Print(CitizenMP.Server.Logging.BaseLog log, PrintType type, params object[] args)
        {
            Func<string> func = () => { return string.Join(" ", args.Select(a => a ?? "null")); };
            if (type == PrintType.Debug)
            {
                log.Debug(func);
            }
            else if (type == PrintType.Warning)
            {
                log.Warn(func);
            }
            else if (type == PrintType.Error)
            {
                log.Error(func);
            }
            else if (type == PrintType.Fatal)
            {
                log.Fatal(func);
            }
            else
            {
                log.Info(func);
            }
        }

        internal ushort[] GetPlayers()
        {
            return PlayersByNetId.Keys.ToArray();
        }

        internal string GetPlayerName(int ID)
        {
            Player p = GetPlayerFromID(ID);

            return p != null ? p.Name : null;
        }

        internal IEnumerable<string> GetPlayerIdentifiers(int ID)
        {
            Player p = GetPlayerFromID(ID);

            return p != null ? p.Identifiers : null;
        }

        internal int GetPlayerPing(int ID)
        {
            Player p = GetPlayerFromID(ID);

            return p != null ? p.Ping : -1;
        }

        internal string GetPlayerEP(int ID)
        {
            Player p = GetPlayerFromID(ID);

            return p != null ? p.RemoteEP.ToString() : null;
        }

        internal double GetPlayerLastMsg(int ID)
        {
            Player p = GetPlayerFromID(ID);

            return p != null ? Time.CurrentTime - p.LastSeen : 99999999;
        }

        internal int GetHostID()
        {
            return PlayerScriptFunctions != null ? (int)PSFGetHostId.Invoke(null, new object[] { }) : -1;
        }

        internal void DropPlayer(int ID, string reason)
        {
            if (PSFDropPlayer != null) PSFDropPlayer.Invoke(null, new object[] { ID, reason });
        }

        internal void TempBanPlayer(int ID, string reason)
        {
            if (PSFTempBanPlayer != null) PSFTempBanPlayer.Invoke(null, new object[] { ID, reason });
        }

        internal Player GetPlayerFromID(int ID)
        {
            return Players.Where(a => a.Value.NetID == ID).Select(a => a.Value).FirstOrDefault();
        }

        internal void TriggerClientEvent(string eventname, int netID, params object[] args)
        {
            if (ESFTriggerClientEvent != null) ESFTriggerClientEvent.Invoke(null, new object[] { eventname, netID, ConvertArgsToNLua(args) });
        }
        internal void RegisterServerEvent(string eventname)
        {
            if (ESFRegisterServerEvent != null) ESFRegisterServerEvent.Invoke(null, new object[] { eventname });
        }

        internal bool TriggerEvent(string eventname, params object[] args)
        {
            if (ESFTriggerEvent != null) return (bool)ESFTriggerEvent.Invoke(null, new object[] { eventname, ConvertArgsToNLua(args) });

            return false;
        }

        internal void CancelEvent()
        {
            if (ESFCancelEvent != null) ESFCancelEvent.Invoke(null, new object[] { });
        }

        internal bool WasEventCanceled()
        {
            return ESFWasEventCanceled != null ? (bool)ESFWasEventCanceled.Invoke(null, new object[] { }) : false;
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

                Delegate[] handlers =
                {
                    new Action(() => { caller.TriggerLocalEvent(eventname); }),
                    new Action<object>((a1) => { object[] cargs = ConvertArgsFromNLua(a1); caller.TriggerLocalEvent(eventname, cargs[0]); }),
                    new Action<object, object>((a1, a2) => { object[] cargs = ConvertArgsFromNLua(a1, a2); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1]); }),
                    new Action<object, object, object>((a1, a2, a3) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2]); }),
                    new Action<object, object, object, object>((a1, a2, a3, a4) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3]); }),
                    new Action<object, object, object, object, object>((a1, a2, a3, a4, a5) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4]); }),
                    new Action<object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5]); }),
                    new Action<object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6]); }),
                    new Action<object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7]); }),
                    new Action<object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10], cargs[11]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10], cargs[11], cargs[12]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10], cargs[11], cargs[12], cargs[13]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10], cargs[11], cargs[12], cargs[13], cargs[14]); }),
                    new Action<object, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>((a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16) => { object[] cargs = ConvertArgsFromNLua(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16); caller.TriggerLocalEvent(eventname, cargs[0], cargs[1], cargs[2], cargs[3], cargs[4], cargs[5], cargs[6], cargs[7], cargs[8], cargs[9], cargs[10], cargs[11], cargs[12], cargs[13], cargs[14], cargs[15]); })
                };

                foreach (Delegate handler in handlers)
                {
                    if (handler.Method.GetParameters().Length != args) continue;

                    scripteventhandlers[icaller][eventname].Add(handler);

                    if (eventname != "rconCommand") AddEventHandler(eventname, handler);
                }
            }
        }

        internal static object[] ConvertArgsFromNLua(params object[] args)
        {
            List<object> Converted = new List<object>();

            foreach (object arg in args)
            {
                Type type = arg.GetType();

                if (type == typeof(Neo.IronLua.LuaTable))
                {
                    Neo.IronLua.LuaTable table = (Neo.IronLua.LuaTable)arg;

                    bool dict = false;
                    for (int i = 0; i < table.Values.Keys.Count; i++)
                    {
                        if (table.Values.Keys.ElementAt(i).ToString() != (i + 1).ToString())
                        {
                            dict = true;

                            break;
                        }
                    }

                    if (!dict)
                    {
                        List<object> tvalues = new List<object>();
                        foreach (object o in table.Values.Values) tvalues.Add(ConvertArgsFromNLua(o)[0]);

                        Converted.Add(tvalues);
                    }
                    else
                    {
                        Dictionary<object, object> tvalues = new Dictionary<object, object>();
                        foreach (var kv in table.Values) tvalues.Add(ConvertArgsFromNLua(kv.Key)[0], ConvertArgsFromNLua(kv.Value)[0]);

                        Converted.Add(tvalues);
                    }

                    continue;
                }

                if (type.ToString().StartsWith("Neo.IronLua")) instance.Print(PrintType.Warning, "Conversation required for " + type + "!");

                Converted.Add(arg);
            }

            return Converted.ToArray();
        }

        private static readonly Type[] WriteTypes = new Type[]
        {
            typeof(string),
            typeof(DateTime),
            typeof(Enum), 
            typeof(decimal),
            typeof(Guid),
        };

        internal static object[] ConvertArgsToNLua(params object[] args)
        {
            List<object> Converted = new List<object>();

            foreach (object arg in args)
            {
                Type type = arg.GetType();
                Type[] interfaces = type.GetInterfaces();

                if (type.IsPrimitive || WriteTypes.Contains(type))
                {
                    Converted.Add(arg);

                    continue;
                }
                else if (interfaces.Contains(typeof(IDictionary)))
                {
                    IDictionary dict = (IDictionary)arg;
                    Neo.IronLua.LuaTable table = new Neo.IronLua.LuaTable();
                    foreach (object key in dict.Keys) Neo.IronLua.LuaTable.insert(table, ConvertArgsToNLua(key)[0], ConvertArgsToNLua(dict[key])[0]);

                    Converted.Add(table);

                    continue;
                }
                else if (interfaces.Contains(typeof(IList)))
                {
                    IList list = (IList)arg;
                    Neo.IronLua.LuaTable table = new Neo.IronLua.LuaTable();
                    foreach (object o in list) Neo.IronLua.LuaTable.insert(table, ConvertArgsToNLua(o)[0]);

                    Converted.Add(table);

                    continue;
                }
                else if (interfaces.Contains(typeof(IEnumerable)))
                {
                    IEnumerable enumerable = (IEnumerable)arg;
                    Neo.IronLua.LuaTable table = new Neo.IronLua.LuaTable();
                    foreach (object o in enumerable) Neo.IronLua.LuaTable.insert(table, ConvertArgsToNLua(o)[0]);

                    Converted.Add(table);

                    continue;
                }

                Converted.Add(arg);
            }

            return Converted.ToArray();
        }

        private void AddEventHandler(string eventname, Delegate eventhandler)
        {
            if (SEAddEventHandler != null) SEAddEventHandler.Invoke(null, new object[] { eventname, eventhandler });
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
            return SEGetInstanceId != null ? (int)SEGetInstanceId.Invoke(null, new object[] { }) : -1;
        }

        internal int GetEventSource()
        {
            return SELuaEnvironment != null ? (int)((Neo.IronLua.LuaGlobal)SELuaEnvironment)["source"] : -1;
        }

        internal bool StopResource(string resourceName)
        {
            return RSFStopResource != null && resourceName != "ServerWrapper" ? (bool)RSFStopResource.Invoke(null, new object[] { resourceName }) : false;
        }

        internal bool StartResource(string resourceName)
        {
            return RSFStartResource != null && resourceName != "ServerWrapper" ? (bool)RSFStartResource.Invoke(null, new object[] { resourceName }) : false;
        }

        internal void SetGameType(string gameType)
        {
            if (RSFSetGameType != null) RSFSetGameType.Invoke(null, new object[] { gameType });
        }

        internal void SetMapName(string mapName)
        {
            if (RSFSetMapName != null) RSFSetMapName.Invoke(null, new object[] { mapName });
        }
    }
}