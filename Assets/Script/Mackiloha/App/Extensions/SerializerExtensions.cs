using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mackiloha.IO;

namespace Mackiloha.App.Extensions
{
    public static class SerializerExtensions
    {
        public static T ReadFromMiloObjectBytes<T>(this MiloSerializer serializer, MiloObjectBytes entry) where T : ISerializable, new()
        {
            using (var ms = new MemoryStream(entry.Data))
            {
                var obj = serializer.ReadFromStream<T>(ms);
                (obj as MiloObject).Name = entry.Name;

                return obj;
            }
        }

        public static byte[] WriteToBytes(this MiloSerializer serializer, ISerializable obj)
        {
            using (var ms = new MemoryStream())
            {
                serializer.WriteToStream(ms, obj);
                return ms.ToArray();
            }
        }
    }
}
