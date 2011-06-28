using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Security.Principal;
using System.Windows.Media.Imaging;
using System.Net;

namespace AirFileExchange.Air
{
    public class Helper
    {
        public static T XmlDeserialize<T>(string Xml) where T : class
        {
            var xs = new XmlSerializer(typeof(T));
            var reader = new StringReader(Xml);
            return (T)xs.Deserialize(reader);
        }

        public static string XmlSerialize<T>(T Object) where T : class
        {
            var xs = new XmlSerializer(typeof(T));
            var writer = new StringWriter();
            xs.Serialize(writer, Object);
            return writer.ToString();
        }

        public static byte[] BytesFromImage(BitmapImage image)
        {
            byte[] buffer = null;
            if (image.StreamSource != null && image.StreamSource.Length > 0)
            {
                using (BinaryReader binaryReader = new BinaryReader(image.StreamSource))
                {
                    buffer = binaryReader.ReadBytes((int)image.StreamSource.Length);
                }
            }
            return buffer;
        }

        public static BitmapImage ImageFromBytes(byte[] bytes)
        {
            MemoryStream stream = new MemoryStream(bytes);
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.EndInit();
            return image;
        }

        private static string UserAccountPictureFilePath()
        {
            string tempPath = Environment.GetEnvironmentVariable("TEMP");
            string appPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string imagePath;
            if (Environment.OSVersion.Version.Major < 6)
            {
                imagePath = string.Format("{0}\\Microsoft\\User Account Pictures\\{1}.bmp", appPath, Environment.UserName);
            }
            else
            {
                imagePath = string.Format("{0}\\{1}.bmp", tempPath, Environment.UserName);
            }
            return imagePath;
        }

        public static string UserAccountPictureAsBase64()
        {
            byte[] bytes = File.ReadAllBytes(UserAccountPictureFilePath());
            return Convert.ToBase64String(bytes);
        }

        public static BitmapImage UserAccountPicture()
        {
            return ImageFromBytes(File.ReadAllBytes(UserAccountPictureFilePath()));
        }

        public static BitmapImage ImageFromBase64(string encodedBase64)
        {
            return ImageFromBytes(Convert.FromBase64String(encodedBase64));
        }

        public static string LocalIPAddress()
        {
            string localIP = string.Empty;
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily.ToString() == "InterNetwork")
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }
    }
}
