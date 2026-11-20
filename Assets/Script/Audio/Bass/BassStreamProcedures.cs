using System;
using System.IO;
using ManagedBass;

#if UNITY_ANDROID
using System.Runtime.InteropServices;
using AOT;
#endif

namespace YARG.Audio.BASS
{
    public class BassStreamProcedures : FileProcedures
    {
        private readonly Stream _stream;
        private readonly long _start;
        private readonly long _length;

        public BassStreamProcedures(Stream stream)
        {
            _stream = stream;
            _start = stream.Position;
            _length = stream.Length - _start;

#if UNITY_ANDROID
            // IL2CPP cannot marshal instance delegates to native code, so the
            // file callbacks must be static methods (see the Static* methods
            // below). The owning instance is recovered from the user pointer
            // BASS passes back to each callback. The GCHandle itself is
            // allocated by the caller (BassAudioManager.CreateSourceStream)
            // and freed by StaticClose when BASS closes the stream.
            Close = StaticClose;
            Length = StaticLength;
            Read = StaticRead;
            Seek = StaticSeek;
#else
            Close = (IntPtr) => _stream.Close();
            Length = (IntPtr) => _length;
            Read = (IntPtr Buffer, int Length, IntPtr User) =>
            {
                try
                {
                    unsafe
                    {
                        return _stream.Read(new Span<byte>((byte*) Buffer, Length));
                    }
                }
                catch
                {
                    return 0;
                }
            };

            Seek = (long Offset, IntPtr User) =>
            {
                try
                {
                    _stream.Seek(Offset + _start, SeekOrigin.Begin);
                    return true;
                }
                catch
                {
                    return false;
                }
            };
#endif
        }

#if UNITY_ANDROID
        [MonoPInvokeCallback(typeof(FileCloseProcedure))]
        private static void StaticClose(IntPtr user)
        {
            GCHandle handle = default(GCHandle);
            try
            {
                handle = GCHandle.FromIntPtr(user);
                var procs = (BassStreamProcedures) handle.Target;
                procs._stream.Close();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[BassStreamProcedures] Error in StaticClose: " + e.Message);
            }
            finally
            {
                // The handle was allocated by the caller when the stream was
                // created. BASS calls the close procedure exactly once when the
                // stream is freed, so this is the single place it is freed.
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        [MonoPInvokeCallback(typeof(FileLengthProcedure))]
        private static long StaticLength(IntPtr user)
        {
            try
            {
                var procs = (BassStreamProcedures) GCHandle.FromIntPtr(user).Target;
                return procs._length;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[BassStreamProcedures] Error in StaticLength: " + e.Message);
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(FileReadProcedure))]
        private static int StaticRead(IntPtr buffer, int length, IntPtr user)
        {
            try
            {
                var procs = (BassStreamProcedures) GCHandle.FromIntPtr(user).Target;
                unsafe
                {
                    return procs._stream.Read(new Span<byte>((byte*) buffer, length));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[BassStreamProcedures] Error in StaticRead: " + e.Message);
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(FileSeekProcedure))]
        private static bool StaticSeek(long offset, IntPtr user)
        {
            try
            {
                var procs = (BassStreamProcedures) GCHandle.FromIntPtr(user).Target;
                procs._stream.Seek(offset + procs._start, SeekOrigin.Begin);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[BassStreamProcedures] Error in StaticSeek: " + e.Message);
                return false;
            }
        }
#endif
    }
}
