using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Core.Song;

namespace YARG.Audio
{
    public interface IAudioManager
    {
        public AudioOptions Options { get; set; }

        public IList<string> SupportedFormats { get; }

        public bool IsAudioLoaded { get; }
        public bool IsPlaying { get; }
        public bool IsFadingOut { get; }

        public double MasterVolume { get; }
        public double SfxVolume { get; }

        public double CurrentPositionD { get; }
        public double AudioLengthD { get; }

        public float CurrentPositionF { get; }
        public float AudioLengthF { get; }

        public event Action SongEnd;

        public void Initialize();
        public void Unload();

        public IList<IMicDevice> GetAllInputDevices();

        public void LoadSfx();

        public void LoadSong(IDictionary<SongStem, string> stems, float speed);
        public void LoadMogg(Stream stream, List<MoggStemMap> stemMaps, float speed);
        public void LoadCustomAudioFile(string audioPath, float speed);
        public void UnloadSong();

        public void Play();
        public void Pause();

        public void FadeIn(float maxVolume);
        public UniTask FadeOut(CancellationToken token = default);

        public void PlaySoundEffect(SfxSample sample);

        public void SetStemVolume(SongStem stem, double volume);
        public void SetAllStemsVolume(double volume);

        public void UpdateVolumeSetting(SongStem stem, double volume);

        public double GetVolumeSetting(SongStem stem);

        public void ApplyReverb(SongStem stem, bool reverb);

        public void SetSpeed(float speed);
        public void SetWhammyPitch(SongStem stem, float percent);

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);

        public static void LoadAudio(IAudioManager manager, SongMetadata song, float speed, params SongStem[] ignoreStems)
        {
            if (song.IniData != null)
            {
                LoadIniAudio(manager, song.Directory, speed, ignoreStems);
            }
            else
            {
                LoadRBCONAudio(manager, song, speed, ignoreStems);
            }
        }

        public static async UniTask<bool> LoadPreviewAudio(IAudioManager manager, SongMetadata song, float speed)
        {
            if (song.IniData != null)
            {
                string directory = song.Directory;
                string previewBase = Path.Combine(directory, "preview");
                foreach (var ext in manager.SupportedFormats)
                {
                    string previewFile = previewBase + ext;
                    if (File.Exists(previewFile))
                    {
                        await UniTask.RunOnThreadPool(() => manager.LoadCustomAudioFile(previewFile, 1));
                        return true;
                    }
                }
                await UniTask.RunOnThreadPool(() => LoadIniAudio(manager, directory, speed, SongStem.Crowd));
            }
            else
                await UniTask.RunOnThreadPool(() => LoadRBCONAudio(manager, song, speed, SongStem.Crowd));
            return false;
        }

        private static void LoadIniAudio(IAudioManager manager, string directory, float speed, params SongStem[] ignoreStems)
        {
            var stems = AudioHelpers.GetSupportedStems(directory);
            foreach (var stem in ignoreStems)
                stems.Remove(stem);
            manager.LoadSong(stems, speed);
        }

        private static void LoadRBCONAudio(IAudioManager manager, SongMetadata song, float speed, params SongStem[] ignoreStems)
        {
            var rbmetadata = song.RBData.SharedMetadata;

            List<MoggStemMap> stemMaps = new();
            if (rbmetadata.DrumIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (rbmetadata.DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        stemMaps.Add(new(SongStem.Drums, rbmetadata.DrumIndices, rbmetadata.DrumStemValues));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..2], rbmetadata.DrumStemValues[2..4]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[3..5], rbmetadata.DrumStemValues[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..2], rbmetadata.DrumStemValues[0..4]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[4..6], rbmetadata.DrumStemValues[8..12]));
                        break;
                }
            }

            if (rbmetadata.BassIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Bass))
                stemMaps.Add(new(SongStem.Bass, rbmetadata.BassIndices, rbmetadata.BassStemValues));

            if (rbmetadata.GuitarIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Guitar))
                stemMaps.Add(new(SongStem.Guitar, rbmetadata.GuitarIndices, rbmetadata.GuitarStemValues));

            if (rbmetadata.KeysIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Keys))
                stemMaps.Add(new(SongStem.Keys, rbmetadata.KeysIndices, rbmetadata.KeysStemValues));

            if (rbmetadata.VocalsIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Vocals))
                stemMaps.Add(new(SongStem.Vocals, rbmetadata.VocalsIndices, rbmetadata.VocalsStemValues));

            if (rbmetadata.TrackIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Song))
                stemMaps.Add(new(SongStem.Song, rbmetadata.TrackIndices, rbmetadata.TrackStemValues));

            if (rbmetadata.CrowdIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Crowd))
                stemMaps.Add(new(SongStem.Crowd, rbmetadata.CrowdIndices, rbmetadata.CrowdStemValues));

            manager.LoadMogg(song.RBData.GetMoggStream(), stemMaps, speed);
        }
    }
}