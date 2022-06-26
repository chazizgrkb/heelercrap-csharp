using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace MsnpServer
{
    class MsnpSession : TcpSession
    {
        // put any client variables shit here, putting them in OnReceived will redefine them.
        // it does work with multiple sockets(?) as seen here.
        // https://cdn.discordapp.com/attachments/954065147129917441/990426981554323466/unknown.png
        //public int msnp; // the MSNP version, required due to clients having different parameters.
        //public string email; // the email.
        //public string clientver; // the MSN version of the client.

        public object Msnp { get; private set; }
        public object Email { get; private set; }
        public object ClientVersion { get; private set; }

        public MsnpSession(TcpServer server) : base(server) 
        {
            Msnp = 0;
            Email = "unknown@email.com";
            ClientVersion = "0.0.0000";
        }

        // why is this called a while after the client sends some shit?
        protected override void OnConnected()
        {
            Console.WriteLine($"MSNP TCP session with Id {Id} connected!");

            // Send invite message
            //string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";

            //SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"MSNP TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size).Trim('\r', '\n'); // MSNP adds newlines, so trim that.
            string clientInfo = "Info: " + Socket.RemoteEndPoint + ", MSNP" + Msnp + ", (Client version: " + ClientVersion + "), " + Email + ", " + Id;
            Console.WriteLine(clientInfo);
            Console.WriteLine("Input: >>> " + message);

            string[] input = message.Split(" "); // split client's input.
            string[] output = null;
            string command = input[0]; // the name of the command
            string number = null;
            bool isDisconnecting = false;
            if (input.Length == 1) {
                number = "0"; // commands such as "OUT" will cause Heelercrap to crash due to no transaction ID, so just define a dummy transaction ID.
            } else {
                number = input[1]; // incrementing ID
            }
            string outputEncoded = null;

            /* sources : 
             * 
             * https://wiki.nina.chat/wiki/Protocols/MSNP/Commands (msnp8+)
             * http://msn-messenger-protocol.herokuapp.com/sitev1/ (msnp7)
             */

            switch (command)
            {
                case "OUT": // Disconnection, pretty simple, right
                    output = new string[] { "OUT" };
                    isDisconnecting = true;
                    break;
                case "VER": // VER [numb] MSNP8 CVR0
                            // (some clients, like msn 4, report multiple MSNP versions. however, use the 1st listed one)
                    Msnp = Int16.Parse(input[2].Remove(0,4));
                    output = new string[] { "VER", number, "MSNP" + Msnp, "CVR0" };
                    break;
                case "INF": // Old authentication check, return MD5.
                    output = new string[] { "INF", number, "MD5" };
                    break;
                case "CVR": // CVR [numb] 0x0409 win 4.10 i386 MSNMSGR 5.0.0544 MSMSGS example@passport.com
                    if (Convert.ToInt32(Msnp) >= 8) //MSNP8 and above added the email parameter, likely because of the new auth process.
                    {
                        ClientVersion = input[7];
                        Email = input[9];
                    } 
                    else
                    {
                        ClientVersion = input[7];
                    }
                    output = new string[] { "CVR", number, (string)ClientVersion, (string)ClientVersion, "1.0.0000", "http://updatelink/", "http://website/" };
                    break;
                case "USR": // authentication commands
                    string authCommand = input[3];
                    if (input[2] == "MD5") //md5 auth, msnp2-msn7
                    {
                        switch (authCommand)
                        {
                            case "I": // USR [numb] MD5 I [email]
                                Email = input[4]; //CVR does not exist in MSNP7 and below.
                                output = new string[] { "USR", number, "MD5", "S", "1013928519.693957190" }; // placeholder
                                break;
                            case "S": // USR [numb] MD5 S [md5 hash???]
                                output = new string[] { "USR", number, "OK", (string)Email, "LegacyUser", "1" };
                                break;
                            default: // just awkwardly reuse input as output.
                                Console.WriteLine("Notice (USR MD5): the command '" + command + "' is not implemented");
                                output = input;
                                break;
                        }
                    }
                    else if (input[2] == "TWN") //tweener, msnp8+
                    {
                        switch (authCommand)
                        {
                            case "I": // USR [numb] TWN I [email]
                                output = new string[] { "USR", number, "TWN", "S", "1.0.0000", "lc=1033,id=507,tw=40,fs=1,ru=http%3A%2F%2Fmessenger%2Emsn%2Ecom,ct=1062764229,kpp=1,kv=5,ver=2.1.0173.1,tpf=43f8a4c8ed940c04e3740be46c4d1619" };
                                break;
                            case "S": // USR [numb] TWN S [useless gibberish]
                                output = new string[] { "USR", number, "OK", (string)Email, (string)Email + "(Hi!)", "1", "0" };
                                break;
                            default: // just awkwardly reuse input as output.
                                Console.WriteLine("Notice (USR TWN): the command '" + command + "' is not implemented");
                                output = input;
                                break;
                        }
                    }
                    break;
                default: // just awkwardly reuse input as output.
                    Console.WriteLine("Notice: the command '" + command + "' is not implemented");
                    output = input;
                    break;
            }

            // Send output to what should be the socket.
            outputEncoded = string.Join(" ", output) + "\r\n"; //msn uses newline
            Console.WriteLine(clientInfo);
            Console.WriteLine("Output: <<< " + outputEncoded);
            SendAsync(outputEncoded);

            if (isDisconnecting == true)
            {
                Disconnect();
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"MSNP TCP session caught an error with code {error}");
        }
    }

    class MsnpServer : TcpServer
    {
        public void dumpSession(TcpSession session)
        {
            using (var writer = new System.IO.StringWriter())
            {
                ObjectDumper.Dumper.Dump(session, "Object Dumper", writer);
                Console.Write(writer.ToString());
            }
        }

        public MsnpServer(IPAddress address, int port) : base(address, port) { }


        protected override TcpSession CreateSession() 
        {
            return new MsnpSession(this);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"MSNP TCP server caught an error with code {error}");
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

            Console.WriteLine($"MSNP server port: {port}");

            Console.WriteLine();

            // Create a new TCP chat server
            var server = new MsnpServer(IPAddress.Any, port);

            // Start the server
            Console.Write("Starting Heelercrap...");
            server.Start();

            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    server.Restart();
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