using ServerWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace ServerScriptExample
{
    [Export(typeof(IServerScript))]
    class ServerScriptExample : ServerScript
    {
        public ServerScriptExample() : base("ServerScriptExample")
        {
            // Do anything that doesn't require functions provided by the Wrapper (Except AddDependency), those can only be used during or after Load() as a proxy needs to be created.
            //AddDependency(typeof(MySQL)); // Controls the order in which scripts get Load()'d
        }

        public override void Load()
        {
            Interval = 1000; // Tick() interval in ms (likely delayed somewhat with reflection, do not rely on tick timings to be accurate)

            //TickTimer = false; // Disable the Tick() timer

            Print("Hi from " + Name);

            SetTimeout(5000, (timer) => { Print("Hi again from " + Name); timer.Dispose(); });

            RegisterServerEvent("CSharpEventTest");
            // NOTE: some events (such as "rconCommand") may pass NeoLua types, LuaTable is converted to List<object> or Dictionary<object, object>, depending on whether the table keys has more than just the key id.
            AddEventHandler("CSharpEventTest", new Action<string>((message) =>
            {
                Print("CSharpEventTest has been raised - " + message);
            }));
            TriggerEvent("CSharpEventTest", "Example Argument");

            RegisterServerEvent("CSharpEventListTest");
            // Lists and arrays are converted to List<object>
            AddEventHandler("CSharpEventListTest", new Action<List<object>>((oarray) =>
            {
                List<string> list = oarray.Select(o => (string)o).ToList();
                Print("CSharpEventListTest has been raised - " + list.Count);
                foreach (string s in list) Print("CSharpEventListTest " + s);
            }));
            List<string> listtest = new List<string>() { "Example Argument 1", "Example Argument 2", "Example Argument 3" };
            TriggerEvent("CSharpEventListTest", listtest);

            RegisterServerEvent("CSharpEventArrayTest");
            AddEventHandler("CSharpEventArrayTest", new Action<List<object>>((oarray) =>
            {
                Print("CSharpEventArrayTest has been raised - " + oarray.Count);
                foreach (object o in oarray) Print("CSharpEventArrayTest " + o);
            }));
            TriggerEvent("CSharpEventArrayTest", new[] { listtest.ToArray() }); // Passing an array as the only argument makes the function accept the array members as the args, instead of the whole array as a single argument.

            RegisterServerEvent("CSharpEventDictTest");
            // Dictionaries are converted to Dictionary<object, object>
            AddEventHandler("CSharpEventDictTest", new Action<Dictionary<object, object>>((dict) =>
            {
                Print("CSharpEventDictTest has been raised - " + dict.Count);
                foreach (string s in dict.Select(kv => kv.Key + " - " + kv.Value)) Print("CSharpEventDictTest " + s);
            }));
            TriggerEvent("CSharpEventDictTest", new Dictionary<string, object>() { { "Example Argument 1", "Example Value 1" }, { "Example Argument 2", "Example Value 2" } });

            AddEventHandler("rconCommand", new Action<string, List<object>>((command, args) =>
            {
                //Print("Command: \"" + command + "\", Args: " + string.Join(",", args));

                if (command == "rconexample")
                {
                    CancelEvent();

                    RconPrint("Example rcon command.");
                }
            }));

            AddEventHandler("chatMessage", new Action<int, string, string>((source, playername, message) => 
            {
                //Print("source: " + source + ", playername: " + playername + ", message: " + message);

                string[] args = message.Split(' ');
                if(args[0] == "/example")
                {
                    CancelEvent();

                    TriggerClientEvent("chatMessage", source, "", new[] { 255, 255, 255 }, "This is an example command.");
                }
            }));

            /*MySQL.ExecuteQuery("CREATE TABLE IF NOT EXISTS ExampleTable (id INTEGER AUTO_INCREMENT PRIMARY KEY, steamid TEXT, cash BIGINT DEFAULT 0);", new Action(() =>
            {
                Print("Table created!");
            }));*/
        }

        public override void Unload()
        {
            //RemoveEventHandler("CSharpEventTest", CSharpEventTestHandler); // Used to be needed, now handled by the wrapper, can still be used at runtime.
        }

        public override void Tick()
        {
            var players = Players; // get_Players returns a new copy of the ReadOnlyDictionary with every call, cache one in a variable to prevent unnecessary additional work.
            
            Print("Clients: " + players.Count + ", names: " + string.Join("|", players.Select(kv => kv.Value.Name)) + ", pings: " + string.Join("|", players.Select(kv => kv.Value.Ping)));
        }
    }
}