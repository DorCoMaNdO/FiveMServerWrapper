using MySql.Data.MySqlClient;
using ServerWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;

namespace ServerScriptMySQL
{
    [Export(typeof(IServerScript))]
    public class MySQL : ServerScript
    {
        MySqlConnection DB;
        string host = "127.0.0.1", db = "serverscriptmysql", user = "root", pass = "";
        int port = 3306;
        static Queue<Tuple<string, Action>> NonQueryQueue = new Queue<Tuple<string, Action>>();
        static Queue<Tuple<string, Action<MySqlDataReader>>> ReaderQueue = new Queue<Tuple<string, Action<MySqlDataReader>>>();
        static Queue<Tuple<Delegate, MySqlDataReader>> ActionQueue = new Queue<Tuple<Delegate, MySqlDataReader>>();

        public MySQL() : base("MySQL Handler")
        {
            DB = new MySqlConnection("Server=" + host + ";Port=" + port + ";Database='" + db + "';Uid='" + user + "';Pwd='" + pass + "';");

            DB.Open();

            new Thread(() =>
            {
                while (true)
                {
                    lock (NonQueryQueue)
                    {
                        while (NonQueryQueue.Count > 0)
                        {
                            Tuple<string, Action> q = NonQueryQueue.Dequeue();
                            
                            string query = q.Item1;
                            using (MySqlCommand c = new MySqlCommand(query, DB)) c.ExecuteNonQuery();
                            
                            lock (ActionQueue) ActionQueue.Enqueue(new Tuple<Delegate, MySqlDataReader>(q.Item2, null));
                            
                            TickTimer = true;
                        }
                    }

                    lock (ReaderQueue)
                    {
                        while (ReaderQueue.Count > 0)
                        {
                            Tuple<string, Action<MySqlDataReader>> q = ReaderQueue.Dequeue();
                            
                            string query = q.Item1;
                            MySqlDataReader r;
                            using (MySqlCommand c = new MySqlCommand(query, DB)) r = c.ExecuteReader();
                            
                            lock (ActionQueue) ActionQueue.Enqueue(new Tuple<Delegate, MySqlDataReader>(q.Item2, r));

                            TickTimer = true;
                        }
                    }

                    Thread.Sleep(0);
                }
            }).Start();
        }

        public override void Load()
        {
            Interval = 0;

            TickTimer = false;
        }

        public override void Unload()
        {
        }

        public static void ExecuteQuery(string query)
        {
            ExecuteQuery(query, null);
        }

        public static void ExecuteQuery(string query, Action callback)
        {
            lock (NonQueryQueue) NonQueryQueue.Enqueue(new Tuple<string, Action>(query, callback));
        }

        public static void ExecuteReader(string query, Action<MySqlDataReader> callback)
        {
            lock (ReaderQueue) ReaderQueue.Enqueue(new Tuple<string, Action<MySqlDataReader>>(query, callback));
        }

        public override void Tick()
        {
            lock (ActionQueue)
            {
                while (ActionQueue.Count > 0)
                {
                    Tuple<Delegate, MySqlDataReader> q = ActionQueue.Dequeue();
                    
                    if (q.Item1 == null) continue;
                    
                    if (q.Item2 != null)
                    {
                        using (q.Item2) ((Action<MySqlDataReader>)q.Item1)(q.Item2);
                    }
                    else
                    {
                        ((Action)q.Item1)();
                    }
                }
            }

            TickTimer = false;
        }
    }
}
