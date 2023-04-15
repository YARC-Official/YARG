using System;
using System.Collections.Generic;
using System.IO;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Mix;
using UnityEngine;

namespace YARG {
	public class BassAudioManager : MonoBehaviour, IAudioManager {
		public IList<string> SupportedFormats { get; private set; }

		public int StemsLoaded { get; private set; }

		public bool IsAudioLoaded { get; private set; }
		public bool IsPlaying     { get; private set; }

		public double CurrentPositionD => GetPosition();
		public double AudioLengthD     { get; private set; }

		public float CurrentPositionF => (float) GetPosition();
		public float AudioLengthF     { get; private set; }

		private int opusHandle;
		private int mixerHandle;

		private int    leadChannelHandle;
		private double leadChannelLength;

		private int[] stemChannels;
		private int[] sfxSamples;

		private Dictionary<int, Dictionary<EffectType, int>> stemEffects;

		private void Awake() {
			SupportedFormats = new[] {
				".ogg",
				".mogg",
				".wav",
				".mp3",
				".aiff",
				".opus",
			};

			stemChannels = new int[AudioHelpers.SupportedStems.Count];
			sfxSamples = new int[AudioHelpers.SfxPaths.Count];
			stemEffects = new Dictionary<int, Dictionary<EffectType, int>>();

			opusHandle = 0;
			mixerHandle = 0;
		}

		public void Initialize() {
			Debug.Log("Initializing BASS...");
			string bassPath = GetBassDirectory();
			string opusLibDirectory = Path.Combine(bassPath, "bassopus");

			opusHandle = Bass.PluginLoad(opusLibDirectory);
			Bass.Configure(Configuration.IncludeDefaultDevice, true);

			Bass.UpdatePeriod = 5;
			Bass.DeviceBufferLength = 10;
			Bass.PlaybackBufferLength = 100;
			Bass.DeviceNonStop = true;

			Bass.Configure(Configuration.TruePlayPosition, 0);

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

			Bass.PluginFree(opusHandle);
			opusHandle = 0;
			
			// Free SFX samples
			foreach(int sample in sfxSamples) {
				if(sample != 0)
					Bass.SampleFree(sample);
			}

			Bass.Free();
		}

		public void LoadSfx() {
			Debug.Log("Loading SFX");

			sfxSamples = new int[AudioHelpers.SfxPaths.Count];

			string sfxFolder = Path.Combine(Application.streamingAssetsPath, "sfx");
			
			foreach (string sfx in AudioHelpers.SfxPaths) {
				string sfxPath = Path.Combine(sfxFolder, sfx);
				
				foreach(string format in SupportedFormats) {
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
				
				int sfxHandle = Bass.SampleLoad(sfxPath, 0, 0, 8, BassFlags.Decode);

				if (sfxHandle == 0) {
					Debug.LogError($"Failed to load SFX! {sfxPath}");
					Debug.LogError($"Bass Error: {Bass.LastError}");
					continue;
				}

				int sampleIndex = (int) AudioHelpers.GetSfxFromName(sfx);
				
				sfxSamples[sampleIndex] = sfxHandle;
				Debug.Log($"Loaded {sfx}");
			}
			
			Debug.Log("Finished loading SFX");
		}

		public void LoadSong(IEnumerable<string> stems) {
			UnloadSong();

			mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
			StemsLoaded = 0;

			foreach (string stemPath in stems) {
				string stemName = Path.GetFileNameWithoutExtension(stemPath);
				int stemIndex = (int) AudioHelpers.GetStemFromName(stemName);

				if (stemChannels[stemIndex] != 0) {
					Debug.LogError($"Stem already loaded! {stemPath}");
					continue;
				}

				int streamHandle = Bass.CreateStream(stemPath, 0, 0, BassFlags.Decode);

				// Stream failed to load
				if (streamHandle == 0) {
					Debug.LogError($"Failed to load stem! {stemPath}");
					Debug.LogError($"Bass Error: {Bass.LastError}");
					continue;
				}

				double stemLength = GetAudioLengthInSeconds(streamHandle);
				if (stemLength > leadChannelLength) {
					leadChannelHandle = streamHandle;
					leadChannelLength = stemLength;
				}

				stemChannels[stemIndex] = streamHandle;
				StemsLoaded++;
				
				stemEffects.Add(streamHandle, new Dictionary<EffectType, int>());

				//Debug.Log($"Loaded stem {stemPath}");

				BassMix.MixerAddChannel(mixerHandle, streamHandle, BassFlags.Default);
			}

			Debug.Log($"Loaded {StemsLoaded} stems");

			// Setup audio length
			AudioLengthD = leadChannelLength;
			AudioLengthF = (float) AudioLengthD;

			IsAudioLoaded = true;
		}

		public void UnloadSong() {
			IsPlaying = false;
			IsAudioLoaded = false;
			StemsLoaded = 0;

			// Free mixer stream
			if (mixerHandle != 0) {
				Bass.StreamFree(mixerHandle);
			}

			// Free all stem channels and reset the array
			for (int i = 0; i < stemChannels.Length; i++) {
				int stemHandle = stemChannels[i];
				stemChannels[i] = 0;

				Bass.StreamFree(stemHandle);
			}
		}

		public void Play() {
			// Don't try to play if there's no audio loaded or if it's already playing
			if (!IsAudioLoaded || IsPlaying) {
				return;
			}

			// Playing mixer stream plays all channels
			Bass.ChannelPlay(mixerHandle);
			IsPlaying = true;
		}

		public void Pause() {
			if (!IsAudioLoaded || !IsPlaying) {
				return;
			}

			// Pausing mixer stream pauses all channels
			Bass.ChannelPause(mixerHandle);
			IsPlaying = false;
		}

		public void PlaySoundEffect(SfxSample sample) {
			if(sfxSamples[(int) sample] == 0)
				return;
			
			int channel = Bass.SampleGetChannel(sfxSamples[(int) sample]);
			Bass.ChannelPlay(channel);
		}

		public void SetStemVolume(SongStem stem, double volume) {
			int stemIndex = (int) stem;
			int stemHandle = stemChannels[stemIndex];

			// If handle is 0 then it's not loaded
			if (stemHandle == 0) {
				return;
			}

			Bass.ChannelSetAttribute(stemHandle, ChannelAttribute.Volume, volume);
		}

		public void ApplyReverb(SongStem stem, bool reverb) {
			int stemIndex = (int) stem;
			int stemHandle = stemChannels[stemIndex];

			// If handle is 0 then it's not loaded
			if (stemHandle == 0) {
				return;
			}

			if (reverb) {
				// Reverb already applied
				if(stemEffects[stemHandle].ContainsKey(EffectType.DXReverb))
					return;
				
				// Set reverb FX
				int reverbHandle = Bass.ChannelSetFX(stemHandle, EffectType.DXReverb, 0);
				
				var reverbParams = new DXReverbParameters {
					fInGain = 0.0f,
					fReverbMix = -4f,
					fReverbTime = 1000.0f,
					fHighFreqRTRatio = 0.001f
				};
				
				// I don't think the volume works??? Might need a gain DSP function instead
				Bass.ChannelSetAttribute(stemHandle, ChannelAttribute.Volume, 1.5f);
				
				Bass.FXSetParameters(reverbHandle, reverbParams);
				
				stemEffects[stemHandle].Add(EffectType.DXReverb, reverbHandle);
			} else {
				// No reverb is applied
				if (!stemEffects[stemHandle].ContainsKey(EffectType.DXReverb))
					return;
				
				Bass.ChannelRemoveFX(stemHandle, stemEffects[stemHandle][EffectType.DXReverb]);
				
				// Should set volume back to stem volume in settings when that is added
				Bass.ChannelSetAttribute(stemHandle, ChannelAttribute.Volume, 1f);
				
				stemEffects[stemHandle].Remove(EffectType.DXReverb);
			}
		}

		public double GetPosition() {
			return Bass.ChannelBytes2Seconds(leadChannelHandle, Bass.ChannelGetPosition(leadChannelHandle));
		}

		public void SetPosition(double position) {
			throw new System.NotImplementedException();
		}

		private double GetAudioLengthInSeconds(int channel) {
			long length = Bass.ChannelGetLength(channel);
			double seconds = Bass.ChannelBytes2Seconds(channel, length);
			return seconds;
		}

		private void OnApplicationQuit() {
			Unload();
		}

		private static string GetBassDirectory() {
			string pluginDirectory = Path.Combine(Application.dataPath, "Plugins");

			// Locate windows directory
			// Checks if running on 64 bit and sets the path accordingly
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
#if UNITY_64
			pluginDirectory = Path.Combine(pluginPath, "x86_64");
#else
			pluginDirectory = Path.Combine(pluginPath, "x86");
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
			pluginDirectory = Path.Combine(path, "Mac");
#elif UNITY_EDITOR_LINUX
			pluginDirectory = Path.Combine(path, "Linux/x86_64");
#endif

			return pluginDirectory;
		}
	}
}