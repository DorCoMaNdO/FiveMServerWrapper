using CitizenMP.Server;
using CitizenMP.Server.Game;
using System;
using System.Collections.Generic;

namespace ServerWrapper
{
    //public struct Player
    public class Player : MarshalByRefObject // For some reason on some machines this class being a struct throws an exception.
    {
        private Client c;

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

        //public int Base { get { return c.Base; } /*set { c.Base = value; }*/ }
        //public string Guid { get { return c.Guid; } /*set { c.Guid = value; }*/ }
        public IEnumerable<string> Identifiers { get { return c.Identifiers; } /*set { c.Identifiers = value; }*/ }
        public long LastSeen { get { return c.LastSeen; } }
        public string Name { get { return c.Name; } /*set { c.Name = value; }*/ }
        public ushort NetID { get { return c.NetID; } /*set { c.NetID = value; }*/ }
        //public object NetImplData { get { return c.NetImplData; } }
        public int Ping { get { return c.Ping; } }
        //public uint ProtocolVersion { get { return c.ProtocolVersion; } /*set { c.ProtocolVersion = value; }*/ }
        public NetEndPoint RemoteEP { get { return c.RemoteEP; } /*set { c.RemoteEP = value; }*/ }
        //public bool SentData { get { return c.SentData; } /*set { c.SentData = value; }*/ }
        //public NetEndPoint TempEP { get { return c.TempEP; } /*set { c.TempEP = value; }*/ }
        //public int TempID { get { return c.TempID; } /*set { c.TempID = value; }*/ }
        //public string Token { get { return c.Token; } /*set { c.Token = value; }*/ }

        /*public void Touch()
        {
            c.Touch();
        }*/

        /*public override object InitializeLifetimeService()
        {
            return null;
        }*/
    }
}