using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Song
{
    public interface IDirectedEvent
    {
        float Position { get; set; }
    }

    public struct DirectedEventFloat : IDirectedEvent // V0
    {
        public float Position { get; set; }
        public float Value { get; set; }
    }

    public struct DirectedEventTextFloat : IDirectedEvent // V2
    {
        public float Position { get; set; }
        public string Text { get; set; }
        public float Value { get; set; }
    }

    public struct DirectedEventBoolean : IDirectedEvent // V3
    {
        public float Position { get; set; }
        public bool Enabled { get; set; }
    }

    public struct DirectedEventVector4 : IDirectedEvent // V4
    {
        public float Position { get; set; }
        public Vector4 Value { get; set; }
    }

    public struct DirectedEventVector3 : IDirectedEvent // V5
    {
        public float Position { get; set; }
        public Vector3 Value { get; set; }
    }

    public struct DirectedEventText : IDirectedEvent // V6
    {
        public float Position { get; set; }
        public string Text { get; set; }
    }

    public enum DirectedEventType : int
    {
        Float,
        TextFloat = 2,
        Boolean,
        Vector4,
        Vector3,
        Text
    }

    public struct DirectedEventGroup
    {
        public DirectedEventType EventType;

        public string DirectorName;

        public string PropName;
        public int Unknown1;
        public string PropName2;
        public int Unknown2;

        public List<IDirectedEvent> Events;
    }

    public class PropAnim : MiloObject
    {
        public string AnimName { get; set; }
        public float TotalTime { get; set; }

        public List<DirectedEventGroup> DirectorGroups { get; } = new List<DirectedEventGroup>();

        public override string Type => "PropAnim";
    }
}
