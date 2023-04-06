using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Render
{
    public struct LightEvent
    {
        public Sphere Origin;
        public float KeyFrame;
    }

    public interface ILightAnim : IRenderObject
    {
        string Light { get; set; }
        List<LightEvent> Events { get; }

        string LightAnimation { get; set; }
    }

    public class LightAnim : RenderObject, ILightAnim, IAnim
    {
        internal Anim Anim { get; } = new Anim();

        // Anim
        public List<AnimEntry> AnimEntries => Anim.AnimEntries;
        public List<string> Animatables => Anim.Animatables;

        // LightAnim
        public string Light { get; set; }
        public List<LightEvent> Events { get; } = new List<LightEvent>();

        public string LightAnimation { get; set; }

        public override string Type => "LightAnim";
    }
}
