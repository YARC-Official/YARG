using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface ITrans : IRenderObject
    {
        Matrix4 Mat1 { get; set; }
        Matrix4 Mat2 { get; set; }

        List<string> Transformables { get; }

        int UnknownInt { get; set; }
        string Camera { get; set; }
        bool UnknownBool { get; set; }

        string Transform { get; set; }
    }

    public class Trans : RenderObject, ITrans
    {
        // Trans
        public Matrix4 Mat1 { get; set; } = Matrix4.Identity();
        public Matrix4 Mat2 { get; set; } = Matrix4.Identity();

        public List<string> Transformables { get; } = new List<string>();

        public int UnknownInt { get; set; }
        public string Camera { get; set; }
        public bool UnknownBool { get; set; }

        public string Transform { get; set; }

        public override string Type => "Trans";
    }

    // TODO: Remove/refactor. Hack until serializer code is refactored for inheritance
    public class TransStandalone : Trans { }
}
