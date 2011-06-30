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
using System.Net;
using System.Windows.Media.Animation;
using AirFileExchange.Server;
using System.Collections.Specialized;

namespace AirFileExchange.Controls
{
    /// <summary>
    /// Interaction logic for UserIcon.xaml
    /// </summary>
    public partial class UserIcon : UserControl
    {
        public AirServer.IPEndPointHolder RemotePoint { get; set; }
        public bool IsCanceled;

        public event EventHandler Click;

        public delegate void EventDropFileList(object sender, string[] fileDropList);
        public event EventDropFileList DropFileList;

        public UserIcon()
        {
            InitializeComponent();

            (Resources["HideStoryboard"] as Storyboard).Completed += new EventHandler(UserIcon_Completed);
        }

        public void Popup()
        {
            (Resources["PopupStoryboard"] as Storyboard).Begin();
        }

        public void Remove()
        {
            (Resources["HideStoryboard"] as Storyboard).Begin();
        }

        void UserIcon_Completed(object sender, EventArgs e)
        {
            if (Parent is WrapPanel)
            {
                (Parent as WrapPanel).Children.Remove(this);
            }
        }

        public bool IsSuspend
        {
            get
            {
                return ImageIcon.Opacity != 1;
            }
            set
            {
                ImageIcon.Opacity = value ? 0.6 : 1;
            }
        }

        private void UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            border.Visibility = Visibility.Visible;
            Mouse.Capture(this, CaptureMode.Element);
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            border.Visibility = Visibility.Collapsed;
            Mouse.Capture(this, CaptureMode.None);

            if (IsMouseOver && Click != null)
            {
                Click(this, new EventArgs());
            }
        }

        public double ProgressValue
        {
            get
            {
                return Progress.Value;
            }
            set
            {
                Progress.Value = value;

                if (Progress.Value == 0)
                {
                    if (Progress.Visibility == Visibility.Visible)
                    {
                        (Resources["ProgressHideStoryboard"] as Storyboard).Begin();
                    }
                }
                else
                {
                    if (Progress.Visibility != Visibility.Visible)
                    {
                        (Resources["ProgressShowStoryboard"] as Storyboard).Begin();
                    }
                }
            }
        }

        public enum OperationStatus { None, Success, Failed }

        private OperationStatus lastStatus = OperationStatus.None;
        public OperationStatus LastStatus
        {
            get
            {
                return lastStatus;
            }
            set
            {
                lastStatus = value;
                switch (lastStatus)
                {
                    case OperationStatus.None:
                        (Resources["ImageStatusPopupStoryboard"] as Storyboard).Stop();
                        ImageStatus.Visibility = Visibility.Collapsed;
                        ImageStatus.Source = null;
                        break;

                    case OperationStatus.Failed:
                        ImageStatus.Source = new BitmapImage(new Uri("../Images/icon-failed.png", UriKind.Relative));
                        (Resources["ImageStatusPopupStoryboard"] as Storyboard).Begin();
                        break;

                    case OperationStatus.Success:
                        ImageStatus.Source = new BitmapImage(new Uri("../Images/icon-success.png", UriKind.Relative));
                        (Resources["ImageStatusPopupStoryboard"] as Storyboard).Begin();
                        break;
                }
            }
        }

        private void userControl_DragEnter(object sender, DragEventArgs e)
        {
            DataObject dataObject = (DataObject)e.Data;
            if (dataObject.ContainsFileDropList())
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Handled = false;
                e.Effects = DragDropEffects.None;
            }
        }

        private void userControl_DragOver(object sender, DragEventArgs e)
        {
            DataObject dataObject = (DataObject)e.Data;
            if (dataObject.ContainsFileDropList())
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Handled = false;
                e.Effects = DragDropEffects.None;
            }
        }

        private void userControl_DragLeave(object sender, DragEventArgs e)
        {
            DataObject dataObject = (DataObject)e.Data;
            if (dataObject.ContainsFileDropList())
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Handled = false;
                e.Effects = DragDropEffects.None;
            }
        }

        private void userControl_Drop(object sender, DragEventArgs e)
        {
            DataObject dataObject = (DataObject)e.Data;
            if (dataObject.ContainsFileDropList())
            {
                e.Handled = true;
                e.Effects = DragDropEffects.Copy;

                StringCollection fileDropList = dataObject.GetFileDropList();
                if (DropFileList != null)
                {
                    string[] list = new string[fileDropList.Count];
                    fileDropList.CopyTo(list, 0);
                    DropFileList(this, list);
                }
            }
            else
            {
                e.Handled = false;
                e.Effects = DragDropEffects.None;
            }
        }
    }
}
