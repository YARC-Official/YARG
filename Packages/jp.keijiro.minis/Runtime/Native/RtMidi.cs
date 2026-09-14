using System.Runtime.InteropServices;
using System;

namespace Minis.Native
{
    internal unsafe abstract class RtMidiHandle : SafeHandle
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RtMidiWrapper
        {
            public IntPtr ptr;
            public IntPtr data;
            [MarshalAs(UnmanagedType.U1)]
            public bool ok;
            public IntPtr msg;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        private RtMidiWrapper* Ptr => (RtMidiWrapper*)handle;

        public bool Ok => !IsInvalid && Ptr->ok;
        public string ErrorMessage => !IsInvalid ? Marshal.PtrToStringAnsi(Ptr->msg) : null;

        protected RtMidiHandle(bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
        }
    }

    internal sealed class RtMidiInHandle : RtMidiHandle
    {
        private RtMidiInHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            rtmidi_in_free(handle);
            return true;
        }

        [DllImport(RtMidi.DllName)]
        private static extern void rtmidi_in_free(IntPtr device);
    }

    internal sealed class RtMidiOutHandle : RtMidiHandle
    {
        private RtMidiOutHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            rtmidi_out_free(handle);
            return true;
        }

        [DllImport(RtMidi.DllName)]
        private static extern void rtmidi_out_free(IntPtr device);
    }

    internal unsafe delegate void RtMidiCCallback(
        double timeStamp,
        byte* message,
        UIntPtr messageSize,
        void* userData
    );

    internal static unsafe class RtMidi
    {
#if UNITY_IOS && !UNITY_EDITOR
        // iOS plugins are static libraries; symbols live in the main executable.
        internal const string DllName = "__Internal";
#else
        internal const string DllName = "RtMidi.dll";
#endif

        [DllImport(DllName)]
        public static extern void rtmidi_open_port(
            RtMidiHandle device,
            uint portNumber,
            [MarshalAs(UnmanagedType.LPStr)] string portName
        );

        [DllImport(DllName)]
        public static extern void rtmidi_close_port(
            RtMidiHandle device
        );

        [DllImport(DllName)]
        public static extern uint rtmidi_get_port_count(
            RtMidiHandle device
        );

        [DllImport(DllName)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string rtmidi_get_port_name(
            RtMidiHandle device,
            uint portNumber
        );

        [DllImport(DllName)]
        public static extern RtMidiInHandle rtmidi_in_create_default();

        [DllImport(DllName)]
        public static extern void rtmidi_in_set_callback(
            RtMidiInHandle device,
            RtMidiCCallback callback,
            void* userData
        );

        [DllImport(DllName)]
        public static extern void rtmidi_in_cancel_callback(
            RtMidiInHandle device
        );

        [DllImport(DllName)]
        public static extern double rtmidi_in_get_message(
            RtMidiInHandle device,
            byte* message,
            ref UIntPtr size
        );

        [DllImport(DllName)]
        public static extern RtMidiOutHandle rtmidi_out_create_default();

        [DllImport(DllName)]
        public static extern int rtmidi_out_send_message(
            RtMidiOutHandle device,
            byte* message,
            int length
        );
    }
}
