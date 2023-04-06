using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Mackiloha.IO.Serializers;
using Mackiloha.Render;

namespace Mackiloha.IO
{
    public class MiloSerializer
    {
        public readonly SystemInfo Info;
        private readonly AbstractSerializer[] Serializers;
        
        public MiloSerializer(SystemInfo info)
        {
            Info = info;

            Serializers = new AbstractSerializer[]
            {
                new AnimSerializer(this),
                new CamSerializer(this),
                new DrawSerializer(this),
                new EnvironSerializer(this),
                new FontSerializer(this),
                new HMXBitmapSerializer(this),
                new MatSerializer(this),
                new MeshSerializer(this),
                new MiloObjectBytesSerializer(this),
                new MiloObjectDirSerializer(this),
                new P9SongPrefSerializer(this),
                new PropAnimSerializer(this),
                new TexSerializer(this),
                new TransSerializer(this),
                new ViewSerializer(this)
            };
        }

        public MiloSerializer(SystemInfo info, AbstractSerializer[] serializers)
        {
            Info = info;

            // Just to be extra safe
            if (serializers == null)
                Serializers = new AbstractSerializer[0];
            else
                Serializers = serializers.Where(x => x != default(AbstractSerializer)).ToArray();
        }

        public void ReadFromFile(string path, ISerializable data)
        {
            using (var fs = File.OpenRead(path))
                ReadFromStream(fs, data);
        }

        public T ReadFromFile<T>(string path) where T : ISerializable, new()
        {
            using (var fs = File.OpenRead(path))
                return ReadFromStream<T>(fs);
        }

        public void ReadFromStream(Stream stream, ISerializable data)
        {
            var serializer = Serializers.FirstOrDefault(x => x.IsOfType(data));

            if (serializer == null)
                throw new NotImplementedException($"Deserialization of {data.GetType().Name} is not supported yet!");

            var ar = new AwesomeReader(stream, Info.BigEndian);
            serializer.ReadFromStream(ar, data);
        }

        public T ReadFromStream<T>(Stream stream) where T : ISerializable, new()
        {
            var data = new T();
            ReadFromStream(stream, data);
            return data;
        }

        internal MiloObject ReadFromStream(Stream stream, string type)
        {
            MiloObject obj;

            switch (type)
            {
                case "Tex":
                    obj = ReadFromStream<Tex>(stream);
                    break;
                default:
                    throw new NotImplementedException($"Deserialization of {type} is not supported yet!");
            }

            return obj;
        }
        
        public void WriteToFile(string path, ISerializable obj)
        {
            using (var fs = File.OpenWrite(path))
            {
                WriteToStream(fs, obj);
            }
        }

        public void WriteToStream(Stream stream, ISerializable obj)
        {
            var aw = new AwesomeWriter(stream, Info.BigEndian);

            var serializer = Serializers.FirstOrDefault(x => x.IsOfType(obj));

            if (serializer == null)
                throw new NotImplementedException($"Serialization of {obj.GetType().Name} is not supported yet!");

            serializer.WriteToStream(aw, obj);

            /*
            switch (obj)
            {
                case Tex tex:
                    WriteToStream(aw, tex);
                    break;
                case HMXBitmap bitmap:
                    WriteToStream(aw, bitmap);
                    break;
                case MiloObjectBytes bytes:
                    WriteToStream(aw, bytes);
                    break;
                default:
                    throw new NotImplementedException($"Serialization of {obj.GetType().Name} is not supported yet!");
            } */
        }
    }
}
