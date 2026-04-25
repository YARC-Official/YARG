using System.Collections.Generic;
using UnityEngine;

namespace YARG.Venue
{
    public enum AnimatorCommandType
    {
        Trigger,
        BoolOn,
        BoolOff,
        Float,
        Blend,
        Randomize
    }

    public readonly struct AnimatorCommand
    {
        public readonly double              Time;
        public readonly Animator            Target;
        public readonly AnimatorCommandType Type;
        public readonly int                 ParamHash;
        public readonly float               Value;      // Bool: 1=true/0=false, Float: the value, Blend: duration
        public readonly int                 BlendLayer; // only used for Blend

        private AnimatorCommand(double time, Animator target, AnimatorCommandType type,
            int hash = 0, float value = 0f, int blendLayer = 0)
        {
            Time = time;
            Target = target;
            Type = type;
            ParamHash = hash;
            Value = value;
            BlendLayer = blendLayer;
        }

        public static AnimatorCommand Trigger(double time, Animator target, int hash) =>
            new(time, target, AnimatorCommandType.Trigger, hash);

        public static AnimatorCommand BoolOn(double time, Animator target, int hash, float offAfterSeconds) =>
            new(time, target, AnimatorCommandType.BoolOn, hash, offAfterSeconds);

        public static AnimatorCommand BoolOff(double time, Animator target, int hash) =>
            new(time, target, AnimatorCommandType.BoolOff, hash);

        public static AnimatorCommand Float(double time, Animator target, int hash, float value) =>
            new(time, target, AnimatorCommandType.Float, hash, value);

        public static AnimatorCommand Blend(double time, Animator target, int hash,
            float duration, int layer) =>
            new(time, target, AnimatorCommandType.Blend, hash, duration, layer);

        public static AnimatorCommand Randomize(double time, Animator target) =>
            new(time, target, AnimatorCommandType.Randomize);
    }

    /// <summary>
    /// A pre-sorted list of animator commands for the entire song.
    /// Consumed in order against VisualTime during Update.
    /// </summary>
    public sealed class AnimatorCommandQueue
    {
        private readonly List<AnimatorCommand> _commands = new();
        private          int                   _index;

        public void Add(AnimatorCommand cmd) => _commands.Add(cmd);

        /// <summary>Call once after all channels have finished adding commands.</summary>
        public void Sort() => _commands.Sort(static (a, b) => a.Time.CompareTo(b.Time));

        public void Reset() => _index = 0;

        /// <summary>
        /// Dispatches all commands whose time has arrived.
        /// Returns any BoolOn commands so the caller can schedule their BoolOff pair.
        /// </summary>
        public void Flush(double visualTime, List<AnimatorCommand> pendingBoolOffs, VenueHashLibrary hashLib)
        {
            while (_index < _commands.Count && _commands[_index].Time <= visualTime)
            {
                var cmd = _commands[_index++];
                switch (cmd.Type)
                {
                    case AnimatorCommandType.Trigger:
                        hashLib.Randomize(cmd.Target);
                        cmd.Target.SafeSetTrigger(cmd.ParamHash);
                        break;
                    case AnimatorCommandType.BoolOn:
                        hashLib.Randomize(cmd.Target);
                        cmd.Target.SafeSetBool(cmd.ParamHash, true);
                        if (cmd.Value > 0f) // Value holds the off-delay
                            pendingBoolOffs.Add(AnimatorCommand.BoolOff(
                                cmd.Time + cmd.Value, cmd.Target, cmd.ParamHash));
                        break;
                    case AnimatorCommandType.BoolOff:
                        cmd.Target.SafeSetBool(cmd.ParamHash, false);
                        break;
                    case AnimatorCommandType.Float:
                        cmd.Target.SafeSetFloat(cmd.ParamHash, cmd.Value);
                        break;
                    case AnimatorCommandType.Blend:
                        hashLib.Randomize(cmd.Target);
                        cmd.Target.SafeCrossFadeInFixedTime(cmd.ParamHash, cmd.Value, cmd.BlendLayer);
                        break;
                    case AnimatorCommandType.Randomize:
                        hashLib.Randomize(cmd.Target);
                        break;
                }
            }
        }
    }
}