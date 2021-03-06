﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Net;

namespace AirFileExchange.Air
{
    public class Udp
    {
        public const int DefaultPort = 3000;

        public static void Send(string message, IPEndPoint udpPoint)
        {
            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;

                byte[] buffer = Encoding.UTF8.GetBytes(message);
                udpClient.Send(buffer, buffer.Length, udpPoint);
            }
        }

        public static void SendBroadcast(string message, int port = DefaultPort)
        {
            Send(message, new IPEndPoint(IPAddress.Broadcast, port));
        }
    }
}
