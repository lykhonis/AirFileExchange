using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Net;
using System.IO;
using AirFileExchange.Air;
using System.Threading;

namespace AirFileExchange
{
    public class Update
    {
        [Serializable]
        [XmlRoot("update-info")]
        public class UpdateInfo
        {
            [XmlAttribute("version")]
            public string Version;

            [XmlAttribute("date-release")]
            public double DateRelease;

            [XmlAttribute("url")]
            public string Url;

            [XmlAttribute("name")]
            public string Name;
        }

        private const string HttpUpdateUrl = "http://vladli.com/afe/update/";
        private const string CurrentVersion = "1.0.0.0";

        private static int CompareVersion(string first, string second)
        {
            int compare;
            string[] first0 = first.Split('.');
            string[] second0 = second.Split('.');

            if (first0.Length != 4 || second0.Length != 4)
            {
                throw new ArgumentException();
            }

            for (int i = 0; i < 4; i++)
            {
                if ((compare = Convert.ToInt32(first0[i]).CompareTo(Convert.ToInt32(second0[i]))) != 0)
                {
                    return compare;
                }
            }
            return 0;
        }

        public static void Download(UpdateInfo updateInfo, out string fileName)
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\";
            Directory.CreateDirectory(folder);
            fileName = folder + updateInfo.Name;

            using (WebClient webClient = new WebClient())
            {
                webClient.DownloadFile(updateInfo.Url, fileName);
            }
        }

        public delegate void DownloadComplete(string fileName, object state, Exception e);
        public static void DownloadAsync(UpdateInfo updateInfo, object state, DownloadComplete complete)
        {
            new Thread(new ThreadStart(() =>
            {
                try
                {
                    string fileName;
                    Download(updateInfo, out fileName);
                    if (complete != null)
                    {
                        complete(fileName, state, null);
                    }
                }
                catch (Exception e)
                {
                    if (complete != null)
                    {
                        complete(null, state, e);
                    }
                }
            })).Start();
        }

        public static bool IsAvailable(out UpdateInfo updateInfo)
        {
            System.Net.ServicePointManager.Expect100Continue = false;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(HttpUpdateUrl);
            request.Method = "GET";

            string responseString;
            using (WebResponse response = request.GetResponse())
            {
                using (StreamReader responseReader = new StreamReader(response.GetResponseStream()))
                {
                    responseString = responseReader.ReadToEnd();
                }
            }

            updateInfo = Helper.XmlDeserialize<UpdateInfo>(responseString);
            return CompareVersion(updateInfo.Version, CurrentVersion) > 0;
        }

        public delegate void AvailableComplete(bool isAvailable, UpdateInfo updateInfo, object state, Exception e);
        public static void IsAvailableAsync(object state, AvailableComplete complete)
        {
            new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        UpdateInfo updateInfo;
                        bool isAvailable = IsAvailable(out updateInfo);
                        if (complete != null)
                        {
                            complete(isAvailable, updateInfo, state, null);
                        }
                    }
                    catch (Exception e)
                    {
                        if (complete != null)
                        {
                            complete(false, null, state, e);
                        }
                    }
                })).Start();
        }
    }
}
