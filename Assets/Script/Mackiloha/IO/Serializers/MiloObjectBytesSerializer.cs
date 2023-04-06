using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.IO.Serializers
{
    public class MiloObjectBytesSerializer : AbstractSerializer
    {
        public MiloObjectBytesSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            throw new NotImplementedException();
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var bytes = data as MiloObjectBytes;

            if (bytes.Data == null)
                return;

            aw.Write(bytes.Data);
        }

        public override bool IsOfType(ISerializable data) => data is MiloObjectBytes;

        public override int Magic() => -1; // Magic is derived from byte data

        internal override int[] ValidMagics()
        {
            return Array.Empty<int>();
        }
    }
}
