using System.Runtime.InteropServices;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input {
	[StructLayout(LayoutKind.Explicit, Size = 27)]
	public struct HIDProGuitarInputReport : IInputStateTypeInfo {
		// TODO: Make this match up more with the XInput report.

		public FourCC format => new('H', 'I', 'D');

		[InputControl(name = "fret0", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 5)]
		[InputControl(name = "fret1", format = "BIT", layout = "Integer", bit = 5, sizeInBits = 5)]
		[InputControl(name = "fret2", format = "BIT", layout = "Integer", bit = 10, sizeInBits = 5)]
		[InputControl(name = "fret3", format = "BIT", layout = "Integer", bit = 16, sizeInBits = 5)]
		[InputControl(name = "fret4", format = "BIT", layout = "Integer", bit = 21, sizeInBits = 5)]
		[InputControl(name = "fret5", format = "BIT", layout = "Integer", bit = 26, sizeInBits = 5)]
		[FieldOffset(6)]
		public int fretStates;

		[InputControl(name = "string0", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(10)]
		public byte str0Velocity;
		[InputControl(name = "string1", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(11)]
		public byte str1Velocity;
		[InputControl(name = "string2", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(12)]
		public byte str2Velocity;
		[InputControl(name = "string3", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(13)]
		public byte str3Velocity;
		[InputControl(name = "string4", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(14)]
		public byte str4Velocity;
		[InputControl(name = "string5", format = "BIT", layout = "Integer", bit = 0, sizeInBits = 7)]
		[FieldOffset(15)]
		public byte str5Velocity;

		[FieldOffset(25)]
		public short reportId;
	}
}