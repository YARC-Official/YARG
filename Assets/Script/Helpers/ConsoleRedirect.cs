using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

using SystemDebug = System.Diagnostics.Debug;
using UnityDebug = UnityEngine.Debug;

namespace YARG.Util
{
    /// <summary>
    /// Redirects writes to System.Console to Unity3D's Debug.Log.
    /// </summary>
    /// <author>
    /// Jackson Dunstan, http://jacksondunstan.com/articles/2986
    /// </author>
    public static class ConsoleRedirect
    {
        private class UnityTextWriter : TextWriter
        {
            private StringBuilder buffer = new(1024);

            public override void Flush()
            {
                UnityDebug.Log(buffer.ToString());
                buffer.Length = 0;
            }

            public override void Write(string value)
            {
                buffer.Append(value);
                if (value != null)
                {
                    var len = value.Length;
                    if (len > 0)
                    {
                        var lastChar = value [len - 1];
                        if (lastChar == '\n')
                        {
                            Flush();
                        }
                    }
                }
            }

            public override void Write(char value)
            {
                buffer.Append(value);
                if (value == '\n')
                {
                    Flush();
                }
            }

            public override void Write(char[] value, int index, int count)
            {
                Write(new string (value, index, count));
            }

            public override Encoding Encoding
            {
                get { return Encoding.Default; }
            }
        }

        private class UnityTraceListener : TraceListener
        {
            private StringBuilder buffer = new(1024);

            public override bool IsThreadSafe => false;

            public override void Write(string message)
            {
                buffer.Append(message);
                if (message.Contains('\n'))
                {
                    UnityDebug.Log(buffer.ToString());
                    buffer.Length = 0;
                }
            }

            public override void WriteLine(string message)
            {
                UnityDebug.Log(message);
            }

            public override void Fail(string message)
            {
                UnityDebug.Assert(false, message);
            }

            public override void Fail(string message, string detailMessage)
            {
                if (string.IsNullOrEmpty(detailMessage))
                    UnityDebug.Assert(false, message);
                else
                    UnityDebug.Assert(false, $"{message}\n{detailMessage}");
            }
        }

        public static void Redirect()
        {
            var consoleRedirect = new UnityTextWriter();
            var traceRedirect = new UnityTraceListener();

            Console.SetOut(consoleRedirect);
            Trace.Listeners.Add(traceRedirect);
            SystemDebug.Listeners.Add(traceRedirect);
        }
    }
}