using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha
{
    public interface IMiloObject : ISerializable
    {
        string Name { get; set; }
        string Type { get; }
    }

    public abstract class MiloObject : IMiloObject
    {        
        public virtual string Name { get; set; }
        public abstract string Type { get; }

        public override string ToString()
            => Name != "" ? $"{Type}: {Name}" : Type;
    }
}
