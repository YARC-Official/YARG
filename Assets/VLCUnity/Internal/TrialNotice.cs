using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Handles all trial-related notifications:
/// - Editor: Shows a dialog when entering Play mode
/// - Runtime: Logs a warning to the console on startup
/// Only activates when running with a trial build of the native plugin.
/// </summary>
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public static class TrialNotice
{
#if !UNITY_EDITOR_WIN && (UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX)
    const string UnityPlugin = "libVLCUnityPlugin";
#elif UNITY_IOS
    const string UnityPlugin = "@rpath/VLCUnityPlugin.framework/VLCUnityPlugin";
#else
    const string UnityPlugin = "VLCUnityPlugin";
#endif

    [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_is_trial")]
    static extern bool IsTrial();

    private const string STORE_URL = "https://videolabs.io/store/unity/";

    private const string TRIAL_MESSAGE =
        "[VLC for Unity] TRIAL VERSION - 60 second playback limit per session, watermarked video output. Purchase full version: " + STORE_URL;

#if UNITY_EDITOR
    static TrialNotice()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && IsTrial())
        {
            bool openStore = EditorUtility.DisplayDialog(
                "VLC for Unity - Trial Version",
                "You are using the trial version of VLC for Unity.\n\n" +
                "Trial limitations:\n" +
                "- Watermark on video output\n" +
                "- 60 second playback limit per session\n\n" +
                "Purchase a license to remove all limitations.",
                "Purchase License",
                "Continue Trial"
            );

            if (openStore)
            {
                Application.OpenURL(STORE_URL);
            }
        }
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void LogTrialNotice()
    {
        if (IsTrial())
        {
            Debug.LogWarning(TRIAL_MESSAGE);
        }
    }
}
