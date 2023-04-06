using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface ILight : IRenderObject
    {
        Sphere Origin { get; set; }
        float KeyFrame { get; set; }
    }

    public class Light : RenderObject, ILight, ITrans
    {
        internal Trans Trans { get; } = new Trans();

        // Trans
        public Matrix4 Mat1 { get => Trans.Mat1; set => Trans.Mat1 = value; }
        public Matrix4 Mat2 { get => Trans.Mat2; set => Trans.Mat2 = value; }

        public List<string> Transformables => Trans.Transformables;

        public int UnknownInt { get => Trans.UnknownInt; set => Trans.UnknownInt = value; }
        public string Camera { get => Trans.Camera; set => Trans.Camera = value; }
        public bool UnknownBool { get => Trans.UnknownBool; set => Trans.UnknownBool = value; }

        public string Transform { get => Trans.Transform; set => Trans.Transform = value; }
        
        // Light
        public Sphere Origin { get; set; }
        public float KeyFrame { get; set; }

        public override string Type => "Light";
    }
}
