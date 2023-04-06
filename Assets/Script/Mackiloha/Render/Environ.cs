using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface IEnviron : IRenderObject
    {
        List<string> Lights { get; }
        Color4 AmbientColor { get; set; }

        float FogStart { get; set; }
        float FogEnd { get; set; }
        Color4 FogColor { get; set; }

        bool EnableFog { get; set; }
    }

    public class Environ : RenderObject, IEnviron, IDraw
    {
        internal Draw Draw { get; } = new Draw();

        // Draw
        public bool Showing { get => Draw.Showing; set => Draw.Showing = value; }

        public List<string> Drawables => Draw.Drawables;
        public Sphere Boundry { get => Draw.Boundry; set => Draw.Boundry = value; }

        // Environ
        public List<string> Lights { get; } = new List<string>();
        public Color4 AmbientColor { get; set; }

        public float FogStart { get; set; }
        public float FogEnd { get; set; }
        public Color4 FogColor { get; set; }

        public bool EnableFog { get; set; }

        public override string Type => "Environ";
    }
}
