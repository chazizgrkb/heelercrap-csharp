using System.Net;
using System.Net.Sockets;
using NetCoreServer;

namespace HeelercrapServer
{
    class SwitchboardServer : TcpServer
    {
        public SwitchboardServer(IPAddress address, int port) : base(address, port) {}

        public SwitchboardSession msnSession = null;

        protected override TcpSession CreateSession()
        {
            var msnSession = new SwitchboardSession(this);
            return msnSession;
        }

        public List<string> GetSessions(SwitchboardServer server)
        {
            List<string> allSessions = new List<string>();
            foreach (SwitchboardSession session in server.Sessions.Values)
            {
                allSessions.Add($"[CONNECTED {session.ConnectTimeStamp}] {session.Email}:{session.SessionID}|{session.Id}");
            }
            return allSessions;
        }

        public string GetSessionByEmail(SwitchboardServer server, string email, string SessionID)
        {
            string sessionGUID = "00000000-0000-0000-0000-000000000000"; // yeah yeah should be a GUID but whatever
            var sessions = GetSessions(server);
            foreach (string line in sessions)
            {
                string session = SessionID.ToString();
                if (line.Contains(email + ":" + SessionID))
                {
                    sessionGUID = line.Substring(line.IndexOf("|") + 1); // remove the email as we only need the GUID
                    break;
                }
            }
            Console.WriteLine(sessionGUID);
            return sessionGUID;
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Switchboard TCP server caught an error with code {error}");
        }
    }
}