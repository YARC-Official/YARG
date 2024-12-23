using System.IO;

namespace YARG.Core.Utility
{
    public interface IBinarySerializable
    {

        public void Serialize(BinaryWriter writer);

        public void Deserialize(BinaryReader reader, int version = 0);

    }
}