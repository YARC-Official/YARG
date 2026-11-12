#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input.Devices
{
    /// <summary>
    /// Support for instruments driven by the xone kernel driver on Linux.
    /// <br/>
    /// xone exposes Xbox One instruments as evdev joysticks without hidraw nodes, which Unity
    /// reports as native devices with the "Linux" interface. HIDrogen's LinuxShim removes these
    /// devices on the assumption that its hidraw backend will provide them instead, so the
    /// instrument is recognized and then immediately disconnects on boot
    /// (see https://github.com/TheNathannator/HIDrogen/issues/15 and YARG issue #1486).
    /// <br/>
    /// Devices for the instruments registered here are renamed to the "SDL" interface, which
    /// HIDrogen explicitly leaves alone since hidraw cannot provide them. If a device is created
    /// before this class can claim it (device discovery races with initialization), it is adopted
    /// afterwards: recreated with the instrument layout while keeping its native device ID.
    /// All other devices are handled exactly as HIDrogen normally would.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
#endif
    internal static class XoneLinuxSupport
    {
        // The interface used by Unity's native Linux joystick support; removed by HIDrogen's LinuxShim.
        private const string kLinuxInterface = "Linux";
        // HIDrogen considers "SDL" devices to be devices which hidraw cannot provide (such as
        // these), and leaves them alone. This is also what keyboard and mouse devices use.
        private const string kShimmedInterface = "SDL";

        private static readonly Dictionary<string, string> s_LayoutsByProduct = new()
        {
            { XoneMadCatzRB4Drumkit.NativeProduct, nameof(XoneMadCatzRB4Drumkit) },
            { XoneMadCatzRB4Stratocaster.NativeProduct, nameof(XoneMadCatzRB4Stratocaster) },
            { XonePdpRB4Jaguar.NativeProduct, nameof(XonePdpRB4Jaguar) },
        };

        private static bool s_Initialized;
        private static bool s_AdoptPending;

        private static InputDeviceFindControlLayoutDelegate s_SdlLayoutFinder;
        private static FieldInfo s_DescriptionField;
        private static MethodInfo s_RecreateDeviceMethod;
        private static object s_InputManager;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        internal static void Initialize()
        {
            if (s_Initialized)
                return;
            s_Initialized = true;

            XoneMadCatzRB4Drumkit.Initialize();
            XoneMadCatzRB4Stratocaster.Initialize();
            XonePdpRB4Jaguar.Initialize();

            // The Input System's SDL layout finder is registered before this point (it is hooked
            // while the Input System initializes), and finders registered earlier take precedence
            // over later ones and over layout matchers. Remove it so that our finder can claim
            // these devices first. HIDrogen's LinuxShim does the same once it takes over.
            UnhookSdlLayoutFinder();
            InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;

            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onBeforeUpdate += OnBeforeUpdate;
            InputSystem.onAfterUpdate += OnAfterUpdate;

            if (!CacheInternals())
                return;

            // Claim any devices which were created before the finder was hooked
            AdoptDevices();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            // Devices are commonly discovered while the scene loads, before this class can claim
            // them. Adopt them before HIDrogen's cleanup pass runs on the first input update.
            if (s_Initialized)
                AdoptDevices();
        }

        private static bool CacheInternals()
        {
            s_DescriptionField = typeof(InputDevice).GetField("m_Description",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var inputSystemType = typeof(InputSystem);
            var managerField = inputSystemType.GetField("s_Manager",
                BindingFlags.Static | BindingFlags.NonPublic);
            var managerType = inputSystemType.Assembly.GetType("UnityEngine.InputSystem.InputManager");
            s_RecreateDeviceMethod = managerType?.GetMethod("RecreateDevice",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(InputDevice), typeof(InternedString) }, null);
            s_InputManager = managerField?.GetValue(null);

            if (s_DescriptionField == null || s_RecreateDeviceMethod == null || s_InputManager == null)
            {
                Debug.LogError("Xone instrument support: failed to find Input System internals; " +
                    "xone-driven instruments will not be detected.");
                return false;
            }

            return true;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added)
                s_AdoptPending = true;
        }

        private static void OnBeforeUpdate()
        {
            // Run before HIDrogen's cleanup pass, which also happens on the first input update
            if (s_AdoptPending)
            {
                s_AdoptPending = false;
                AdoptDevices();
            }
        }

        private static void OnAfterUpdate()
        {
            if (s_AdoptPending)
            {
                s_AdoptPending = false;
                AdoptDevices();
            }
        }

        private static string OnFindLayoutForDevice(ref InputDeviceDescription description, string matchedLayout,
            InputDeviceExecuteCommandDelegate executeCommandDelegate)
        {
            if (description.interfaceName != kLinuxInterface)
                return null;

            if (!s_LayoutsByProduct.TryGetValue(description.product, out var layoutName))
                return null;

            // HIDrogen's LinuxShim removes all "Linux" devices; hide ours from it by renaming
            // the interface at creation time, so the device keeps its native device ID.
            description.interfaceName = kShimmedInterface;
            return layoutName;
        }

        /// <summary>
        /// Renames the interface of any unclaimed instrument devices and recreates devices which
        /// were matched by a generic layout with the proper instrument layout.
        /// </summary>
        private static void AdoptDevices()
        {
            // Recreating a device replaces it, so iterate over a copy
            var devices = new List<InputDevice>(InputSystem.devices);
            foreach (var device in devices)
            {
                if (!s_LayoutsByProduct.TryGetValue(device.description.product, out var layoutName))
                    continue;

                bool recreate = device.layout != layoutName;
                RenameInterface(device);
                if (recreate)
                    RecreateDevice(device, layoutName);
            }
        }

        private static void RenameInterface(InputDevice device)
        {
            if (device.description.interfaceName != kLinuxInterface)
                return;

            var description = device.description;
            description.interfaceName = kShimmedInterface;
            s_DescriptionField.SetValue(device, description);
        }

        private static void RecreateDevice(InputDevice device, string layoutName)
        {
            // Not possible through public APIs without losing the device's native ID (and with it
            // all input), so the Input System's own layout-switching mechanism is used instead.
            s_RecreateDeviceMethod.Invoke(s_InputManager, new object[] { device, new InternedString(layoutName) });
        }

        private static void UnhookSdlLayoutFinder()
        {
            var layoutBuilder = typeof(InputSystem).Assembly
                .GetType("UnityEngine.InputSystem.Linux.SDLLayoutBuilder");
            var method = layoutBuilder?.GetMethod("OnFindLayoutForDevice",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogError("Xone instrument support: failed to find the Input System's SDL layout finder; " +
                    "xone-driven instruments will not be detected.");
                return;
            }

            s_SdlLayoutFinder = (InputDeviceFindControlLayoutDelegate) Delegate.CreateDelegate(
                typeof(InputDeviceFindControlLayoutDelegate), method);
            InputSystem.onFindLayoutForDevice -= s_SdlLayoutFinder;
        }
    }
}
#endif
