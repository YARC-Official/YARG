using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mackiloha
{
    public class MiloObjectDir : MiloObject, IEnumerable<MiloObject>
    {
        private string _name;

        public override string Name
        {
            get => _name ?? GetDirectoryEntry()?.Name;
            set => _name = value;
        }

        public List<MiloObject> Entries { get; } = new List<MiloObject>();

        public MiloObject this[int idx] => Entries[idx];
        public MiloObject this[string name] => name != null ? Entries.FirstOrDefault(x => name.Equals(x.Name, StringComparison.CurrentCultureIgnoreCase)) : null;
        
        public T Find<T>(string name) where T : MiloObject => name != null ? Entries.Where(x => x is T).Select(x => x as T).FirstOrDefault(x => name.Equals(x.Name, StringComparison.CurrentCultureIgnoreCase)) : default(T);
        public List<T> Find<T>() where T : MiloObject => Entries.Where(x => x is T).Select(x => x as T).OrderBy(x => x.Name).ToList();
        public MiloObject Find(string name) => Find<MiloObject>(name);

        public List<MiloObject> FilterByType(string type) => type != null ? Entries.Where(x => type.Equals(x.Type, StringComparison.CurrentCultureIgnoreCase)).OrderBy(x => (string)x.Name).ToList() : new List<MiloObject>();

        public IEnumerator<MiloObject> GetEnumerator() => Entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Entries.GetEnumerator();

        public void SortEntriesByName() => Entries.Sort((x, y) => string.Compare(x.Name, y.Name));

        public void SortEntriesByType()
        {
            Entries.Sort((x, y) =>
            {
                var type = string.Compare(x.Type, y.Type);
                if (type != 0)
                    return type;

                return string.Compare(x.Name, y.Name);
            });
        }

        // TODO: Change object to ISerializable
        public Dictionary<string, object> Extras { get; } = new Dictionary<string, object>();

        public override string Type => GetDirectoryEntry()?.Type ?? "ObjectDir";

        public MiloObject GetDirectoryEntry()
        {
            if (Extras.TryGetValue("DirectoryEntry", out var dirEntry))
            {
                return dirEntry as MiloObject;
            }

            return null;
        }
    }
}
