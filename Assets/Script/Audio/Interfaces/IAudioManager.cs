using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Song;

namespace YARG {
	public interface IAudioManager {
		public bool UseStarpowerFx { get; set; }
		public bool IsChipmunkSpeedup { get; set; }

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

		public IPreviewContext PreviewContext { get; }

		public void Initialize();
		public void Unload();

		public void LoadSfx();

		public void LoadSong(ICollection<string> stems, bool isSpeedUp, params SongStem[] ignoreStems);
		public void LoadMogg(ExtractedConSongEntry exConSong, bool isSpeedUp, params SongStem[] ignoreStems);
		public void LoadCustomAudioFile(string audioPath);
		public void UnloadSong();

		public void Play();
		public void Pause();

		public void DisposePreviewContext();

		public void FadeIn(float maxVolume);
		public UniTask FadeOut(CancellationToken token = default);

		public void PlaySoundEffect(SfxSample sample);

		public void SetStemVolume(SongStem stem, double volume);
		public void SetAllStemsVolume(double volume);

		public void UpdateVolumeSetting(SongStem stem, double volume);

		public double GetVolumeSetting(SongStem stem);

		public void ApplyReverb(SongStem stem, bool reverb);

		public double GetPosition();
		public void SetPosition(double position);
	}
}