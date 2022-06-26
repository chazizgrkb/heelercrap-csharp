using System.Net.Sockets;
using System.Text;
using NetCoreServer;
using TcpClient = NetCoreServer.TcpClient;

namespace HeelercrapServer
{
    class SwitchboardSession : TcpSession
    {
        //public object Msnp { get; private set; } // The MSNP version. This is required due to MSNP having many different versions over it's lifespan.
        public object Email { get; private set; } // The current email of the client, inputted via the login prompt.
        //public object ClientVersion { get; private set; } // The messenger version of the client
        public object SessionID { get; private set; } // The number of the session, what the fuck.
        public object ConnectTimeStamp { get; private set; } // The timestamp of the session's connection.
        public object RandomID { get; private set; } // Randomized ID.
        Common common = null;

        protected string GetSession(string email, string sid)
        {
            var server = (SwitchboardServer)Server; // required for GetSessionByEmail.
            string id = server.GetSessionByEmail(server, email, sid);
            return id;
        }

        public SwitchboardSession(TcpServer server) : base(server) 
        {
            Email = "unknown@email.com";
            var randomNumber = new Random();
            SessionID = 1337;
            RandomID = 1337;
        }

        protected override void OnConnecting()
        {
            common = new Common();
            Console.WriteLine($"MSNP Switchboard TCP session with Id {Id} connected!");
            ConnectTimeStamp = common.UnixTimeNow();
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"MSNP Switchboard TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size).Trim('\r', '\n');;
            string clientInfo = "[SWITCHBOARD] Info: " + Socket.RemoteEndPoint + ", " + Email + ", " + Id; // info of the client
            Console.WriteLine(clientInfo);
            Console.WriteLine("Input: >>> " + message);

            NotificationClient client;

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
            string[] inviteStuff = null;
            string inviteStuffEncoded = null;

            switch (command)
            {
                case "OUT": // Disconnection, pretty simple, right?
                    output = new string[] { "OUT" };
                    isDisconnecting = true;
                    break;
                case "USR":
                    Email = input[2];
                    output = new string[] { "USR", number, "OK", input[2], "FriendlyName" }; // placeholder
                    break;
                case "CAL": // Invite someone to SB session.
                    client = new NotificationClient("127.0.0.1", 1863); // half-ass attempt at connecting to MSNP notification LOL
                    client.ConnectAsync();
                    inviteStuff = new string[] { "RNG", RandomID.ToString(), "127.0.0.2:1863", "CKI", "849102291.520491932", (string)Email, "UserName" };
                    inviteStuffEncoded = string.Join(" ", inviteStuff) + "\r\n";
                    client.SendAsync(inviteStuffEncoded);
              /*    string sessionGUID_1 = GetSession(input[2], RandomID.ToString());
                    string sessionGUID_2 = GetSession((string)Email, RandomID.ToString());
                    if (sessionGUID_1.Equals("00000000-0000-0000-0000-000000000000") || sessionGUID_2.Equals("00000000-0000-0000-0000-000000000000"))
                    {
                        output = new string[] { "217", number }; // account is non-existent/offline
                    }
                    else
                    {
                        output = new string[] { "CAL", number, "RINGING", RandomID.ToString() }; // placeholder
                    } */
                    output = new string[] { "CAL", number, "RINGING", RandomID.ToString() }; // placeholder
                    client.DisconnectAsync();
                    break;
                case "ANS": // Accept invite to a SB session.
                    Email = input[2];
                    output = new string[] { "ANS", number, "\"OK\"" }; // placeholder
                    break;
                default: // just awkwardly reuse input as output.
                    Console.WriteLine("SB Notice: the command '" + command + "' is not implemented");
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
            Console.WriteLine($"Switchboard TCP session caught an error with code {error}");
        }
    }
}