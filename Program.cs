using System;
using System.Linq;
using System.Net;

namespace HeelercrapServer
{
    class Common
    {
        public object UnixTimeNow()
        {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }
    }
    class Program
    {
        static void Main(string[] args)
        {

            // TCP server port
            int port = 1863;
            if (args.Length > 0)
                port = int.Parse(args[0]);

            string MSNPNotificationIP = "127.0.0.1";
            string MSNPSwitchboardIP = "127.0.0.2"; // different ip due to same port.

            Console.WriteLine($"MSNP NS Address: {IPAddress.Parse(MSNPNotificationIP)}");
            Console.WriteLine($"MSNP SB Address: {IPAddress.Parse(MSNPSwitchboardIP)}");
            Console.WriteLine($"MSNP Server Port: {port}");

            Console.WriteLine();

            // Create a new TCP chat server
            var NotificationServer = new MsnpServer(IPAddress.Parse(MSNPNotificationIP), port);
            var SwitchboardServer = new SwitchboardServer(IPAddress.Parse(MSNPSwitchboardIP), port);

            // Start the server
            Console.Write("Starting Heelercrap...");
            NotificationServer.Start();
            SwitchboardServer.Start();

            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    Console.WriteLine("Notification server users:");
                    Console.WriteLine(string.Join("\r\n", NotificationServer.GetSessions(NotificationServer)));
                    Console.WriteLine("Switchboard server users:");
                    Console.WriteLine(string.Join("\r\n", SwitchboardServer.GetSessions(SwitchboardServer)));
                    //break;
                }

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    NotificationServer.Restart();
                    SwitchboardServer.Restart();
                    Console.WriteLine("Done!");
                    continue;
                }

                // Multicast admin message to all sessions
                //line = "(admin) " + line;
                //server.Multicast(line);
            }

            // Stop the server
            //Console.Write("Server stopping...");
            //server.Stop();
            //Console.WriteLine("Done!");
        }
    }
}