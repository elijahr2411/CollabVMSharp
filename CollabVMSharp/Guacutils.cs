using System.Collections.Generic;

namespace CollabVMSharp {
    /// <summary>
    /// Utilities for converting lists of strings to and from Guacamole format
    /// </summary>
    public static class Guacutils {
        /// <summary>
        /// Encode an array of strings to guacamole format
        /// </summary>
        /// <param name="msgArr">List of strings to be encoded</param>
        /// <returns>A guacamole string array containing the provided strings</returns>
        public static string Encode(params string[] msgArr) {
            string res = "";
            int i = 0;
            foreach (string s in msgArr) {
                res += s.Length.ToString();
                res += ".";
                res += s;
                if (i == msgArr.Length - 1) res += ";";
                else res += ",";
                i++;
            }
            return res;
        }
        /// <summary>
        /// Decode a guacamole string array
        /// </summary>
        /// <param name="msg">String containing a guacamole array</param>
        /// <returns>An array of strings</returns>
        public static string[] Decode(string msg) {
            List<string> outArr = new List<string>();
            int pos = 0;
            while (pos < msg.Length - 1) {
                int dotpos = msg.IndexOf('.', pos + 1);
                string lenstr = msg.Substring(pos, dotpos - pos);
                int len = int.Parse(lenstr);
                string str = msg.Substring(dotpos + 1, len);
                outArr.Add(str);
                pos = dotpos + len + 2;
            }
            return outArr.ToArray();
        }
    }
}
