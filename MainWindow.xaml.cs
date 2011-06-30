using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AirFileExchange.Server;
using AirFileExchange.Client;
using System.Threading;
using AirFileExchange.Controls;
using System.Net;
using Microsoft.Win32;
using System.Net.Sockets;

namespace AirFileExchange
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AirServer airServer;
        private AirClient airClient;
        private bool isClosed;
        private string UserAccountPicture;

        public MainWindow()
        {
            InitializeComponent();

            isClosed = false;

            airServer = new AirServer();
            airServer.UserPresenceReceivedRequest += new AirServer.UserPresenceRequest(airServer_UserPresenceReceivedRequest);
            airServer.UserPresenceReceivedAsk += new AirServer.UserPresenceAsk(airServer_UserPresenceReceivedAsk);
            airServer.UserPresenceGotTimeout += new AirServer.UserPresenceTimeout(airServer_UserPresenceGotTimeout);
            airServer.UserWantToSendFiles += new AirServer.UserSendsFiles(airServer_UserWantToSendFiles);
            airServer.Start();

            airClient = new AirClient();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            isClosed = true;
            airServer.Dispose();
        }

        void airServer_UserPresenceGotTimeout(AirServer.IPEndPointHolder remotePoint)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirServer.UserPresenceTimeout(airServer_UserPresenceGotTimeout), new object[] { remotePoint });
                return;
            }

            foreach (UserIcon userIcon in PanelOfUsers.Children)
            {
                if (userIcon.RemotePoint.Equals(remotePoint))
                {
                    userIcon.Remove();
                    break;
                }
            }
        }

        void airServer_UserPresenceReceivedAsk(out bool isVisible, ref Air.UserInfo userInfo)
        {
            if (string.IsNullOrEmpty(UserAccountPicture))
            {
                UserAccountPicture = AirFileExchange.Air.Helper.UserAccountPictureAsBase64();
            }

            isVisible = true;
            userInfo = new AirFileExchange.Air.UserInfo()
            {
                DisplayName = Environment.UserName,
                ComputerName = Environment.MachineName,
                Icon = UserAccountPicture
            };
        }

        void airServer_UserPresenceReceivedRequest(Air.RequestPresence request, AirServer.IPEndPointHolder remotePoint)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirServer.UserPresenceRequest(airServer_UserPresenceReceivedRequest), new object[] { request, remotePoint });
                return;
            }

            if (!remotePoint.IsMine)
            {
                UserIcon userIcon = new UserIcon();
                userIcon.RemotePoint = remotePoint;
                userIcon.TextDisplayName.Text = request.UserInfo.DisplayName;
                userIcon.TextComputerName.Text = request.UserInfo.ComputerName;
                userIcon.ImageIcon.Source = AirFileExchange.Air.Helper.ImageFromBase64(request.UserInfo.Icon);
                userIcon.Click += new EventHandler(userIcon_Click);
                userIcon.DropFileList += new UserIcon.EventDropFileList(userIcon_DropFileList);
                PanelOfUsers.Children.Add(userIcon);

                userIcon.Popup();
            }
        }

        void userIcon_DropFileList(object sender, string[] fileDropList)
        {
            UserIcon userIcon = (UserIcon)sender;

            if (userIcon.IsSuspend || userIcon.ProgressValue != 0)
            {
                return;
            }

            userIcon.IsCanceled = false;
            userIcon.IsSuspend = true;
            userIcon.LastStatus = UserIcon.OperationStatus.None;

            airClient.SendFilesToAsync(userIcon.RemotePoint.IpEndPoint, fileDropList,
                new AirClient.SendFilesProgress(airClient_SendFilesProgress),
                userIcon, new AirClient.SendFilesComplete(airClient_SendFilesTo),
                new AirClient.SendFilesDenied(airClient_SendFilesDenied));
        }

        void userIcon_Click(object sender, EventArgs e)
        {
            UserIcon userIcon = (UserIcon)sender;

            if (userIcon.IsSuspend)
            {
                return;
            }

            if (userIcon.ProgressValue != 0)
            {
                if (MessageBox.Show(this, "Do you want to cancel the operation?", "AirFileExchange",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    userIcon.IsCanceled = true;
                }
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files (*.*)|*.*";
            openFileDialog.Multiselect = true;
            openFileDialog.Title = string.Format("AirFileExchange - Share files with \"{0}\"", userIcon.TextDisplayName.Text);

            if (openFileDialog.ShowDialog(this) ?? true)
            {
                userIcon.IsCanceled = false;
                userIcon.IsSuspend = true;
                userIcon.LastStatus = UserIcon.OperationStatus.None;

                airClient.SendFilesToAsync(userIcon.RemotePoint.IpEndPoint, openFileDialog.FileNames,
                    new AirClient.SendFilesProgress(airClient_SendFilesProgress),
                    userIcon, new AirClient.SendFilesComplete(airClient_SendFilesTo),
                    new AirClient.SendFilesDenied(airClient_SendFilesDenied));
            }
            else
            {
                userIcon.ProgressValue = 0;
            }
        }

        void airClient_SendFilesDenied(IPAddress ipAddress, string[] files, object state)
        {
            /*foreach (UserIcon userIcon in PanelOfUsers.Children)
            {
                if (userIcon.RemotePoint.IpEndPoint.Address.Equals(ipAddress))
                {
                    userIcon.IsSuspend = false;

                    MessageBox.Show(this, string.Format("\"{0}\" denied your request to send the files.", userIcon.TextDisplayName.Text), 
                        "AirFileExchange - Receiving files denied");
                    return;
                }
            }*/
        }

        void airClient_SendFilesProgress(string file, IPAddress ipAddress, long sentCurrent, long currentSize,
            long sentTotal, long totalSize, object state, out bool cancel)
        {
            UserIcon userIcon = (UserIcon)state;

            cancel = isClosed || userIcon.IsCanceled;


            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirClient.SendFilesProgress(airClient_SendFilesProgress), new object[] { file, ipAddress, 
                    sentCurrent, currentSize, sentTotal, totalSize, state, cancel });
                return;
            }

            userIcon.IsSuspend = false;
            userIcon.ProgressValue = 10000 * sentTotal / totalSize;
        }

        void airClient_SendFilesTo(object state, Exception e)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirClient.SendFilesComplete(airClient_SendFilesTo), new object[] { state, e });
                return;
            }

            UserIcon userIcon = (UserIcon)state;

            userIcon.IsSuspend = false;
            userIcon.ProgressValue = 0;
            userIcon.LastStatus = e == null ? UserIcon.OperationStatus.Success : UserIcon.OperationStatus.Failed;
        }

        void airServer_UserWantToSendFiles(Air.SendFiles sendFiles, AirServer.IPEndPointHolder remotePoint, TcpClient client)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirServer.UserSendsFiles(airServer_UserWantToSendFiles), new object[] { sendFiles, remotePoint, client });
                return;
            }

            foreach (UserIcon userIcon in PanelOfUsers.Children)
            {
                if (userIcon.RemotePoint.Equals(remotePoint))
                {
                    if (MessageBox.Show(this, string.Format("\"{0}\" wants to share {1} file(s) (~{2:0.00} MB) with you. Would you like to receive them now?",
                        userIcon.TextDisplayName.Text, sendFiles.Count, sendFiles.Size / 1024f), "AirFileExchange - Receiving files",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        userIcon.IsCanceled = false;
                        userIcon.LastStatus = UserIcon.OperationStatus.None;

                        airServer.ReceiveFilesFromAsync(remotePoint, client, sendFiles, true, userIcon,
                            new AirServer.UserReceiveFilesProgress(airServer_UserReceiveFilesProgress),
                            new AirServer.UserReceiveFilesComplete(airServer_UserReceiveFilesComplete));
                    }
                    else
                    {
                        airServer.ReceiveFilesFromAsync(remotePoint, client, sendFiles, false, null, null, null);
                    }
                    return;
                }
            }
        }

        void airServer_UserReceiveFilesProgress(AirFileExchange.Air.SendFile sendFile, AirServer.IPEndPointHolder remotePoint, 
            long receivedCurrent, long currentSize, long receivedTotal, long totalSize, object state, out bool cancel)
        {
            UserIcon userIcon = (UserIcon)state;

            cancel = isClosed || userIcon.IsCanceled;

            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirServer.UserReceiveFilesProgress(airServer_UserReceiveFilesProgress), new object[] { sendFile, remotePoint, 
                    receivedCurrent, currentSize, receivedTotal, totalSize, state, cancel });
                return;
            }

            userIcon.ProgressValue = 10000 * receivedTotal / totalSize;
        }

        void airServer_UserReceiveFilesComplete(AirFileExchange.Air.SendFiles sendFiles, AirServer.IPEndPointHolder remotePoint, 
            object state, Exception e)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new AirServer.UserReceiveFilesComplete(airServer_UserReceiveFilesComplete), new object[] { sendFiles, remotePoint, state, e });
                return;
            }

            UserIcon userIcon = (UserIcon)state;
            userIcon.ProgressValue = 0;
            userIcon.LastStatus = e == null ? UserIcon.OperationStatus.Success : UserIcon.OperationStatus.Failed;
        }

        private void ButtonHelp_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }
    }
}
