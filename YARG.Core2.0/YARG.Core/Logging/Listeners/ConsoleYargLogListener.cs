using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class ConsoleYargLogListener : BaseYargLogListener
    {
        public ConsoleYargLogListener() : this(new BasicYargLogFormatter())
        {
        }

        public ConsoleYargLogListener(IYargLogFormatter formatter) : base(formatter)
        {
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            // Creates a new (stack allocated) array segment of the buffer
            // This can be passed to Console.WriteLine as a char[] overload (as Console doesn't have span writeline!?!?)
            // Avoids allocating a string

            var segment = output.AsArraySegment();
            Console.WriteLine(segment.Array!, segment.Offset, segment.Count);
        }
    }
}