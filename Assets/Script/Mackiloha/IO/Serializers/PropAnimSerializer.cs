using Mackiloha.Song;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mackiloha.IO.Serializers
{
    public class PropAnimSerializer : AbstractSerializer
    {
        public PropAnimSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var propAnim = data as PropAnim;
            int version = ReadMagic(ar, data);

            var meta = ReadMeta(ar);
            propAnim.AnimName = meta.ScriptName;

            ar.BaseStream.Position += 4; // Skip 4 constant
            propAnim.TotalTime = ar.ReadSingle();
            ar.BaseStream.Position += 4; // Skip 0/1 number

            if (version >= 12)
                ar.BaseStream.Position += 1;

            var eventGroupCount = ar.ReadInt32();
            propAnim.DirectorGroups
                .AddRange(
                    RepeatFor(eventGroupCount, () => ReadGroupEvent(ar)));
        }

        protected virtual DirectedEventGroup ReadGroupEvent(AwesomeReader ar)
        {
            var eventGroup = new DirectedEventGroup();

            var num1 = ar.ReadInt32();
            var num2 = ar.ReadInt32();

            if (num1 != num2)
                throw new NotSupportedException($"Expected \"{num1}\" to equal \"{num2}\" for start of directed event group");

            if (!Enum.IsDefined(typeof(DirectedEventType), num1))
                throw new NotSupportedException($"Value of \'{num1}\' is not supported for directed event!");

            eventGroup.EventType = (DirectedEventType)num1;
            eventGroup.DirectorName = ar.ReadString();
            ar.BaseStream.Position += 11; // Skip constants

            eventGroup.PropName = ar.ReadString();
            eventGroup.Unknown1 = ar.ReadInt32(); // Usually 0
            eventGroup.PropName2 = ar.ReadString(); // Usually empty
            eventGroup.Unknown2 = ar.ReadInt32(); // Unknown enum... hopefully it's not important

            var count = ar.ReadInt32();
            eventGroup.Events = new List<IDirectedEvent>();

            switch (eventGroup.EventType)
            {
                case DirectedEventType.Float:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventFloat()
                        {
                            Value = ar.ReadSingle(),
                            Position = ar.ReadSingle()
                        }));
                    break;
                case DirectedEventType.TextFloat:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventTextFloat()
                        {
                            Value = ar.ReadSingle(),
                            Text = ar.ReadString(),
                            Position = ar.ReadSingle()
                        }));
                    break;
                case DirectedEventType.Boolean:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventBoolean()
                        {
                            Enabled = ar.ReadBoolean(),
                            Position = ar.ReadSingle()
                        }));
                    break;
                case DirectedEventType.Vector4:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventVector4()
                        {
                            Value = new Vector4()
                            {
                                X = ar.ReadSingle(),
                                Y = ar.ReadSingle(),
                                Z = ar.ReadSingle(),
                                W = ar.ReadSingle()
                            },
                            Position = ar.ReadSingle()
                        }));
                    break;
                case DirectedEventType.Vector3:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventVector3()
                        {
                            Value = new Vector3()
                            {
                                X = ar.ReadSingle(),
                                Y = ar.ReadSingle(),
                                Z = ar.ReadSingle()
                            },
                            Position = ar.ReadSingle()
                        }));
                    break;
                case DirectedEventType.Text:
                    eventGroup.Events.AddRange(RepeatFor<IDirectedEvent>(count,
                        () => new DirectedEventText()
                        {
                            Text = ar.ReadString(),
                            Position = ar.ReadSingle()
                        }));
                    break;
            }

            return eventGroup;
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var propAnim = data as PropAnim;

            var version = Magic();

            // Hacky way to support GDRB
            if (version == 11 && IsGRDRBAnim(propAnim))
                version = 12;

            aw.Write(version);

            // Write meta
            aw.Write((int)2);
            aw.Write((string)propAnim.AnimName);
            aw.BaseStream.Position += 5; // Skip zeros

            aw.Write((int)4);
            aw.Write((float)propAnim.TotalTime);
            aw.Write((int)1);

            if (version >= 12)
                aw.Write((byte)0x00);

            // Write director groups
            aw.Write(propAnim.DirectorGroups.Count);
            foreach (var group in propAnim.DirectorGroups)
                WriteGroupEvent(group, aw);
        }

        protected void WriteGroupEvent(DirectedEventGroup eventGroup, AwesomeWriter aw)
        {
            // Write event type + director
            aw.Write((int)eventGroup.EventType);
            aw.Write((int)eventGroup.EventType);
            aw.Write((string)eventGroup.DirectorName);

            // Constants
            aw.Write((bool)true);
            aw.Write((bool)false);
            aw.Write((bool)true);
            aw.Write((int)0);
            aw.Write((int)5);

            // Write prop names + unknown ints
            aw.Write((string)eventGroup.PropName);
            aw.Write((int)eventGroup.Unknown1);
            aw.Write((string)eventGroup.PropName2);
            aw.Write((int)eventGroup.Unknown2);

            if (eventGroup.Events is null)
            {
                aw.Write((int)0);
                return;
            }

            // Write events
            aw.Write((int)eventGroup.Events.Count);

            switch (eventGroup.EventType)
            {
                case DirectedEventType.Float:
                    var fEvents = CastEvents<DirectedEventFloat>(eventGroup.Events);

                    foreach (var ev in fEvents)
                    {
                        aw.Write((float)ev.Value);
                        aw.Write((float)ev.Position);
                    }
                    break;
                case DirectedEventType.TextFloat:
                    var tfEvents = CastEvents<DirectedEventTextFloat>(eventGroup.Events);

                    foreach (var ev in tfEvents)
                    {
                        aw.Write((float)ev.Value);
                        aw.Write((string)ev.Text);
                        aw.Write((float)ev.Position);
                    }
                    break;
                case DirectedEventType.Boolean:
                    var bEvents = CastEvents<DirectedEventBoolean>(eventGroup.Events);

                    foreach (var ev in bEvents)
                    {
                        aw.Write((bool)ev.Enabled);
                        aw.Write((float)ev.Position);
                    }
                    break;
                case DirectedEventType.Vector4:
                    var v4Events = CastEvents<DirectedEventVector4>(eventGroup.Events);

                    foreach (var ev in v4Events)
                    {
                        aw.Write((float)ev.Value.X);
                        aw.Write((float)ev.Value.Y);
                        aw.Write((float)ev.Value.Z);
                        aw.Write((float)ev.Value.W);
                        aw.Write((float)ev.Position);
                    }
                    break;
                case DirectedEventType.Vector3:
                    var v3Events = CastEvents<DirectedEventVector3>(eventGroup.Events);

                    foreach (var ev in v3Events)
                    {
                        aw.Write((float)ev.Value.X);
                        aw.Write((float)ev.Value.Y);
                        aw.Write((float)ev.Value.Z);
                        aw.Write((float)ev.Position);
                    }
                    break;
                case DirectedEventType.Text:
                    var tEvents = CastEvents<DirectedEventText>(eventGroup.Events);

                    foreach (var ev in tEvents)
                    {
                        aw.Write((string)ev.Text);
                        aw.Write((float)ev.Position);
                    }
                    break;
            }
        }

        protected List<T> CastEvents<T>(List<IDirectedEvent> events)
            where T : IDirectedEvent
            => events
                .Select(x => (T)Convert.ChangeType(x, typeof(T)))
                .ToList();

        protected bool IsGRDRBAnim(PropAnim anim)
        {
            var gdrbNames = new[] { "_mikedirnt", "_trecool", "_billiejoe" };

            // Check if any events are for GDRB specific band members
            return anim.DirectorGroups
                .Select(x => x.PropName)
                .Any(x => gdrbNames
                    .Any(y => x.EndsWith(y, StringComparison.CurrentCultureIgnoreCase)));
        }

        public override bool IsOfType(ISerializable data) => data is PropAnim;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 25:
                    // TBRB
                    // TODO: Refector to use optional different magic
                    return 11;
                default:
                    return -1;
            }
        }

        internal override int[] ValidMagics()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 25:
                    // TBRB / GDRB
                    return new[] { 11, 12 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
