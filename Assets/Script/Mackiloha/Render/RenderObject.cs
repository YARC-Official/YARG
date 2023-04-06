using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface IRenderObject : IMiloObject { }

    public abstract class RenderObject : MiloObject, IRenderObject
    {
        public override string Type => "Render";
    }
}
