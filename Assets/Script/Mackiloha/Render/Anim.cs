using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public struct AnimEntry
    {
        public string Name;
        public float F1;
        public float F2;
    }

    public interface IAnim : IRenderObject
    {
        List<AnimEntry> AnimEntries { get; }
        List<string> Animatables { get; }
    }

    public class Anim : RenderObject, IAnim
    {
        // Anim
        public List<AnimEntry> AnimEntries { get; } = new List<AnimEntry>();
        public List<string> Animatables { get; } = new List<string>();

        public override string Type => "Anim";
    }
}
