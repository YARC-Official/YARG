using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.DTB;

namespace Mackiloha
{
    // Serialized w/ milo entries beginning with GH2
    public struct MiloMeta
    {
        public int Revision;
        public string ScriptName;
        public DTBFile Script;
        public string Comments;
    }
}
