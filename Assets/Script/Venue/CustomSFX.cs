using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Venue
{
    public class CustomSFX : MonoSingleton<CustomSFX>
    {
        private static readonly HashSet<string> ClipNames              = new();
        [SerializeField]
        private                 bool            disableBuiltinCrowdSfx = false;

        protected override void SingletonAwake()
        {
            GlobalVariables.State.CrowdSfxVenueOverride = disableBuiltinCrowdSfx;
        }

        public void PlayClip(string clipName)
        {
            if (!ClipNames.Contains(clipName))
            {
                YargLogger.LogFormatError("Venue asked to play sfx clip {0}, but it does not exist!", clipName);
                return;
            }

            GlobalAudioHandler.PlayVenueSample(clipName);
        }

        public static void AddClips(Dictionary<string, byte[]> clipData)
        {
            GlobalAudioHandler.ClearVenueSamples();

            string extension = ".bytes";
            foreach (var clip in clipData)
            {
                string name;
                if (clip.Key.EndsWith(extension))
                {
                    name = Path.GetFileNameWithoutExtension(clip.Key[..extension.Length]);
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(clip.Key);
                }

                if (ClipNames.Add(name))
                {
                    GlobalAudioHandler.AddVenueSample(name, clip.Value);
                }
            }
        }

        protected override void SingletonDestroy()
        {
            ClipNames.Clear();
            GlobalAudioHandler.ClearVenueSamples();
            GlobalVariables.State.CrowdSfxVenueOverride = false;
        }
    }
}