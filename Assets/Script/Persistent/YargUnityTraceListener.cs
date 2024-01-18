using System;
using UnityEngine;
using YARG.Core;

namespace YARG
{
    public class YargUnityTraceListener : IYargTraceListener
    {
        public void LogMessage(YargTraceType type, string message)
        {
            switch (type)
            {
                case YargTraceType.Info:
                    Debug.Log(message);
                    break;
                case YargTraceType.Warning:
                    Debug.LogWarning(message);
                    break;
                case YargTraceType.Error:
                    Debug.LogError(message);
                    break;
                case YargTraceType.AssertFail:
                    Debug.Assert(false, message);
                    break;
            }
        }

        public void LogException(Exception ex, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Debug.LogError(message);

            Debug.LogException(ex);
        }
    }
}