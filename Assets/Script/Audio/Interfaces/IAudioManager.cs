using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;

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

        public double PlaybackBufferLength { get; }

        public double CurrentPositionD { get; }
        public double AudioLengthD { get; }

        public float CurrentPositionF { get; }
        public float AudioLengthF { get; }

        public event Action SongEnd;

        public void Initialize();
        public void Unload();

        public IList<IMicDevice> GetAllInputDevices();

        public void LoadSfx();

        public void LoadSong(Dictionary<SongStem, Stream> stems, float speed);
        public void LoadMogg(Stream stream, List<MoggStemMap> stemMaps, float speed);
        public void LoadCustomAudioFile(Stream stream, float speed);
        public void LoadCustomAudioFile(string file, float speed)
        {
            LoadCustomAudioFile(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1), speed);
        }

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

        public double GetPosition(bool bufferCompensation = true);
        public void SetPosition(double position, bool bufferCompensation = true);

        public int GetData(float[] buffer);

        public bool HasStem(SongStem stem);
    }
}