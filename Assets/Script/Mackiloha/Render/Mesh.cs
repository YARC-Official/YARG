using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public struct Vertex3
    {
        public float X;
        public float Y;
        public float Z;

        public float NormalX;
        public float NormalY;
        public float NormalZ;

        public float ColorR;
        public float ColorG;
        public float ColorB;
        public float ColorA;

        public float U;
        public float V;
    }

    public struct Face
    {
        public ushort V1;
        public ushort V2;
        public ushort V3;
    }

    public struct FaceGroup
    {
        public int Size;
        public List<int> Sections;
        public List<int> VertexIndicies;
    }

    public struct Bone
    {
        public string Name;
        public Matrix4 Mat;
    }
    
    public interface IMesh : IRenderObject
    {
        string Material { get; set; }
        string MainMesh { get; set; }

        int Unknown { get; set; }

        List<Vertex3> Vertices { get; }
        List<Face> Faces { get; }

        List<FaceGroup> Groups { get; }
        List<Bone> Bones { get; }
    }

    public class Mesh : RenderObject, IMesh, ITrans, IDraw
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
        
        // Mesh
        public string Material { get; set; }
        public string MainMesh { get; set; }

        public int Unknown { get; set; }

        public List<Vertex3> Vertices { get; } = new List<Vertex3>();
        public List<Face> Faces { get; } = new List<Face>();

        public List<FaceGroup> Groups { get; } = new List<FaceGroup>();
        public List<Bone> Bones { get; } = new List<Bone>();

        public override string Type => "Mesh";
    }
}
