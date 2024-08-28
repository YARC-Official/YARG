using UnityEngine;
using YARG.Logging;

namespace YARG
{
    /// <summary>
    /// Handles any general initialization that YARG.Core requires.
    /// </summary>
    [DefaultExecutionOrder(-4000)]
    public static class CoreInitialization
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize()
        {
            Application.quitting += Uninitialize;

            LogHandler.Initialize();
        }

        private static void Uninitialize()
        {
            Application.quitting -= Uninitialize;

            LogHandler.Uninitialize();
        }
    }
}