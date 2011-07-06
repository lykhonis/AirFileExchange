using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using AirFileExchange.Air;
using System.Threading;
using System.IO;

namespace AirFileExchange.Client
{
    public class AirClient
    {
        public class AirDeniedException : Exception
        {
            public AirDeniedException()
            {
            }

            public AirDeniedException(string message) : base(message)
            {
            }
        }

        public const int DefaultPort = 3001;

        public delegate void SendFilesProgress(string file, IPAddress ipAddress, long sentCurrent, long currentSize,
            long sentTotal, long totalSize, object state, out bool cancel);

        public AirClient()
        {
        }

        public void SendFilesTo(IPAddress ipAddress, string[] files, object state, SendFilesProgress progress)
        {
            SendFiles sendFiles = new SendFiles();
            sendFiles.Count = files.Length;
            sendFiles.UserAddress = new UserAddress() { Address = Helper.LocalIPAddress(), Port = DefaultPort };
            sendFiles.Size = 0;

            long totalSize = 0, sentTotal = 0, sentCurrent = 0;

            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                sendFiles.Files.Add(new SendFile()
                {
                    Name = fileInfo.Name,
                    Size = fileInfo.Length
                });

                sendFiles.Size += fileInfo.Length / 1024;
                totalSize += fileInfo.Length;
            }

            using (TcpClient tcpClient = new TcpClient())
            {
                tcpClient.Connect(ipAddress, DefaultPort);

                byte[] buffer = Encoding.UTF8.GetBytes(Helper.XmlSerialize<SendFiles>(sendFiles));

                NetworkStream networkStream = tcpClient.GetStream();
                networkStream.Write(buffer, 0, buffer.Length);

                try
                {
                    byte[] bytes = new byte[1024];
                    StringBuilder stringBuilder = new StringBuilder();

                    int i;
                    while ((i = networkStream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        stringBuilder.Append(Encoding.UTF8.GetString(bytes, 0, i));
                        if (i < bytes.Length)
                            break;
                    }

                    // Sending files?
                    try
                    {
                        SendFilesData sendFilesData = Helper.XmlDeserialize<SendFilesData>(stringBuilder.ToString());

                        if ("allowed".Equals(sendFilesData.Status))
                        {
                            bool cancel = false;

                            foreach (string file in files)
                            {
                                // not open a file from user's desktop directory ???
                                using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                                {
                                    sentCurrent = 0;

                                    while (!cancel && (i = fileStream.Read(bytes, 0, bytes.Length)) != 0)
                                    {
                                        networkStream.Write(bytes, 0, i);

                                        sentCurrent += i;
                                        sentTotal += i;

                                        if (progress != null)
                                        {
                                            progress(file, ipAddress, sentCurrent, fileStream.Length, sentTotal, totalSize, state, out cancel);
                                        }
                                    }
                                }
                                if (cancel)
                                {
                                    throw new OperationCanceledException();
                                }
                            }
                        }
                        else
                        {
                            throw new AirDeniedException();
                        }
                    }
                    finally
                    {
                    }
                }
                finally
                {
                }
            }
        }

        public delegate void SendFilesComplete(object state, Exception e);
        public void SendFilesToAsync(IPEndPoint ipEndPoint, string[] files, SendFilesProgress progress, object state,
            SendFilesComplete complete)
        {
            new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        SendFilesTo(ipEndPoint.Address, files, state, progress);
                        if (complete != null)
                        {
                            complete(state, null);
                        }
                    }
                    catch (Exception e)
                    {
                        if (complete != null)
                        {
                            complete(state, e);
                        }
                    }
                })).Start();
        }
    }
}
