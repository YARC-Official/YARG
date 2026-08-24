using System;
using ManagedBass;
using ManagedBass.Asio;
using UnityEditor;
using YARG.Audio.BASS;
using YARG.Core.Audio;

namespace YARG.Editor
{
    [InitializeOnLoad]
    public static class AudioDomainReloadHandler
    {
        static AudioDomainReloadHandler()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CleanupAudio;
            EditorApplication.quitting += CleanupAudio;
        }

        private static void CleanupAudio()
        {
            GlobalAudioHandler.Close();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                for (int asioIndex = 0; asioIndex < BassAsio.DeviceCount; asioIndex++)
                {
                    BassAsio.CurrentDevice = asioIndex;
                    BassAsio.Stop();
                    BassAsio.Free();
                }
            }
            catch
            {
            }
#endif

            try
            {
                for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var recordInfo); deviceIndex++)
                {
                    if (recordInfo.IsInitialized)
                    {
                        Bass.CurrentRecordingDevice = deviceIndex;
                        Bass.RecordFree();
                    }
                }
            }
            catch
            {
            }

            try
            {
                Bass.Free();
                Bass.PluginFree(0);
                BassOutputDevice.ResetForEditor();
            }
            catch
            {
            }
        }
    }
}
