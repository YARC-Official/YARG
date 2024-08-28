using System;
using System.Runtime.InteropServices;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;

namespace STB2CSharp
{
    public unsafe class StbImageDecoder : IImageDecoder
    {
        private class StbImageArray : FixedArray<byte>
        {
            public StbImageArray(byte* ptr, long length) : base(ptr, length) { }

            protected override void DisposeUnmanaged()
            {
                FreeNative(Ptr);
            }
        }

        public unsafe FixedArray<byte> Decode(byte* data, int length, out int width, out int height, out ImageFormat format)
        {
            byte* loaded = LoadNative(data, length, out width, out height, out int components);
            if (loaded == null)
            {
                format = default;
                return null;
            }

            format = components switch
            {
                1 => ImageFormat.Grayscale,
                2 => ImageFormat.Grayscale_Alpha,
                3 => ImageFormat.RGB,
                4 => ImageFormat.RGBA,
                _ => throw new NotImplementedException($"Unhandled components value {components}")
            };

            return new StbImageArray(loaded, width * height * components);
        }

        [DllImport("STB2CSharp", EntryPoint = "load_image_from_memory")]
        private static extern byte* LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp", EntryPoint = "free_image")]
        private static extern void FreeNative(byte* image);
    }
}