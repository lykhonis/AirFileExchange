using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Threading;

namespace AirFileExchange
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex onlyOneCopyMutex;

        public App()
        {
            Startup += new StartupEventHandler(App_Startup);
        }

        void App_Startup(object sender, StartupEventArgs e)
        {
            bool isNewInstance = false;
            onlyOneCopyMutex = new Mutex(true, "AirFileExchange", out isNewInstance);
            if (isNewInstance)
            {
                MainWindow = new MainWindow();
                MainWindow.Show();
            }
            else
            {
                App.Current.Shutdown();
            }
        }
    }
}
