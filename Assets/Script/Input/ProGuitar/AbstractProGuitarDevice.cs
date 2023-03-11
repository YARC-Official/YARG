using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YARG.Input {
	public abstract class AbstractProGuitarGampad : InputDevice {
		public IntegerControl Fret0 {
			get;
			private set;
		}
		public IntegerControl Fret1 {
			get;
			private set;
		}
		public IntegerControl Fret2 {
			get;
			private set;
		}
		public IntegerControl Fret3 {
			get;
			private set;
		}
		public IntegerControl Fret4 {
			get;
			private set;
		}
		public IntegerControl Fret5 {
			get;
			private set;
		}

		public IntegerControl String0 {
			get;
			private set;
		}
		public IntegerControl String1 {
			get;
			private set;
		}
		public IntegerControl String2 {
			get;
			private set;
		}
		public IntegerControl String3 {
			get;
			private set;
		}
		public IntegerControl String4 {
			get;
			private set;
		}
		public IntegerControl String5 {
			get;
			private set;
		}

		protected override void FinishSetup() {
			Fret0 = GetChildControl<IntegerControl>("fret0");
			Fret1 = GetChildControl<IntegerControl>("fret1");
			Fret2 = GetChildControl<IntegerControl>("fret2");
			Fret3 = GetChildControl<IntegerControl>("fret3");
			Fret4 = GetChildControl<IntegerControl>("fret4");
			Fret5 = GetChildControl<IntegerControl>("fret5");

			String0 = GetChildControl<IntegerControl>("string0");
			String1 = GetChildControl<IntegerControl>("string1");
			String2 = GetChildControl<IntegerControl>("string2");
			String3 = GetChildControl<IntegerControl>("string3");
			String4 = GetChildControl<IntegerControl>("string4");
			String5 = GetChildControl<IntegerControl>("string5");

			base.FinishSetup();
		}

		public IntegerControl GetFretControl(int i) {
			return i switch {
				0 => Fret0,
				1 => Fret1,
				2 => Fret2,
				3 => Fret3,
				4 => Fret4,
				5 => Fret5,
				_ => null
			};
		}

		public IntegerControl GetStringControl(int i) {
			return i switch {
				0 => String0,
				1 => String1,
				2 => String2,
				3 => String3,
				4 => String4,
				5 => String5,
				_ => null
			};
		}
	}
}