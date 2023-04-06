using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.DTB
{
    public class IntegerItem : DTBItem
    {
        public IntegerItem() : this(0)
        {

        }

        public IntegerItem(int value)
        {
            Integer = value;
        }

        public int Integer { get; set; }

        public int NumericValue
        {
            get { return 0x00; }
        }
    }
}
