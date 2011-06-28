using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Windows.Media.Imaging;

namespace AirFileExchange.Air
{
    [Serializable]
    [XmlRoot("user-info")]
    public class UserInfo
    {
        [XmlAttribute("display-name")]
        public string DisplayName;

        [XmlAttribute("computer-name")]
        public string ComputerName;

        [XmlElement("icon")]
        public string Icon;
    }

    [Serializable]
    [XmlRoot("user-address")]
    public class UserAddress
    {
        [XmlAttribute("address")]
        public string Address;

        [XmlAttribute("port")]
        public int Port;
    }

    [Serializable]
    [XmlRoot("request-presence")]
    public class RequestPresence
    {
        [XmlAttribute("status")]
        public string Status;

        [XmlElement("user-address")]
        public UserAddress UserAddress;

        [XmlElement("user-info")]
        public UserInfo UserInfo;
    }

    [Serializable]
    [XmlRoot("send-file")]
    public class SendFile
    {
        [XmlAttribute("name")]
        public string Name;

        [XmlAttribute("size")]
        public long Size;
    }

    [Serializable]
    [XmlRoot("send-files")]
    public class SendFiles
    {
        [XmlAttribute("count")]
        public int Count;

        [XmlAttribute("size")]
        public long Size;

        [XmlElement("user-address")]
        public UserAddress UserAddress;

        [XmlArray("files")]
        [XmlArrayItem("file", typeof(SendFile))]
        public List<SendFile> Files = new List<SendFile>();
    }

    [Serializable]
    [XmlRoot("send-files-data")]
    public class SendFilesData
    {
        [XmlElement("user-address")]
        public UserAddress UserAddress;

        [XmlAttribute("status")]
        public string Status;
    }
}
