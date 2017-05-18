using ServerWrapper;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace ServerScriptExample
{
    [Export(typeof(IServerScript))]
    class ServerScriptExample : ServerScript
    {
        public ServerScriptExample() : base("ServerScriptExample")
        {
            // Do anything that doesn't require functions provided by the Wrapper, those can only be used during or after Load() as a proxy needs to be created.
        }

        public override void Load()
        {
            Interval = 1000; // Tick() interval in ms (likely delayed somewhat with reflection, do not rely on tick timings to be accurate)

            //TickTimer = false; // Disable the Tick() timer

            Print("Hi from " + Name);

            SetTimeout(5000, (timer) => { Print("Hi again from " + Name);  });

            RegisterServerEvent("CSharpEventTest");
            // NOTE: some events (such as "rconCommand") may pass NeoLua types, those cannot be marshalled and will throw an exception regardless of what type you assign them to, meaning said events are unsupported.
            AddEventHandler("CSharpEventTest", new Action<string>((message) =>
            {
                Print("CSharpEventTest has been raised - " + message);
            }));
            TriggerEvent("CSharpEventTest", "Example Argument");

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