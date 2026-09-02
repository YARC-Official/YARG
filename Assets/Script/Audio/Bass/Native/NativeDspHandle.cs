#nullable enable
using System;
using Microsoft.Win32.SafeHandles;

namespace YARG.Audio.BASS.Native
{
    public abstract class NativeDspHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        protected NativeDspHandle() : base(true)
        {
        }

        protected abstract void Destroy(IntPtr handle);

        protected override bool ReleaseHandle()
        {
            Destroy(handle);
            return true;
        }
    }
}
