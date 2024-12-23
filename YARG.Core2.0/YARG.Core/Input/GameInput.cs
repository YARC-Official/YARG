using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using YARG.Core.Extensions;

namespace YARG.Core.Input
{
    /// <summary>
    /// An input that the game consumes.
    /// </summary>
    /// <remarks>
    /// Although integer, axis, and button values are all exposed at once, an action can only use one of the three.
    /// Each input type shares the same memory, and reading the incorrect value for an action will result in issues!
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public readonly struct GameInput
    {
        /// <summary>
        /// The time at which the input occurred.
        /// </summary>
        [FieldOffset(0)]
        public readonly double Time;

        /// <summary>
        /// The mode-specific action that the input is for.
        /// </summary>
        [FieldOffset(8)]
        public readonly int Action;

        // Union emulation, each of these fields will share the same memory

        /// <summary>
        /// The integer value of the input.
        /// Only valid for actions that expect integer values.
        /// </summary>
        [FieldOffset(12)]
        public readonly int Integer;

        /// <summary>
        /// The axis value of the input.
        /// Only valid for actions that expect axis values.
        /// </summary>
        [FieldOffset(12)]
        public readonly float Axis;

        /// <summary>
        /// The button state of the input.
        /// Only valid for actions that expect button states.
        /// </summary>
        [FieldOffset(12)]
        public readonly bool Button;

        public GameInput(double time, int action, int value) : this()
        {
            Time = time;
            Action = action;
            Integer = value;
        }

        public GameInput(double time, int action, float value) : this()
        {
            Time = time;
            Action = action;
            Axis = value;
        }

        public GameInput(double time, int action, bool value) : this()
        {
            Time = time;
            Action = action;
            Button = value;
        }

        // Since constructors can't be generic:

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, int value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, float value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, bool value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        /// <summary>
        /// Gets the action value as a specific enum type.
        /// </summary>
        /// <typeparam name="TAction">
        /// The enum type to convert the action into.
        /// </typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TAction GetAction<TAction>()
            where TAction : unmanaged, Enum
        {
            return Action.Convert<TAction>();
        }
    }
}