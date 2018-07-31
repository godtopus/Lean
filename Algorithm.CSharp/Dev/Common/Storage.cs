using System;
using System.IO;

namespace QuantConnect.Algorithm.CSharp
{
    class Storage
    {
        public static void CreateFile(string path, object[] header, string separator = ";")
        {
            File.WriteAllText(path, string.Join(separator, header) + Environment.NewLine);
        }

        public static void AppendToFile(string path, object[] line, string separator = ";")
        {
            File.AppendAllText(path, string.Join(separator, line) + Environment.NewLine);
        }

        public static double ToUTCTimestamp(DateTime date)
        {
            return (date.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds;
        }
    }
}
