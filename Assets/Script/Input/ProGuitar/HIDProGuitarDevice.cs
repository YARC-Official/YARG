using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace YARG.Input {

#if UNITY_EDITOR
	// Make sure static constructor is called during startup.
	[InitializeOnLoad]
#endif

	[InputControlLayout(stateType = typeof(HIDProGuitarInputReport))]
	public class HIDProGuitarGampad : AbstractProGuitarGampad {
		static HIDProGuitarGampad() {
			// Wii Fender Mustang
			InputSystem.RegisterLayout<HIDProGuitarGampad>(matches: new InputDeviceMatcher()
				.WithInterface("HID")
				.WithCapability("vendorId", 0x1BAD)
				.WithCapability("productId", 0x3430));

			// PS3 Fender Mustang
			InputSystem.RegisterLayout<HIDProGuitarGampad>(matches: new InputDeviceMatcher()
				.WithInterface("HID")
				.WithCapability("vendorId", 0x12BA)
				.WithCapability("productId", 0x2430));
		}

		// Make sure the static constructor above is called during startup (runtime).
		[RuntimeInitializeOnLoadMethod]
		private static void Init() {

		}
	}
}