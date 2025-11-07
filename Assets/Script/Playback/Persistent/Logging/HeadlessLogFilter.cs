using System;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Logging
{
    internal static class HeadlessLogFilter
    {
        private static readonly string[] ShaderFragments =
        {
            "shader is not supported on this GPU",
            "Shader Unsupported:",
            "Did you use #pragma only_renderers",
            "If subshaders removal was intentional"
        };

        private static bool _notified;

        public static bool ShouldSuppress(LogType type, string message)
        {
            if (type != LogType.Error && type != LogType.Warning)
            {
                return false;
            }

            foreach (var fragment in ShaderFragments)
            {
                if (message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!_notified)
                    {
                        _notified = true;
                        YargLogger.LogInfo("[DedicatedServer] Suppressing unsupported shader logs for headless mode.");
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
