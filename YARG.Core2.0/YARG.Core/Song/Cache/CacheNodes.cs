using System;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    public sealed class CategoryCacheWriteNode
    {
        public int title;
        public int artist;
        public int album;
        public int genre;
        public int year;
        public int charter;
        public int playlist;
        public int source;
    }

    public sealed class CategoryCacheStrings
    {
        public string[] titles = Array.Empty<string>();
        public string[] artists = Array.Empty<string>();
        public string[] albums = Array.Empty<string>();
        public string[] genres = Array.Empty<string>();
        public string[] years = Array.Empty<string>();
        public string[] charters = Array.Empty<string>();
        public string[] playlists = Array.Empty<string>();
        public string[] sources = Array.Empty<string>();

        public CategoryCacheStrings(UnmanagedMemoryStream stream, bool multithreaded)
        {
            const int NUM_CATEGORIES = 8;
            if (multithreaded)
            {
                var tasks = new Task[NUM_CATEGORIES];
                for (int i = 0; i < NUM_CATEGORIES; ++i)
                {
                    int length = stream.Read<int>(Endianness.Little);
                    var slice = stream.Slice(length);

                    int strIndex = i;
                    tasks[i] = Task.Run(() => { GetArray(strIndex) = ReadStrings(slice); });
                }
                Task.WaitAll(tasks);
            }
            else
            {
                for (int i = 0; i < NUM_CATEGORIES; ++i)
                {
                    int length = stream.Read<int>(Endianness.Little);
                    var slice = stream.Slice(length);
                    GetArray(i) = ReadStrings(slice);
                }
            }

            static string[] ReadStrings(UnmanagedMemoryStream stream)
            {
                int count = stream.Read<int>(Endianness.Little);
                string[] strings = new string[count];
                for (int i = 0; i < count; ++i)
                    strings[i] = stream.ReadString();
                return strings;
            }
        }

        private ref string[] GetArray(int index)
        {
            switch(index)
            {
                case 0: return ref titles;
                case 1: return ref artists;
                case 2: return ref albums;
                case 3: return ref genres;
                case 4: return ref years;
                case 5: return ref charters;
                case 6: return ref playlists;
                case 7: return ref sources;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
