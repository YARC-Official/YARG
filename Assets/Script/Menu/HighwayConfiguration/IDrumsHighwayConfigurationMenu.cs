using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Menu.HighwayConfiguration
{
    public interface IDrumsHighwayConfigurationMenu
    {
        void MoveItemRight(Enum item);
        void MoveItemLeft(Enum item);
        void MergeItemInto(Enum source, Enum target, Enum merged);
        void SplitItemInto(Enum source, (Enum, Enum) split);
    }
}
