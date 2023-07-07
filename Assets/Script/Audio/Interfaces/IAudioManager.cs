using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Song;

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
        public void LoadMogg(byte[] moggArray, List<(SongStem, int[], float[])> stemMaps, float speed);
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

        public void SetWhammyPitch(SongStem stem, float percent);

        public double GetPosition(bool desyncCompensation = true);
        public void SetPosition(double position, bool desyncCompensation = true);
    }
}