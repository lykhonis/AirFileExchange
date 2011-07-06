using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using AirFileExchange.Air;
using AirFileExchange.Client;
using System.IO;

namespace AirFileExchange.Server
{
    public class AirServer : IDisposable
    {
        private Thread identificationThread;
        private Thread discoveringThread;
        private Thread listeningFilesThread;

        public delegate void UserPresenceRequest(RequestPresence request, IPEndPointHolder remotePoint);
        public event UserPresenceRequest UserPresenceReceivedRequest;

        public delegate void UserPresenceAsk(out bool isVisible, ref UserInfo userInfo);
        public event UserPresenceAsk UserPresenceReceivedAsk;

        public delegate void UserPresenceTimeout(IPEndPointHolder remotePoint);
        public event UserPresenceTimeout UserPresenceGotTimeout;

        public delegate void UserSendsFiles(SendFiles sendFiles, IPEndPointHolder remotePoint, TcpClient client);
        public event UserSendsFiles UserWantToSendFiles;

        public delegate void UserReceiveFilesProgress(SendFile sendFile, IPEndPointHolder remotePoint, long receivedCurrent, long currentSize,
            long receivedTotal, long totalSize, object state, out bool cancel);

        public delegate void UserReceiveFilesComplete(SendFiles sendFiles, IPEndPointHolder remotePoint, object state, Exception e);

        public class IPEndPointHolder : IEquatable<IPEndPointHolder>
        {
            public const double DefaultTimeout = 60 * 1000;

            public IPEndPoint IpEndPoint { get; set; }

            public bool IsMine
            {
                get
                {
                    return Helper.IsIPLocal(IpEndPoint.Address);
                }
            }

            private bool isAvailable = false;
            public bool IsAvailable
            {
                get
                {
                    return isAvailable;
                }
                set
                {
                    if (value)
                    {
                        lastUpdate = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalMilliseconds;
                    }
                    isAvailable = value;
                }
            }

            private double lastUpdate;
            public bool IsTimeout
            {
                get
                {
                    return (TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalMilliseconds - lastUpdate) > DefaultTimeout;
                }
            }

            public IPEndPointHolder()
            {
                lastUpdate = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalMilliseconds;
            }

            #region IEquatable<IPEndPointHolder> Members

            public bool Equals(IPEndPointHolder other)
            {
                return IpEndPoint.Address.Equals(other.IpEndPoint.Address);
            }

            #endregion
        }

        private List<IPEndPointHolder> listOfAvailablePCs;
        private TcpListener tcpListener;

        public AirServer()
        {
            listOfAvailablePCs = new List<IPEndPointHolder>();
        }

        public void Start()
        {
            if (this.identificationThread != null)
                throw new InvalidOperationException("Server is already running");

            this.identificationThread = new Thread(new ThreadStart(IdentificationMe));
            this.identificationThread.Start();

            this.discoveringThread = new Thread(new ThreadStart(DiscoveringUsers));
            this.discoveringThread.Start();

            this.listeningFilesThread = new Thread(new ThreadStart(ListeningFiles));
            this.listeningFilesThread.Start();

            //AskForPresence();
        }

        public void Stop()
        {
            if (this.identificationThread == null)
                throw new InvalidOperationException("Server has been already stopped");

            this.identificationThread.Abort();
            this.identificationThread = null;

            this.discoveringThread.Abort();
            this.discoveringThread = null;

            this.tcpListener.Stop();
            this.listeningFilesThread.Abort();
            this.listeningFilesThread = null;
        }

        public void ReceiveFilesFrom(IPEndPointHolder ipEndPoint, TcpClient client, SendFiles sendFiles, bool receive, object state,
            UserReceiveFilesProgress progress)
        {
            try
            {
                byte[] bytes = new byte[1024];
                NetworkStream networkStream = client.GetStream();
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(Helper.XmlSerialize<SendFilesData>(new SendFilesData()
                    {
                        UserAddress = new UserAddress()
                        {
                            Address = Helper.LocalIPAddress(),
                            Port = AirClient.DefaultPort
                        },
                        Status = receive ? "allowed" : "denied"
                    }));
                    networkStream.Write(buffer, 0, buffer.Length);
                }

                if (!receive)
                {
                    throw new OperationCanceledException();
                }

                int i = 0;
                long n = 0;
                bool cancel = false;

                long totalSize = 0, receivedTotal = 0, receivedCurrent = 0;

                foreach (SendFile file in sendFiles.Files)
                {
                    totalSize += file.Size;
                }

                string folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\Air File Exchange\\";
                Directory.CreateDirectory(folder);

                foreach (SendFile file in sendFiles.Files)
                {
                    using (FileStream fileStream = new FileStream(folder + file.Name, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        fileStream.SetLength(0);

                        receivedCurrent = 0;

                        if (i - n > 0)
                        {
                            fileStream.Write(bytes, (int)n, i - (int)n);

                            receivedCurrent += i - (int)n;
                            receivedTotal += i - (int)n;

                            if (progress != null)
                            {
                                progress(file, ipEndPoint, receivedCurrent, file.Size, receivedTotal, totalSize, state, out cancel);
                            }
                        }

                        while (!cancel && (i = networkStream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            n = file.Size - fileStream.Length;
                            if (i <= n)
                            {
                                n = i;
                            }
                            fileStream.Write(bytes, 0, (int)n);

                            receivedCurrent += n;
                            receivedTotal += n;

                            if (progress != null)
                            {
                                progress(file, ipEndPoint, receivedCurrent, file.Size, receivedTotal, totalSize, state, out cancel);
                            }

                            if (file.Size == fileStream.Length)
                            {
                                break;
                            }
                        }
                    }
                    if (cancel)
                    {
                        throw new OperationCanceledException();
                    }
                }
                if (totalSize > receivedTotal)
                {
                    throw new OperationCanceledException();
                }
            }
            finally
            {
                client.Close();
            }
        }

        public void ReceiveFilesFromAsync(IPEndPointHolder ipEndPoint, TcpClient client, SendFiles sendFiles, bool receive, object state,
            UserReceiveFilesProgress progress, UserReceiveFilesComplete complete)
        {
            new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        ReceiveFilesFrom(ipEndPoint, client, sendFiles, receive, state, progress);
                        if (complete != null)
                        {
                            complete(sendFiles, ipEndPoint, state, null);
                        }
                    }
                    catch (Exception e)
                    {
                        if (complete != null)
                        {
                            complete(sendFiles, ipEndPoint, state, e);
                        }
                    }
                })).Start();
        }

        private void AcceptTcpClient(object param)
        {
            TcpClient client = (TcpClient)param;
            try
            {
                byte[] bytes = new byte[256];
                StringBuilder stringBuilder = new StringBuilder();
                NetworkStream networkStream = client.GetStream();

                int i;
                while ((i = networkStream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    stringBuilder.Append(Encoding.UTF8.GetString(bytes, 0, i));
                    if (i < bytes.Length)
                        break;
                }

                // Sending a list of files?
                try
                {
                    SendFiles sendFiles = Helper.XmlDeserialize<SendFiles>(stringBuilder.ToString());

                    if (UserWantToSendFiles != null)
                    {
                        AirServer.IPEndPointHolder ipEndPointHolder = new IPEndPointHolder();
                        ipEndPointHolder.IpEndPoint = new IPEndPoint(IPAddress.Parse(sendFiles.UserAddress.Address), sendFiles.UserAddress.Port);
                        ipEndPointHolder.IsAvailable = true;

                        UserWantToSendFiles(sendFiles, ipEndPointHolder, client);
                    }
                    else
                    {
                        client.Close();
                    }
                }
                catch
                {
                }
            }
            catch
            {
                client.Close();
            }
        }

        private void ListeningFiles()
        {
            tcpListener = new TcpListener(IPAddress.Any, AirClient.DefaultPort);
            tcpListener.Start();

            while (this.listeningFilesThread.IsAlive)
            {
                try
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    new Thread(new ParameterizedThreadStart(AcceptTcpClient)).Start(client);
                }
                catch
                {
                }
            }
        }

        private void FilterTimeoutUsers()
        {
            List<IPEndPointHolder> itemsToRemove = new List<IPEndPointHolder>();

            foreach (IPEndPointHolder item in listOfAvailablePCs)
            {
                if (item.IsTimeout)
                {
                    if (UserPresenceGotTimeout != null)
                    {
                        UserPresenceGotTimeout(item);
                    }

                    itemsToRemove.Add(item);
                }
            }

            foreach (IPEndPointHolder item in itemsToRemove)
            {
                listOfAvailablePCs.Remove(item);
            }
        }

        private void AskForPresence()
        {
            new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        Udp.SendBroadcast(Helper.XmlSerialize<RequestPresence>(new RequestPresence()
                        {
                            Status = "ask",
                            UserAddress = new UserAddress()
                            {
                                Address = Helper.LocalIPAddress(),
                                Port = Udp.DefaultPort
                            },
                            UserInfo = null
                        }));

                        Log.WriteLn("Send: ask - broadcast");
                    }
                    catch
                    {
                    }
                })).Start();
        }

        private void DiscoveringUsers()
        {
            Random random = new Random();

            while (this.discoveringThread.IsAlive)
            {
                try
                {
                    FilterTimeoutUsers();
                    try
                    {
                        Udp.SendBroadcast(Helper.XmlSerialize<RequestPresence>(new RequestPresence()
                        {
                            Status = "ask",
                            UserAddress = new UserAddress()
                            {
                                Address = Helper.LocalIPAddress(),
                                Port = Udp.DefaultPort
                            },
                            UserInfo = null
                        }));

                        Log.WriteLn("Send: ask - broadcast");
                    }
                    catch
                    {
                    }
                    Thread.Sleep(random.Next(8000, 14000));
                }
                catch
                {
                }
            }
        }

        private void IdentificationMe()
        {
            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;

                IPEndPoint udpBroadcastPoint = new IPEndPoint(IPAddress.Any, Udp.DefaultPort);
                udpClient.Client.Bind(udpBroadcastPoint);

                while (this.identificationThread.IsAlive)
                {
                    try
                    {
                        TimeSpan timeToWait = TimeSpan.FromMilliseconds(50);
                        IAsyncResult result = udpClient.BeginReceive(null, null);
                        result.AsyncWaitHandle.WaitOne(timeToWait);
                        try
                        {
                            IPEndPoint remotePoint = null;
                            byte[] buffer = udpClient.EndReceive(result, ref remotePoint);

                            try
                            {
                                RequestPresence requestPresence = Helper.XmlDeserialize<RequestPresence>(Encoding.UTF8.GetString(buffer));

                                Log.WriteLn("Recv: {0} - {1}", requestPresence.Status, remotePoint.Address.ToString());

                                if ("presence".Equals(requestPresence.Status))
                                {
                                    IPEndPointHolder ipEndPointHolder = listOfAvailablePCs.Find(
                                        item => item.IpEndPoint.Address.Equals(IPAddress.Parse(requestPresence.UserAddress.Address)));
                                    if (ipEndPointHolder == null)
                                    {
                                        ipEndPointHolder = new IPEndPointHolder();
                                        ipEndPointHolder.IpEndPoint = new IPEndPoint(IPAddress.Parse(requestPresence.UserAddress.Address),
                                            requestPresence.UserAddress.Port);
                                        ipEndPointHolder.IsAvailable = true;

                                        listOfAvailablePCs.Add(ipEndPointHolder);
                                        if (UserPresenceReceivedRequest != null)
                                        {
                                            UserPresenceReceivedRequest(requestPresence, ipEndPointHolder);
                                        }
                                    }
                                    else
                                    {
                                        ipEndPointHolder.IsAvailable = true;
                                    }
                                }

                                if ("ask".Equals(requestPresence.Status))
                                {
                                    UserInfo userInfo = null;
                                    bool isVisible = true;

                                    if (UserPresenceReceivedAsk != null)
                                    {
                                        UserPresenceReceivedAsk(out isVisible, ref userInfo);
                                    }

                                    if (isVisible)
                                    {
                                        Udp.Send(Helper.XmlSerialize<RequestPresence>(new RequestPresence()
                                        {
                                            Status = "presence",
                                            UserAddress = new UserAddress()
                                            {
                                                Address = Helper.LocalIPAddress(),
                                                Port = Udp.DefaultPort
                                            },
                                            UserInfo = userInfo
                                        }), new IPEndPoint(IPAddress.Parse(requestPresence.UserAddress.Address),
                                            requestPresence.UserAddress.Port));

                                        Log.WriteLn("Send: presence - {0}", requestPresence.UserAddress.Address.ToString());

                                        IPEndPointHolder ipEndPointHolder = listOfAvailablePCs.Find(
                                            item => item.IpEndPoint.Address.Equals(IPAddress.Parse(requestPresence.UserAddress.Address)));
                                        if (ipEndPointHolder == null)
                                        {
                                            Udp.Send(Helper.XmlSerialize<RequestPresence>(new RequestPresence()
                                            {
                                                Status = "ask",
                                                UserAddress = new UserAddress()
                                                {
                                                    Address = Helper.LocalIPAddress(),
                                                    Port = Udp.DefaultPort
                                                },
                                                UserInfo = null
                                            }), new IPEndPoint(IPAddress.Parse(requestPresence.UserAddress.Address),
                                                requestPresence.UserAddress.Port));

                                            Log.WriteLn("Send: ask - {0}", requestPresence.UserAddress.Address.ToString());
                                        }
                                        else
                                        {
                                            ipEndPointHolder.IsAvailable = true;
                                        }
                                    }
                                }

                                if ("left".Equals(requestPresence.Status))
                                {
                                    IPEndPointHolder ipEndPointHolder = listOfAvailablePCs.Find(
                                        item => item.IpEndPoint.Address.Equals(remotePoint.Address));
                                    if (ipEndPointHolder != null)
                                    {
                                        ipEndPointHolder.IsAvailable = false;

                                        if (UserPresenceGotTimeout != null)
                                        {
                                            UserPresenceGotTimeout(ipEndPointHolder);
                                        }

                                        listOfAvailablePCs.Remove(ipEndPointHolder);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                        catch
                        {
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        UserInfo userInfo = null;
                        bool isVisible = false;

                        if (UserPresenceReceivedAsk != null)
                        {
                            UserPresenceReceivedAsk(out isVisible, ref userInfo);
                        }

                        foreach (IPEndPointHolder ipEndPointHolder in listOfAvailablePCs)
                        {
                            Udp.Send(Helper.XmlSerialize<RequestPresence>(new RequestPresence()
                            {
                                Status = "left",
                                UserAddress = new UserAddress()
                                {
                                    Address = Helper.LocalIPAddress(),
                                    Port = Udp.DefaultPort
                                },
                                UserInfo = null
                            }), ipEndPointHolder.IpEndPoint);

                            Log.WriteLn("Send: left - {0}", ipEndPointHolder.IpEndPoint.Address.ToString());
                        }
                    }
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch
            {
            }
        }

        #endregion
    }
}
