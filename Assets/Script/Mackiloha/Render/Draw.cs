using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface IDraw : IRenderObject
    {
        bool Showing { get; set; }

        List<string> Drawables { get; }
        Sphere Boundry { get; set; }
    }

    public class Draw : RenderObject, IDraw
    {
        // Draw
        public bool Showing { get; set; } = true;

        public List<string> Drawables { get; } = new List<string>();
        public Sphere Boundry { get; set; }

        public override string Type => "Draw";
    }
}
