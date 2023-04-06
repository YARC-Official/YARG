using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.DTB
{
    public interface DTBItem
    {
        /// <summary>
        /// Used to designate variable type when writing DTB file.
        /// </summary>
        int NumericValue { get; }
    }
}
