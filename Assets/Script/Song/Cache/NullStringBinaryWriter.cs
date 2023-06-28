using System.IO;

namespace YARG.Song
{
    public class NullStringBinaryWriter : BinaryWriter
    {
        public NullStringBinaryWriter(Stream output) : base(output)
        {
        }

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                value = string.Empty;
            }

            base.Write(value);
        }
    }
}