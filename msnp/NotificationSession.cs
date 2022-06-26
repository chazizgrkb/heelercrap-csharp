using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace HeelercrapServer
{
    class NotificationSession : TcpSession {
    
        public NotificationSession msnpSession;

        // put any client variables shit here, putting them in OnReceived will redefine them.
        // it does work with multiple sockets(?) as seen here.
        // https://cdn.discordapp.com/attachments/954065147129917441/990426981554323466/unknown.png

        public object Msnp { get; private set; } // The MSNP version. This is required due to MSNP having many different versions over it's lifespan.
        public object Email { get; private set; } // The current email of the client, inputted via the login prompt.
        public object ClientVersion { get; private set; } // The messenger version of the client
        public object SessionID { get; private set; } // The number of the session, what the fuck.
        public object ConnectTimeStamp { get; private set; } // The timestamp of the session's connection.
        public object RandomID { get; private set; } // Randomized ID.
        Common common = null;

        // dummy session data
        public NotificationSession(TcpServer server) : base(server) 
        {
            Msnp = 0;
            Email = "unknown@email.com";
            ClientVersion = "0.0.0000";
            var randomNumber = new Random();
            RandomID = randomNumber.Next(320000);
        }

        protected override void OnConnecting()
        {
            common = new Common();
            SessionID = -1;
            ConnectTimeStamp = common.UnixTimeNow();
            Console.WriteLine($"MSNP TCP session with Id {Id} connected!");

            // Send invite message
            //string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";

            //SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"MSNP TCP session with Id {Id} disconnected!");
        }

        protected string GetSession(string email, string sid)
        {
            var server = (MsnpServer)Server; // required for GetSessionByEmail.
            string id = server.GetSessionByEmail(server, email, sid);
            return id;
        }

        void SendClient(string email, string session, string[] command)
        {
            var server = (MsnpServer)Server; // required for GetSessionByEmail.
            Guid clientGuid = Guid.Empty;
            string inputEncoded = null;
            string id = server.GetSessionByEmail(server, email, session);
            if (id == "00000000-0000-0000-0000-000000000000")
            {
                Console.WriteLine("Error: GUID is invalid. Email: " + email);
            }
            else if (Guid.TryParse(id, out clientGuid))
            {
                TcpSession clientSession = server.FindSession(clientGuid);
                string[] input = command;
                inputEncoded = string.Join(" ", input) + "\r\n"; //msn uses newline
                if (clientSession.SendAsync(inputEncoded))
                {
                    Console.WriteLine("Input (requested by another client, to " + clientGuid + "): >>> " + inputEncoded);
                }
            }
            else
            {
                Console.WriteLine("The string cannot be converted into a GUID.");
            }
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            if (IsConnected == false)
            {
                Console.WriteLine("WOAH too early");
            }
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size).Trim('\r', '\n'); // MSNP adds newlines, so trim that.
            string clientInfo = "[NOTIFICATION] Info: " + Socket.RemoteEndPoint + ", MSNP" + Msnp + ", (Client version: " + ClientVersion + "), " + Email + ", " + Id; // info of the client
            Console.WriteLine(clientInfo);
            Console.WriteLine("Input: >>> " + message);

            SwitchboardClient sbClient;

            string[] input = message.Split(" "); // split client's input.
            string[] output = null;
            string command = input[0]; // the name of the command
            string number = null;
            bool isDisconnecting = false;
            bool oneLineOutput = true;
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
                case "OUT": // Disconnection, pretty simple, right?
                    output = new string[] { "OUT" };
                    isDisconnecting = true;
                    break;
                case "XFR": // Redirection?
                    string type = input[2];
                    switch (type) 
                    {
                        case "SB": // XFR [numb] SB [address] CKI [auth_string]
                            output = new string[] { "XFR", number, "SB", "127.0.0.2:1863", "CKI", "17262740.1050826919.32308" };
                            sbClient = new SwitchboardClient("127.0.0.2", 1863);
                            DoTheSwitchboardCrap(sbClient);
                            break;
                    }
                    break;
                case "PNG": // Ping
                    output = new string[] { "QNG" };
                    break;
                case "VER": // VER [numb] MSNP8 CVR0
                            // (some clients, like msn 4, report multiple MSNP versions. however, use the 1st listed one)
                    SessionID = 0;
                    Msnp = Int32.Parse(input[2].Remove(0,4));
                    output = new string[] { "VER", number, "MSNP" + Msnp, "CVR0" };
                    break;
                case "INF": // Old authentication check, return MD5.
                    output = new string[] { "INF", number, "MD5" };
                    break;
                case "CVR": // CVR [numb] 0x0409 win 4.10 i386 MSNMSGR 5.0.0544 MSMSGS example@passport.com
                    ClientVersion = input[7];
                    if (Convert.ToInt32(Msnp) >= 8) //MSNP8 and above added the email parameter, likely because of the new auth process.
                    {
                        Email = input[9];
                    }
                    output = new string[] { "CVR", number, (string)ClientVersion, (string)ClientVersion, "1.0.0000", "http://updatelink/", "http://website/" };
                    break;
                case "USR": // authentication commands
                    string authCommand = input[3];
                    /* if (input[2].Contains("@")) //switchboard connection, pretty dumb.
                    {
                        Email = input[2];
                        SessionID = input[3];
                        output = new string[] { "USR", number, "OK", input[2], "FriendlyName" }; // placeholder
                    } */
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
                                Console.WriteLine("Notice (USR MD5): the command '" + authCommand + "' is not implemented");
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
                                Console.WriteLine("Notice (USR TWN): the command '" + authCommand + "' is not implemented");
                                output = input;
                                break;
                        }
                    }
                    break;
                case "ADD": // Add contact.
                    switch (input[2])
                    {
                        case "AL": // USR [numb] MD5 I [email]
                            output = new string[] { "ADD", number, "AL", "1", input[3], input[4] }; // placeholder
                            SendClient(input[3], "0", new string[] { "ADD", "0", "RL", "1", (string)Email, "Username" }); // sends invite to user if logged in
                            break;
                        case "FL": // USR [numb] MD5 I [email]
                            output = new string[] { "ADD", number, "FL", "1", input[3], input[4], input[5] }; // placeholder
                            break;
                        default: // just awkwardly reuse input as output.
                            Console.WriteLine("Notice (ADD): the command '" + input[2] + "' is not implemented");
                            output = input;
                            break;
                    }
                    break;
                 /*case "CAL": // Invite someone to SB session.
                    SendClient(input[2], SessionID.ToString(), new string[] { "RNG", RandomID.ToString(), "127.0.0.1:1863", "CKI", "849102291.520491932", (string)Email, "UserName" });
                    string sessionGUID_1 = GetSession(input[2], RandomID.ToString());
                    string sessionGUID_2 = GetSession((string)Email, RandomID.ToString());
                    if (sessionGUID_1.Equals("00000000-0000-0000-0000-000000000000") || sessionGUID_2.Equals("00000000-0000-0000-0000-000000000000"))
                    {
                        output = new string[] { "217", number }; // account is non-existent/offline
                    }
                    else
                    {
                        output = new string[] { "CAL", number, "RINGING", RandomID.ToString() }; // placeholder
                    }
                    break;*/
                case "ANS": // Accept invite to a SB session.
                    oneLineOutput = false;
                    output = new string[] { "IRO", "1", "1", "2", Email.ToString(), "Bluey" };
                    string[]output2 = new string[] { "ANS", "1", "\"OK\"" }; // placeholder
                    outputEncoded = string.Join(" ", output) + "\r\n" + string.Join(" ", output2) + "\r\n";
                    break;
                default: // just awkwardly reuse input as output.
                    oneLineOutput = true;
                    Console.WriteLine("NT Notice: the command '" + command + "' is not implemented");
                    output = input;
                    break;
            }

            // Send output to what should be the socket.
            if (oneLineOutput == true)
            {
                outputEncoded = string.Join(" ", output) + "\r\n"; //msn uses newline
            }
            Console.WriteLine(clientInfo);
            Console.WriteLine("Output: <<< " + outputEncoded);
            SendAsync(outputEncoded);

            if (isDisconnecting == true)
            {
                Disconnect();
            }
        }
        protected void DoTheSwitchboardCrap(SwitchboardClient sbClient)
        {
            sbClient.ConnectAsync();
            string[]inviteStuff = new string[] { "ANS", "1", Email.ToString(), "CKI", "849102291.520491932", RandomID.ToString() };
            string inviteStuffEncoded = string.Join(" ", inviteStuff) + "\r\n";
            Console.WriteLine("Output: <<< " + inviteStuffEncoded);
            sbClient.SendAsync(inviteStuffEncoded);
            sbClient.DisconnectAsync();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"MSNP TCP session caught an error with code {error}");
        }
    }
}