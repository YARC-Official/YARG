using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concentus.Oggfile
{
    /// <summary>
    /// A set of tags that are read from / written to an Opus ogg file
    /// </summary>
    public class OpusTags
    {
        private string _comment = string.Empty;
        private IDictionary<string, string> _fields = new Dictionary<string, string>();

        public OpusTags()
        {
        }

        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                _comment = value;
            }
        }

        public IDictionary<string, string> Fields
        {
            get
            {
                return _fields;
            }
        }

        internal static OpusTags ParsePacket(byte[] packet, int packetLength)
        {
            if (packetLength < 8)
                return null;

            if (!"OpusTags".Equals(Encoding.UTF8.GetString(packet, 0, 8)))
                return null;

            OpusTags returnVal = new OpusTags();
            int cursor = 8;
            int nextFieldLength = BitConverter.ToInt32(packet, cursor);
            cursor += 4;
            if (nextFieldLength > 0)
            {
                returnVal._comment = Encoding.UTF8.GetString(packet, cursor, nextFieldLength);
                cursor += nextFieldLength;
            }

            int numTags = BitConverter.ToInt32(packet, cursor);
            cursor += 4;
            for (int c = 0; c < numTags; c++)
            {
                nextFieldLength = BitConverter.ToInt32(packet, cursor);
                cursor += 4;
                if (nextFieldLength > 0)
                {
                    string tag = Encoding.UTF8.GetString(packet, cursor, nextFieldLength);
                    cursor += nextFieldLength;
                    int eq = tag.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = tag.Substring(0, eq);
                        string val = tag.Substring(eq + 1);
                        returnVal._fields[key] = val;
                    }
                }
            }

            return returnVal;
        }
    }
}
