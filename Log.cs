using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AirFileExchange
{
    public class Log
    {
        private static string LogPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\log.txt";

        public static void WriteLn(string message)
        {
            /*using (StreamWriter streamWriter = File.AppendText(LogPath))
            {
                streamWriter.WriteLine(message);
            }*/
        }

        public static void WriteLn(string format, params object[] arg)
        {
            WriteLn(string.Format(format, arg));
        }
    }
}
