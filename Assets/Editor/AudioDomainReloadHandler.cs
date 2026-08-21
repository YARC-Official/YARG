using System;
using ManagedBass;
using ManagedBass.Asio;
using ManagedBass.Wasapi;
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
                    try
                    {
                        BassAsio.CurrentDevice = asioIndex;
                        BassAsio.Stop();
                        BassAsio.Free();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            try
            {
                for (int wasapiIndex = 0; BassWasapi.GetDeviceInfo(wasapiIndex, out var wasapiInfo); wasapiIndex++)
                {
                    if (wasapiInfo.IsInitialized)
                    {
                        try
                        {
                            BassWasapi.CurrentDevice = wasapiIndex;
                            BassWasapi.Stop(true);
                            BassWasapi.Free();
                        }
                        catch
                        {
                        }
                    }
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
