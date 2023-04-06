using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface ICam : IRenderObject
    {
        float NearPlane { get; set; }
        float FarPlane { get; set; }
        float FOV { get; set; }

        Rectangle ScreenArea { get; set; }
        Vector2 ZRange { get; set; }

        string TargetTexture { get; set; }
    }

    public class Cam : RenderObject, ICam, ITrans, IDraw
    {
        internal Trans Trans { get; } = new Trans();
        internal Draw Draw { get; } = new Draw();

        // Trans
        public Matrix4 Mat1 { get => Trans.Mat1; set => Trans.Mat1 = value; }
        public Matrix4 Mat2 { get => Trans.Mat2; set => Trans.Mat2 = value; }

        public List<string> Transformables => Trans.Transformables;

        public int UnknownInt { get => Trans.UnknownInt; set => Trans.UnknownInt = value; }
        public string Camera { get => Trans.Camera; set => Trans.Camera = value; }
        public bool UnknownBool { get => Trans.UnknownBool; set => Trans.UnknownBool = value; }

        public string Transform { get => Trans.Transform; set => Trans.Transform = value; }

        // Draw
        public bool Showing { get => Draw.Showing; set => Draw.Showing = value; }

        public List<string> Drawables => Draw.Drawables;
        public Sphere Boundry { get => Draw.Boundry; set => Draw.Boundry = value; }

        // Cam
        public float NearPlane { get; set; }
        public float FarPlane { get; set; }
        public float FOV { get; set; }

        public Rectangle ScreenArea { get; set; }
        public Vector2 ZRange { get; set; }

        public string TargetTexture { get; set; }

        public override string Type => "Cam";
    }
}
