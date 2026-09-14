using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     BASS file callbacks for reading from a managed Stream. The callbacks
    ///     are static and find their stream through the User pointer: IL2CPP
    ///     cannot marshal instance-method delegates to native code.
    /// </summary>
    public class BassStreamProcedures : FileProcedures
    {
        private sealed class State
        {
            public Stream Stream;
            public long Start;
            public long Length;
            public int Released;
        }

        private readonly GCHandle _state;

        /// <summary>
        ///     Pass as the User argument of Bass.CreateStream so the static
        ///     callbacks can find their stream.
        /// </summary>
        public IntPtr User => GCHandle.ToIntPtr(_state);

        public BassStreamProcedures(Stream stream)
        {
            var state = new State
            {
                Stream = stream,
                Start = stream.Position,
            };
            state.Length = stream.Length - state.Start;
            _state = GCHandle.Alloc(state);

            Close = CloseCallback;
            Length = LengthCallback;
            Read = ReadCallback;
            Seek = SeekCallback;
        }

        /// <summary>
        ///     Frees the state when stream creation failed; BASS only invokes
        ///     the close callback for streams that were actually created.
        /// </summary>
        public void ReleaseOnFailure()
        {
            try
            {
                Release(User);
            }
            catch (InvalidOperationException)
            {
                // Already released through the close callback
            }
        }

        private static void Release(IntPtr user)
        {
            var handle = GCHandle.FromIntPtr(user);
            if (handle.Target is not State state ||
                Interlocked.Exchange(ref state.Released, 1) != 0)
            {
                return;
            }

            state.Stream.Close();
            handle.Free();
        }

        private static State GetState(IntPtr user) => (State) GCHandle.FromIntPtr(user).Target;

        [MonoPInvokeCallback(typeof(FileCloseProcedure))]
        private static void CloseCallback(IntPtr user)
        {
            try
            {
                Release(user);
            }
            catch
            {
                // Nothing sensible to do inside a native callback
            }
        }

        [MonoPInvokeCallback(typeof(FileLengthProcedure))]
        private static long LengthCallback(IntPtr user)
        {
            try
            {
                return GetState(user).Length;
            }
            catch
            {
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(FileReadProcedure))]
        private static int ReadCallback(IntPtr buffer, int length, IntPtr user)
        {
            try
            {
                unsafe
                {
                    return GetState(user).Stream.Read(new Span<byte>((byte*) buffer, length));
                }
            }
            catch
            {
                return 0;
            }
        }

        [MonoPInvokeCallback(typeof(FileSeekProcedure))]
        private static bool SeekCallback(long offset, IntPtr user)
        {
            try
            {
                var state = GetState(user);
                state.Stream.Seek(offset + state.Start, SeekOrigin.Begin);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
