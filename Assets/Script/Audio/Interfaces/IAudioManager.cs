using System.Collections.Generic;
using YARG.Serialization;

namespace YARG {
	public interface IAudioManager {
		public bool UseStarpowerFx  { get; set; }
		public bool IsChipmunkSpeedup { get; set; }

		public IList<string> SupportedFormats { get; }

		public bool IsAudioLoaded { get; }
		public bool IsPlaying { get; }
		
		public double MasterVolume { get; }
		public double SfxVolume { get; }

		public double CurrentPositionD { get; }
		public double AudioLengthD { get; }

		public float CurrentPositionF { get; }
		public float AudioLengthF { get; }

		public void Initialize();
		public void Unload();

		public void LoadSfx();

		public void LoadSong(ICollection<string> stems, bool isSpeedUp);
		public void LoadMogg(XboxMoggData moggData, bool isSpeedUp);
		public void UnloadSong();

		public void Play();
		public void Pause();

		public void PlaySoundEffect(SfxSample sample);

		public void SetStemVolume(SongStem stem, double volume);

		public void UpdateVolumeSetting(SongStem stem, double volume);
		
		public double GetVolumeSetting(SongStem stem);

		public void ApplyReverb(SongStem stem, bool reverb);

		public double GetPosition();
		public void SetPosition(double position);
	}
}