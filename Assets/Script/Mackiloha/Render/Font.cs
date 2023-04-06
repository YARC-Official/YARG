using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public struct FontEntry
    {
        public int Unknown;
        public float UnknownF;
    }

    public interface IFont : IRenderObject
    {
        string Material { get; set; }
        float CharacterWidth { get; set; }
        float CharacterHeight { get; set; }
        char[] Chracters { get; set; }
        List<FontEntry> FontEntries { get; }
    }

    public class Font : RenderObject, IFont
    {
        // Font
        public string Material { get; set; }
        public float CharacterWidth { get; set; }
        public float CharacterHeight { get; set; }
        public char[] Chracters { get; set; }
        public List<FontEntry> FontEntries { get; } = new List<FontEntry>();

        public override string Type => "Font";
    }
}
