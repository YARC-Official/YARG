using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using UnityEngine;
using XboxSTFS;
using static XboxSTFS.XboxSTFSParser;
using YARG.Serialization;
using YARG.Song;
using Debug = UnityEngine.Debug;

namespace YARG {
	public class BassAudioManager : MonoBehaviour, IAudioManager {
		public bool UseStarpowerFx { get; set; }
		public bool IsChipmunkSpeedup { get; set; }

		public IList<string> SupportedFormats { get; private set; }

		public bool IsAudioLoaded { get; private set; }
		public bool IsPlaying { get; private set; }
		public bool IsFadingOut { get; private set; }

		public double MasterVolume { get; private set; }
		public double SfxVolume { get; private set; }

		public double CurrentPositionD => GetPosition();
		public double AudioLengthD { get; private set; }

		public float CurrentPositionF => (float) GetPosition();
		public float AudioLengthF { get; private set; }

		private IPreviewContext _currentPreviewContext;
		public IPreviewContext PreviewContext {
			get {
				_currentPreviewContext ??= new BassPreviewContext(this);
				return _currentPreviewContext;
			}
		}

		private double[] _stemVolumes;

		private int _opusHandle;

		private IStemMixer _mixer;

		private ISampleChannel[] _sfxSamples;

		private void Awake() {
			SupportedFormats = new[] {
				".ogg",
				".mogg",
				".wav",
				".mp3",
				".aiff",
				".opus",
			};

			_stemVolumes = new double[AudioHelpers.SupportedStems.Count];

			_sfxSamples = new ISampleChannel[AudioHelpers.SfxPaths.Count];

			_opusHandle = 0;
		}

		public void Initialize() {
			Debug.Log("Initializing BASS...");
			string bassPath = GetBassDirectory();
			string opusLibDirectory = Path.Combine(bassPath, "bassopus");

			_opusHandle = Bass.PluginLoad(opusLibDirectory);
			Bass.Configure(Configuration.IncludeDefaultDevice, true);

			Bass.UpdatePeriod = 5;
			Bass.DeviceBufferLength = 10;
			Bass.PlaybackBufferLength = 100;
			Bass.DeviceNonStop = true;

			Bass.Configure(Configuration.TruePlayPosition, 0);
			Bass.Configure(Configuration.UpdateThreads, 2);
			Bass.Configure(Configuration.FloatDSP, true);

			Bass.Configure((Configuration) 68, 1);
			Bass.Configure((Configuration) 70, false);

			int deviceCount = Bass.DeviceCount;
			Debug.Log($"Devices found: {deviceCount}");

			if (!Bass.Init(-1, 44100, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero)) {
				Debug.LogError("Failed to initialize BASS");
				Debug.LogError($"Bass Error: {Bass.LastError}");
				return;
			}

			LoadSfx();

			Debug.Log($"BASS Successfully Initialized");
			Debug.Log($"BASS: {Bass.Version}");
			Debug.Log($"BASS.FX: {Bass.Version}");
			Debug.Log($"BASS.Mix: {Bass.Version}");

			Debug.Log($"Update Period: {Bass.UpdatePeriod}");
			Debug.Log($"Device Buffer Length: {Bass.DeviceBufferLength}");
			Debug.Log($"Playback Buffer Length: {Bass.PlaybackBufferLength}");

			Debug.Log($"Current Device: {Bass.GetDeviceInfo(Bass.CurrentDevice).Name}");
		}

		public void Unload() {
			Debug.Log("Unloading BASS plugins");

			UnloadSong();

			Bass.PluginFree(_opusHandle);
			_opusHandle = 0;

			// Free SFX samples
			foreach (var sample in _sfxSamples) {
				sample?.Dispose();
			}

			Bass.Free();
		}

		public void LoadSfx() {
			Debug.Log("Loading SFX");

			_sfxSamples = new ISampleChannel[AudioHelpers.SfxPaths.Count];

			string sfxFolder = Path.Combine(Application.streamingAssetsPath, "sfx");

			foreach (string sfxFile in AudioHelpers.SfxPaths) {
				string sfxPath = Path.Combine(sfxFolder, sfxFile);

				foreach (string format in SupportedFormats) {
					if (!File.Exists($"{sfxPath}{format}"))
						continue;

					// Append extension to path (e.g sfx/boop becomes sfx/boop.ogg)
					sfxPath += format;
					break;
				}


				if (!File.Exists(sfxPath)) {
					Debug.LogError($"SFX path does not exist! {sfxPath}");
					continue;
				}
				var sfxSample = AudioHelpers.GetSfxFromName(sfxFile);

				var sfx = new BassSampleChannel(this, sfxPath, 8, sfxSample);
				if (sfx.Load() != 0) {
					Debug.LogError($"Failed to load SFX! {sfxPath}");
					Debug.LogError($"Bass Error: {Bass.LastError}");
					continue;
				}

				_sfxSamples[(int) sfxSample] = sfx;
				Debug.Log($"Loaded {sfxFile}");
			}

			Debug.Log("Finished loading SFX");
		}

		public void LoadSong(ICollection<string> stems, bool isSpeedUp, params SongStem[] ignoreStems) {
			Debug.Log("Loading song");
			UnloadSong();

			_mixer = new BassStemMixer(this);
			if (!_mixer.Create()) {
				throw new Exception($"Failed to create mixer: {Bass.LastError}");
			}

			foreach (string stemPath in stems) {
				// Gets the file name with no extensions (i.e guitar.ogg -> guitar)
				string stemName = Path.GetFileNameWithoutExtension(stemPath);

				// Gets the index (SongStem to int) from the name
				var songStem = AudioHelpers.GetStemFromName(stemName);

				// Skip stems specified in ignore stems parameter
				if (ignoreStems.Contains(songStem)) {
					continue;
				}

				// Assign 1 stem songs to the song stem
				if (stems.Count == 1) {
					songStem = SongStem.Song;
				}

				var stemChannel = new BassStemChannel(this, stemPath, songStem);
				if (stemChannel.Load(isSpeedUp, PlayMode.Play.speed) != 0) {
					Debug.LogError($"Failed to load stem! {stemPath}");
					Debug.LogError($"Bass Error: {Bass.LastError}");
					continue;
				}

				if (_mixer.GetChannel(songStem) != null) {
					Debug.LogError($"Stem already loaded! {stemPath}");
					continue;
				}

				if (_mixer.AddChannel(stemChannel) != 0) {
					Debug.LogError($"Failed to add stem to mixer!");
					Debug.LogError($"Bass Error: {Bass.LastError}");
				}
			}

			Debug.Log($"Loaded {_mixer.StemsLoaded} stems");

			// Setup audio length
			AudioLengthD = _mixer.LeadChannel.LengthD;
			AudioLengthF = (float) AudioLengthD;

			IsAudioLoaded = true;
		}

		public void LoadMogg(ExtractedConSongEntry exConSong, bool isSpeedUp, params SongStem[] ignoreStems) {
			Debug.Log("Loading mogg song");
			UnloadSong();

			byte[] moggArray;
			if (exConSong is ConSongEntry conSong) {
				if (!conSong.UsingUpdateMogg)
					moggArray = XboxSTFSParser.GetFile(conSong.Location, conSong.FLMogg)[conSong.MoggAddressAudioOffset..];
				else moggArray = File.ReadAllBytes(conSong.MoggPath)[conSong.MoggAddressAudioOffset..];
			} else {
				moggArray = File.ReadAllBytes(exConSong.MoggPath)[exConSong.MoggAddressAudioOffset..];
			}

			int moggStreamHandle = Bass.CreateStream(moggArray, 0, moggArray.Length, BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile);
			if (moggStreamHandle == 0) {
				Debug.LogError($"Failed to load mogg file or position: {Bass.LastError}");
				return;
			}

			// Remove ignored stems from stem maps
			var stems = exConSong.StemMaps.Keys.Where(ignoreStems.Contains).ToList();
			foreach (var stem in stems) {
				exConSong.StemMaps.Remove(stem);
			}

			_mixer = new BassStemMixer(this, moggStreamHandle, exConSong.StemMaps, exConSong.MatrixRatios);
			if (!_mixer.Create()) {
				throw new Exception($"Failed to create mixer: {Bass.LastError}");
			}

			if (!_mixer.SetupMogg(isSpeedUp)) {
				throw new Exception($"Failed to setup MOGG channels: {Bass.LastError}");
			}

			Debug.Log($"Loaded {_mixer.StemsLoaded} stems");

			// Setup audio length
			AudioLengthD = _mixer.LeadChannel.LengthD;
			AudioLengthF = (float) AudioLengthD;

			IsAudioLoaded = true;
		}

		public void UnloadSong() {
			IsPlaying = false;
			IsAudioLoaded = false;

			// Free mixer (and all channels in it)
			_mixer?.Dispose();
			_mixer = null;
		}

		public void Play() => Play(false);

		private void Play(bool fadeIn) {
			// Don't try to play if there's no audio loaded or if it's already playing
			if (!IsAudioLoaded || IsPlaying) {
				return;
			}

			foreach (var channel in _mixer.Channels.Values) {
				if (fadeIn) {
					channel.SetVolume(0);
				} else {
					channel.SetVolume(channel.Volume);
				}
			}
			if (_mixer.Play() != 0) {
				Debug.Log($"Play error: {Bass.LastError}");
			}

			IsPlaying = _mixer.IsPlaying;
		}

		public void Pause() {
			if (!IsAudioLoaded || !IsPlaying) {
				return;
			}

			if (_mixer.Pause() != 0) {
				Debug.Log($"Pause error: {Bass.LastError}");
			}

			IsPlaying = _mixer.IsPlaying;
		}

		public void DisposePreviewContext() {
			_currentPreviewContext?.Dispose();
			_currentPreviewContext = null;
		}

		public void FadeIn(float maxVolume) {
			Play(true);
			if (IsPlaying) {
				_mixer?.FadeIn(maxVolume);
			}
		}

		public async UniTask FadeOut(CancellationToken token = default) {
			if (IsPlaying) {
				IsFadingOut = true;
				await _mixer.FadeOut(token);
				IsFadingOut = false;
			}
		}

		public void PlaySoundEffect(SfxSample sample) {
			var sfx = _sfxSamples[(int) sample];

			sfx?.Play();
		}

		public void SetStemVolume(SongStem stem, double volume) {
			var channel = _mixer?.GetChannel(stem);

			channel?.SetVolume(volume);
		}

		public void SetAllStemsVolume(double volume) {
			if (_mixer == null) {
				return;
			}

			foreach (var (_, channel) in _mixer.Channels) {
				channel.SetVolume(volume);
			}
		}

		public void UpdateVolumeSetting(SongStem stem, double volume) {
			switch (stem) {
				case SongStem.Master:
					MasterVolume = volume;
					Bass.GlobalStreamVolume = (int) (10_000 * MasterVolume);
					break;
				case SongStem.Sfx:
					SfxVolume = volume;
					break;
				default:
					_stemVolumes[(int) stem] = volume * BassHelpers.SONG_VOLUME_MULTIPLIER;
					break;
			}
		}

		public double GetVolumeSetting(SongStem stem) {
			return stem switch {
				SongStem.Master => MasterVolume,
				SongStem.Sfx => SfxVolume,
				_ => _stemVolumes[(int) stem]
			};
		}

		public void ApplyReverb(SongStem stem, bool reverb) => _mixer?.GetChannel(stem)?.SetReverb(reverb);

		public double GetPosition() {
			if (_mixer is null)
				return -1;

			return _mixer.GetPosition();
		}

		public void SetPosition(double position) => _mixer?.SetPosition(position);

		private void OnApplicationQuit() {
			Unload();
		}

		private static string GetBassDirectory() {
			string pluginDirectory = Path.Combine(Application.dataPath, "Plugins");

			// Locate windows directory
			// Checks if running on 64 bit and sets the path accordingly
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
#if UNITY_64
			pluginDirectory = Path.Combine(pluginDirectory, "x86_64");
#else
			pluginDirectory = Path.Combine(pluginDirectory, "x86");
#endif
#endif

			// Unity Editor directory, Assets/Plugins/Bass/
#if UNITY_EDITOR
			pluginDirectory = Path.Combine(pluginDirectory, "BassNative");
#endif

			// Editor paths differ to standalone paths, as the project contains platform specific folders
#if UNITY_EDITOR_WIN
			pluginDirectory = Path.Combine(pluginDirectory, "Windows/x86_64");
#elif UNITY_EDITOR_OSX
			pluginDirectory = Path.Combine(pluginDirectory, "Mac");
#elif UNITY_EDITOR_LINUX
			pluginDirectory = Path.Combine(pluginDirectory, "Linux/x86_64");
#endif

			return pluginDirectory;
		}
	}
}