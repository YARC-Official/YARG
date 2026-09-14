#if UNITY_IOS && YARG_TEST_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Helpers
{
    /// <summary>
    ///     Boot-time, no-UI self-test for on-device validation of the input and
    ///     audio-capture paths that cannot be driven through a physical device's
    ///     touchscreen from automation. Runs only in test builds and only when a
    ///     "device-selftest.txt" marker is present in the app's Documents folder,
    ///     then logs results a harness can pull back with devicectl.
    /// </summary>
    internal static class IOSDeviceSelfTest
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Schedule()
        {
            string marker = Path.Combine(PathHelper.RealPersistentDataPath, "device-selftest.txt");
            if (!File.Exists(marker))
            {
                return;
            }

            var runner = new GameObject("IOSDeviceSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<SelfTestRunner>();
        }

        private sealed class SelfTestRunner : MonoBehaviour
        {
            private float _timer = 6f; // let audio + input backends come up

            private void Update()
            {
                _timer -= Time.unscaledDeltaTime;
                if (_timer > 0f)
                {
                    return;
                }

                Destroy(gameObject);
                Run();
            }

            private static void Run()
            {
                YargLogger.LogInfo("[SELFTEST] ===== iOS device self-test begin =====");
                RunMicTest();
                YargLogger.LogInfo("[SELFTEST] ===== iOS device self-test end =====");
            }

            private static void RunMicTest()
            {
                try
                {
                    var devices = GlobalAudioHandler.GetAllInputDevices();
                    YargLogger.LogFormatInfo("[SELFTEST] mic enumeration returned {0} device(s)", devices.Count);
                    if (devices.Count == 0)
                    {
                        YargLogger.LogWarning("[SELFTEST] no input devices to claim");
                        return;
                    }

                    var device = devices[0];
                    YargLogger.LogFormatInfo("[SELFTEST] claiming '{0}' (channel {1} of {2})",
                        device.DisplayName, device.Channel, device.ChannelCount);

                    MicDevice? mic = null;
                    try
                    {
                        mic = GlobalAudioHandler.CreateInputDevice(device);
                        YargLogger.LogFormatInfo("[SELFTEST] mic claim {0}",
                            mic != null ? "SUCCEEDED — capture path works on device"
                                        : "FAILED (see recording errors above)");
                    }
                    finally
                    {
                        mic?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "[SELFTEST] mic test threw");
                }
            }
        }
    }
}
#endif
