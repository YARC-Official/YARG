using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mackiloha.DTB;

namespace Mackiloha.IO
{
    public abstract class AbstractSerializer
    {
        protected readonly MiloSerializer MiloSerializer;

        public AbstractSerializer(MiloSerializer miloSerializer)
        {
            MiloSerializer = miloSerializer;
        }

        public int ReadMagic(AwesomeReader ar, ISerializable data)
        {
            var magics = ValidMagics();

            if (magics.Length <= 0)
                throw new NotImplementedException($"{GetType().Name}: Deserialization of {data.GetType().Name} for serializer version 0x{MiloSerializer.Info.Version:X2} is not implemented yet");
            
            int version = ar.ReadInt32();
            if (!magics.Any(x => x == version))
            {
                var magicVersions = string.Join(", ", magics.Select(x => $"0x{x:X2}"));
                throw new NotSupportedException($"{GetType().Name}: Magic 0x{version:X2} does not correspond to serializer version 0x{MiloSerializer.Info.Version:X2} (Expected {magicVersions})");
            }

            return version;
        }

        public MiloMeta ReadMeta(AwesomeReader ar)
        {
            var meta = new MiloMeta();

            if (MiloSerializer.Info.Version <= 10)
                return meta;

            meta.Revision = ar.ReadInt32();
            meta.ScriptName = ar.ReadString();

            var hasDtb = ar.ReadBoolean();
            if (hasDtb)
            {
                ar.BaseStream.Position -= 1;
                meta.Script = DTBFile.FromStream(ar, DTBEncoding.Classic); // TODO: Support encodings for post-RB3 and pre-GH1 games?
            }

            if (MiloSerializer.Info.Platform != Platform.PS2)
            {
                // Extra meta
                meta.Comments = ar.ReadString();
            }

            return meta;
        }

        protected static void RepeatFor(int count, Action readItem)
        {
            for (int i = 0; i < count; i++)
                readItem();
        }

        protected static IEnumerable<T> RepeatFor<T>(int count, Func<T> readItem)
        {
            for (int i = 0; i < count; i++)
                yield return readItem();
        }

        public abstract void ReadFromStream(AwesomeReader ar, ISerializable data);

        public abstract void WriteToStream(AwesomeWriter aw, ISerializable data);

        public abstract bool IsOfType(ISerializable data);

        public abstract int Magic();
        internal abstract int[] ValidMagics();
    }
}
