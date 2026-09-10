#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using YARG.Core.Logging;

namespace YARG.Input
{
    /// <summary>
    ///     Publishes GameController-framework devices into the input system.
    ///     Unity's iOS backend only surfaces profile-conforming pads as
    ///     Gamepads, hiding instrument controllers' extra elements; this
    ///     backend flattens each controller's physicalInputProfile (via
    ///     GameControllerBridge.mm) into a per-controller layout whose
    ///     controls can be bound like any other device. Plain gamepads are
    ///     skipped — Unity already handles those.
    /// </summary>
    public static unsafe class IOSGameControllerBackend
    {
        private const int MAX_VALUES = 64;
        private const string INTERFACE_NAME = "iOSGameController";

        // Product categories Unity's own backend already covers as Gamepad
        private static readonly HashSet<string> SKIPPED_CATEGORIES = new()
        {
            "Xbox One", "DualShock 4", "DualSense", "MFi",
            "Switch Pro Controller", "Nintendo Switch Joy-Con (L/R)",
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct State : IInputStateTypeInfo
        {
            public FourCC format => new('Y', 'G', 'C', 'C');
            public fixed float values[MAX_VALUES];
        }

        private class Entry
        {
            public InputDevice Device;
            public int ValueCount;
        }

        // Keyed by the native side's per-connection generation id
        private static readonly Dictionary<int, Entry> _devices = new();
        private static readonly HashSet<int> _seenGenerations = new();
        private static bool _initialized;

        [DllImport("__Internal")]
        private static extern void yarg_gc_start();

#if YARG_TEST_BUILD
        [DllImport("__Internal")]
        private static extern int yarg_gc_spawn_virtual();
#endif

        [DllImport("__Internal")]
        private static extern int yarg_gc_get_controller_count();

        [DllImport("__Internal")]
        private static extern int yarg_gc_get_controller_generation(int index);

        [DllImport("__Internal")]
        private static extern string yarg_gc_get_controller_name(int index);

        [DllImport("__Internal")]
        private static extern string yarg_gc_get_product_category(int index);

        [DllImport("__Internal")]
        private static extern int yarg_gc_get_value_count(int index);

        [DllImport("__Internal")]
        private static extern string yarg_gc_get_value_name(int index, int valueIndex);

        [DllImport("__Internal")]
        private static extern int yarg_gc_read_values(int index, float* buffer, int capacity);

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            yarg_gc_start();
            InputSystem.onBeforeUpdate += Poll;

#if YARG_TEST_BUILD
            // Simulator/automation validation: dropping this marker file into
            // the sandbox spawns Apple's software GCVirtualController, whose
            // GCController flows through the bridge like real hardware. Its
            // category is only unskipped once the spawn succeeds, so the
            // marker can never disturb real-controller publishing — the
            // native side refuses to spawn outside the simulator, where the
            // on-screen gamepad would cover the UI and intercept touches.
            string marker = System.IO.Path.Combine(
                Helpers.PathHelper.RealPersistentDataPath, "spawn-virtual-controller.txt");
            if (System.IO.File.Exists(marker))
            {
                if (yarg_gc_spawn_virtual() != 0)
                {
                    SKIPPED_CATEGORIES.Clear();
                }
                else
                {
                    YargLogger.LogInfo(
                        "Virtual controller marker ignored (simulator-only, needs iOS 15+)");
                }
            }
#endif
        }

        private static void Poll()
        {
            try
            {
                int count = yarg_gc_get_controller_count();

                _seenGenerations.Clear();
                for (int i = 0; i < count; i++)
                {
                    int generation = yarg_gc_get_controller_generation(i);
                    _seenGenerations.Add(generation);

                    if (!_devices.ContainsKey(generation))
                    {
                        AddController(i, generation);
                    }

                    if (_devices.TryGetValue(generation, out var entry) && entry.Device != null)
                    {
                        var state = new State();
                        yarg_gc_read_values(i, state.values, Math.Min(entry.ValueCount, MAX_VALUES));
                        InputSystem.QueueStateEvent(entry.Device, state);
                    }
                }

                // Disconnections
                foreach (var (generation, entry) in new List<KeyValuePair<int, Entry>>(_devices))
                {
                    if (!_seenGenerations.Contains(generation))
                    {
                        _devices.Remove(generation);
                        if (entry.Device != null)
                        {
                            InputSystem.RemoveDevice(entry.Device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let a backend failure spam or break the input update
                InputSystem.onBeforeUpdate -= Poll;
                YargLogger.LogException(ex, "iOS GameController backend failed; disabling");
            }
        }

        private static void AddController(int index, int generation)
        {
            string category = yarg_gc_get_product_category(index) ?? "";
            if (SKIPPED_CATEGORIES.Contains(category))
            {
                // Track it so we don't re-check every frame
                _devices[generation] = new Entry();
                return;
            }

            string name = yarg_gc_get_controller_name(index) ?? "Controller";
            int valueCount = Math.Min(yarg_gc_get_value_count(index), MAX_VALUES);

            var controls = new StringBuilder();
            var usedNames = new HashSet<string>();
            for (int v = 0; v < valueCount; v++)
            {
                string rawName = yarg_gc_get_value_name(index, v) ?? $"value{v}";
                string controlName = SanitizeControlName(rawName, usedNames);

                // Axes report -1..1 (sticks) or 0..1; buttons are 0..1.
                // Buttons get Button controls so they can act as binds.
                bool isAxis = rawName.EndsWith("/x") || rawName.EndsWith("/y") ||
                    rawName.Contains("Axis") || rawName.Contains("Thumbstick");

                if (controls.Length > 0)
                {
                    controls.Append(',');
                }
                controls.Append(
                    $"{{\"name\":\"{controlName}\",\"layout\":\"{(isAxis ? "Axis" : "Button")}\"," +
                    $"\"offset\":{v * 4},\"format\":\"FLT\"}}");
            }

            string layoutName = $"{INTERFACE_NAME}_{generation}";
            string json =
                $"{{\"name\":\"{layoutName}\"," +
                $"\"displayName\":\"{Escape(name)}\"," +
                $"\"format\":\"YGCC\"," +
                $"\"device\":{{\"interface\":\"{INTERFACE_NAME}\",\"serial\":\"{generation}\"}}," +
                $"\"controls\":[{controls}]}}";

            try
            {
                InputSystem.RegisterLayout(json);
                var device = InputSystem.AddDevice(new InputDeviceDescription
                {
                    interfaceName = INTERFACE_NAME,
                    product = name,
                    serial = generation.ToString(),
                });

                _devices[generation] = new Entry { Device = device, ValueCount = valueCount };
                YargLogger.LogFormatInfo("Added GameController device '{0}' with {1} controls",
                    name, valueCount);
            }
            catch (Exception ex)
            {
                _devices[generation] = new Entry();
                YargLogger.LogException(ex, $"Failed to add GameController device '{name}'");
            }
        }

        private static string SanitizeControlName(string raw, HashSet<string> used)
        {
            var builder = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            string name = builder.ToString();
            string result = name;
            for (int suffix = 2; !used.Add(result); suffix++)
            {
                result = $"{name}_{suffix}";
            }
            return result;
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
