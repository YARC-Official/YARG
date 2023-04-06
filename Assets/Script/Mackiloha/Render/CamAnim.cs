using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public interface ICamAnim : IRenderObject
    {
        string Camera { get; set; }
        string Animation { get; set; }
    }

    public class CamAnim : RenderObject, ICamAnim, IAnim
    {
        internal Anim Anim { get; } = new Anim();

        // Anim
        public List<AnimEntry> AnimEntries => Anim.AnimEntries;
        public List<string> Animatables => Anim.Animatables;

        // CamAnim
        public string Camera { get; set; }
        public string Animation { get; set; }

        public override string Type => "CamAnim";
    }
}
