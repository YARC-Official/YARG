using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Venue
{
    [Flags]
    public enum BackgroundType
    {
        Yarground = 1 << 0,
        Video = 1 << 1,
        Image = 1 << 2,
    }
}
