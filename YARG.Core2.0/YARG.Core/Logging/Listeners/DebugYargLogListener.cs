using System.Diagnostics;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class DebugYargLogListener : BaseYargLogListener
    {
        public DebugYargLogListener() : this(new BasicYargLogFormatter())
        {
        }

        public DebugYargLogListener(IYargLogFormatter formatter) : base(formatter)
        {
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            // Unfortunately we have no choice but to take the allocation here :(
            // Debug.WriteLine only takes `object` or `string`
            Debug.WriteLine(output.ToString());
        }
    }
}