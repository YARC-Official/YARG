using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Menu.HighwayConfiguration
{
    public interface IHighwayOrderingItemSpec
    {
        string Name { get; }
        int ColorIndex { get; }
        DrumsHighwayItemIconType Type { get; }
        Enum Value { get; }
        (Enum, Enum)? SplitsInto { get; }
        Enum? MergesInto { get; }
        Enum? MergedResult { get; }
    }
}
