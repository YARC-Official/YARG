using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace YARG.Input {

#if UNITY_EDITOR
	// Make sure static constructor is called during startup.
	[InitializeOnLoad]
#endif

	[InputControlLayout(stateType = typeof(XInpProGuitarInputReport))]
	public class XInpProGuitarGampad : AbstractProGuitarGampad {
		static XInpProGuitarGampad() {
			// Xbox Fender Mustang
			// NOTE: Should only works on Windows. XInput is handled differently on different platforms.
			InputSystem.RegisterLayout<XInpProGuitarGampad>(matches: new InputDeviceMatcher()
				.WithInterface("XInput")
				.WithCapability("subType", 25));
		}

		// Make sure the static constructor above is called during startup (runtime).
		[RuntimeInitializeOnLoadMethod]
		private static void Init() {

		}
	}
}