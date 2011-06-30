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
using System.Windows.Shapes;
using System.Threading;

namespace AirFileExchange
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        private bool isAboutToDownloadNewVersion = false;
        private Update.UpdateInfo newVersionInfo;

        public AboutWindow()
        {
            InitializeComponent();

            ButtonUpdate.IsEnabled = false;
            ButtonUpdate.Content = "Checking for updates...";
            Update.IsAvailableAsync(null, new Update.AvailableComplete(update_AvailableComplete));
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("http://vladli.com");
            }
            catch
            {
            }
        }

        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (isAboutToDownloadNewVersion)
            {
                ButtonUpdate.IsEnabled = false;
                ButtonUpdate.Content = "Downloading...";
                Update.DownloadAsync(newVersionInfo, null, new Update.DownloadComplete(update_DownloadComplete));
            }
            else
            {
                ButtonUpdate.IsEnabled = false;
                ButtonUpdate.Content = "Checking for updates...";
                Update.IsAvailableAsync(null, new Update.AvailableComplete(update_AvailableComplete));
            }
        }

        void update_DownloadComplete(string fileName, object state, Exception e)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new Update.DownloadComplete(update_DownloadComplete), new object[] { fileName, state, e });
                return;
            }

            ButtonUpdate.IsEnabled = true;
            ButtonUpdate.Content = "Check for updates";

            if (e == null)
            {
                try
                {
                    System.Diagnostics.Process.Start(fileName);
                    App.Current.Shutdown();
                }
                catch
                {
                }
            }
        }

        void update_AvailableComplete(bool isAvailable, Update.UpdateInfo updateInfo, object state, Exception e)
        {
            if (Dispatcher.Thread != Thread.CurrentThread)
            {
                Dispatcher.BeginInvoke(new Update.AvailableComplete(update_AvailableComplete), new object[] { isAvailable, updateInfo, state, e });
                return;
            }

            ButtonUpdate.IsEnabled = true;

            if (isAvailable)
            {
                ButtonUpdate.Content = "Install new version";
                isAboutToDownloadNewVersion = true;
                newVersionInfo = updateInfo;
            }
            else
            {
                ButtonUpdate.Content = "Check for updates";
            }
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
