#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using YARG.Core.Logging;

namespace YARG.Helpers
{
    /// <summary>
    ///     Presents the system Bluetooth MIDI pairing sheet
    ///     (CABTMIDICentralViewController). Devices paired there appear as
    ///     regular CoreMIDI sources, which the RtMidi/Minis input path picks up
    ///     like any other MIDI device.
    /// </summary>
    public static class IOSBluetoothMidi
    {
        private delegate void DismissedCallback();

        [DllImport("__Internal", EntryPoint = "yarg_show_bluetooth_midi_pairing")]
        private static extern void ShowPairingNative(DismissedCallback callback);

        // Keeps the reverse-P/Invoke delegate alive while the sheet is open
        private static readonly DismissedCallback _nativeCallback = OnDismissed;

        private static Action _pendingCallback;

        public static void ShowPairingDialog(Action onDismissed = null)
        {
            _pendingCallback = onDismissed;
            ShowPairingNative(_nativeCallback);
        }

        [MonoPInvokeCallback(typeof(DismissedCallback))]
        private static void OnDismissed()
        {
            var callback = _pendingCallback;
            _pendingCallback = null;

            try
            {
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error after Bluetooth MIDI pairing dialog closed!");
            }
        }
    }
}
#endif
