using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input.Devices
{
    // While we can technically just do something like `stateFormat = "BYTE"`,
    // it ends up tripping an assert since there are no control items present in the layout
    internal struct UnsupportedXboxOneState : IInputStateTypeInfo
    {
        public FourCC format => new('G', 'I', 'P');

        [InputControl(layout = "Integer")]
        public byte dummy;
    }

    /// <summary>
    /// An unsupported Xbox One device.<br/>
    /// Doesn't show up in the device list, but shows up in device description logging,
    /// for troubleshooting purposes.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    [InputControlLayout(stateType = typeof(UnsupportedXboxOneState), hideInUI = true)]
    internal class UnsupportedXboxOneDevice : InputDevice
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<UnsupportedXboxOneDevice>(
                matches: new InputDeviceMatcher().WithInterface("GameInput")
            );
        }

        protected override void OnAdded()
        {
            base.OnAdded();

            // Disable device so it doesn't show up in the device list
            // Will still show up in device description logging
            InputSystem.DisableDevice(this);
        }
    }
}