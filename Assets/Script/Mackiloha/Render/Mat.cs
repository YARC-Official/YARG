using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public struct TextureEntry
    {
        public int Unknown1;
        public int Unknown2;
        public Matrix4 Mat;
        public int Unknown3;
        public string Texture;
    }

    public enum BlendFactor : int
    {
        Unknown = 0,
        Zero = 1,
        One = 2,
        SrColor = 3,
        InvSrColor = 4
    }

    public interface IMat : IRenderObject
    {
        List<TextureEntry> TextureEntries { get; }

        Color4 BaseColor { get; set; }
        BlendFactor Blend { get; set; }
    }

    public class Mat : RenderObject, IMat
    {
        // Mat
        public List<TextureEntry> TextureEntries { get; } = new List<TextureEntry>();

        public Color4 BaseColor { get; set; }
        public BlendFactor Blend { get; set; }

        public override string Type => "Mat";
    }
}
