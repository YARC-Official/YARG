using System;
using System.Collections.Generic;
using System.Text;
using Mackiloha.IO;

namespace Mackiloha.App.Metadata
{
    public enum TexEncoding
    {
        Bitmap,
        DXT1,
        DXT5,
        ATI2
    }

    public struct TexMeta
    {
        public TexEncoding? Encoding { get; set; }
        public bool MipMaps { get; set; }

        public static TexMeta DefaultFor(Platform platform)
        {
            switch (platform)
            {
                case Platform.PS3:
                case Platform.X360:
                    return new TexMeta()
                    {
                        Encoding = TexEncoding.DXT1,
                        MipMaps = true
                    };
                default:
                    return new TexMeta()
                    {
                        Encoding = TexEncoding.Bitmap,
                        MipMaps = false
                    };
            }
        }
    }
}
