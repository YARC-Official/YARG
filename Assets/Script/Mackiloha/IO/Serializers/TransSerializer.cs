using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mackiloha.Render;

namespace Mackiloha.IO.Serializers
{
    public class TransSerializer : AbstractSerializer
    {
        public TransSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }
        
        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var trans = data as Trans;
            int version = ReadMagic(ar, data);

            // TODO: Remove/refactor. Hack until serializer code is refactored for inheritance
            if (trans is TransStandalone)
            {
                var meta = ReadMeta(ar);
            }

            trans.Mat1 = ReadMatrix(ar);
            trans.Mat2 = ReadMatrix(ar);
            
            if (version <= 8)
            {
                var transformableCount = ar.ReadInt32();
                trans.Transformables.Clear();
                trans.Transformables.AddRange(RepeatFor(transformableCount, () => ar.ReadString()));
            }

            trans.UnknownInt = ar.ReadInt32();
            switch (trans.UnknownInt)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 5:
                case 7:
                case 6:
                case 8:
                    break;
                default:
                    throw new Exception($"Unexpected number, got {trans.UnknownInt}");
            }
            
            trans.Camera = ar.ReadString();
            trans.UnknownBool = ar.ReadBoolean();

            trans.Transform = ar.ReadString();
        }

        protected static Matrix4 ReadMatrix(AwesomeReader ar)
        {
            return new Matrix4()
            {
                M11 = ar.ReadSingle(), // M11
                M12 = ar.ReadSingle(), // M12
                M13 = ar.ReadSingle(), // M13
                
                M21 = ar.ReadSingle(), // M21
                M22 = ar.ReadSingle(), // M22
                M23 = ar.ReadSingle(), // M23
                
                M31 = ar.ReadSingle(), // M31
                M32 = ar.ReadSingle(), // M32
                M33 = ar.ReadSingle(), // M33
                
                M41 = ar.ReadSingle(), // M41
                M42 = ar.ReadSingle(), // M42
                M43 = ar.ReadSingle(), // M43
                M44 = 1.0f             // M44 - Implicit
            };
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var trans = data as Trans;

            // TODO: Add version check
            var version = Magic();
            aw.Write(version);
            
            WriteMatrix(trans.Mat1, aw);
            WriteMatrix(trans.Mat2, aw);

            aw.Write((int)trans.Transformables.Count);
            trans.Transformables.ForEach(x => aw.Write((string)x));

            aw.Write((int)trans.UnknownInt);
            aw.Write((string)trans.Camera);
            aw.Write((bool)trans.UnknownBool);

            aw.Write((string)trans.Transform);
        }

        protected static void WriteMatrix(Matrix4 mat, AwesomeWriter aw)
        {
            aw.Write((float)mat.M11);
            aw.Write((float)mat.M12);
            aw.Write((float)mat.M13);

            aw.Write((float)mat.M21);
            aw.Write((float)mat.M22);
            aw.Write((float)mat.M23);

            aw.Write((float)mat.M31);
            aw.Write((float)mat.M32);
            aw.Write((float)mat.M33);

            aw.Write((float)mat.M41);
            aw.Write((float)mat.M42);
            aw.Write((float)mat.M43);
        }

        public override bool IsOfType(ISerializable data) => data is Trans;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return 8;
                case 24:
                    // GH2
                    return 9;
                default:
                    return -1;
            }
        }

        internal override int[] ValidMagics()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 10:
                    // GH1
                    return new[] { 8 };
                case 24:
                    // GH2
                    return new[] { 9 };
                case 25:
                    // TBRB
                    return new[] { 9 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
