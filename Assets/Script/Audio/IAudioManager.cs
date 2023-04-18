using System.Collections.Generic;

namespace YARG {
	public interface IAudioManager {
		public bool UseStarpowerFx  { get; set; }
		public bool IsChipmunkSpeedup { get; set; }

		public IList<string> SupportedFormats { get; }

		public int StemsLoaded { get; }

		public bool IsAudioLoaded { get; }
		public bool IsPlaying { get; }

		public double CurrentPositionD { get; }
		public double AudioLengthD { get; }

		public float CurrentPositionF { get; }
		public float AudioLengthF { get; }

		public void Initialize();
		public void Unload();

		public void LoadSfx();

		public void LoadSong(IEnumerable<string> stems, bool isSpeedUp);
		public void UnloadSong();

		public void Play();
		public void Pause();

		public void PlaySoundEffect(SfxSample sample);

		public void SetStemVolume(SongStem stem, double volume);

		public void UpdateVolumeSetting(SongStem stem, double volume);

		public void ApplyReverb(SongStem stem, bool reverb);

		public double GetPosition();
		public void SetPosition(double position);
	}
}