#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using YARG.Audio.BASS.Effects;
using YARG.Helpers;

namespace YARG.Audio.BASS.Native
{
    public static class YargAudioBindings
    {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;
#endif

        private static IntPtr _libraryHandle = IntPtr.Zero;
        private static string? _loadedPath;

        private static GetAbiVersionDelegate? _getAbiVersion;
        private static GainDspAttachDelegate? _gainDspAttach;
        private static GainDspSetGainDelegate? _gainDspSetGain;
        private static GainDspDestroyDelegate? _gainDspDestroy;
        private static FreeverbDspAttachDelegate? _freeverbDspAttach;
        private static FreeverbDspResetDelegate? _freeverbDspReset;
        private static FreeverbDspSetParamsDelegate? _freeverbDspSetParams;
        private static FreeverbDspDestroyDelegate? _freeverbDspDestroy;
        private static DattorroReverbDspAttachDelegate? _dattorroReverbDspAttach;
        private static DattorroReverbDspResetDelegate? _dattorroReverbDspReset;
        private static DattorroReverbDspSetParamsDelegate? _dattorroReverbDspSetParams;
        private static DattorroReverbDspDestroyDelegate? _dattorroReverbDspDestroy;
        private static NoiseGateDspAttachDelegate? _noiseGateDspAttach;
        private static NoiseGateDspResetDelegate? _noiseGateDspReset;
        private static NoiseGateDspSetParamsDelegate? _noiseGateDspSetParams;
        private static NoiseGateDspDestroyDelegate? _noiseGateDspDestroy;
        private static OneShotStreamCreateDelegate? _oneShotStreamCreate;
        private static OneShotStreamAttachDelegate? _oneShotStreamAttach;
        private static OneShotStreamResyncDelegate? _oneShotStreamResync;
        private static OneShotStreamSetPausedDelegate? _oneShotStreamSetPaused;
        private static OneShotStreamSetGainDelegate? _oneShotStreamSetGain;
        private static OneShotStreamDetachDelegate? _oneShotStreamDetach;
        private static OneShotStreamDestroyDelegate? _oneShotStreamDestroy;
        private static SineSynthDspCreateDelegate? _sineSynthDspCreate;
        private static SineSynthDspAttachDelegate? _sineSynthDspAttach;
        private static SineSynthDspDetachDelegate? _sineSynthDspDetach;
        private static SineSynthDspSetScheduleDelegate? _sineSynthDspSetSchedule;
        private static SineSynthDspSetTimingDelegate? _sineSynthDspSetTiming;
        private static SineSynthDspSetOutputChannelDelegate? _sineSynthDspSetOutputChannel;
        private static SineSynthDspDestroyDelegate? _sineSynthDspDestroy;
        private static ReadAheadStreamCreateDelegate? _readAheadStreamCreate;
        private static ReadAheadStreamSetCallbackClockDelegate? _readAheadStreamSetCallbackClock;
        private static ReadAheadStreamPrefillDelegate? _readAheadStreamPrefill;
        private static ReadAheadStreamFlushDelegate? _readAheadStreamFlush;
        private static ReadAheadStreamSetBufferLengthDelegate? _readAheadStreamSetBufferLength;
        private static ReadAheadStreamGetSourcePositionDelegate? _readAheadStreamGetSourcePosition;
        private static ReadAheadStreamGetPositionSnapshotDelegate? _readAheadStreamGetPositionSnapshot;
        private static ReadAheadStreamGetStatsDelegate? _readAheadStreamGetStats;
        private static ReadAheadStreamDestroyDelegate? _readAheadStreamDestroy;

        static YargAudioBindings()
        {
            EnsureLoaded();
        }

        public static void Reload()
        {
            _libraryHandle = IntPtr.Zero;
            _loadedPath = null;
            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
            var libraryPath = GetLibraryPath();
            if (_libraryHandle != IntPtr.Zero && string.Equals(_loadedPath, libraryPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var handle = LoadNativeLibrary(libraryPath);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            _libraryHandle = handle;
            _loadedPath = libraryPath;
            BindAll(handle);
        }

        internal static uint GetAbiVersion() =>
            EnsureBound(ref _getAbiVersion, "yarg_audio_get_abi_version")();

        internal static int GainDspAttach(uint channel, float gain, int priority, out BassGainDsp dsp, out int bassError) =>
            EnsureBound(ref _gainDspAttach, "yarg_gain_dsp_attach")(channel, gain, priority, out dsp, out bassError);

        internal static int GainDspSetGain(BassGainDsp dsp, float gain) =>
            EnsureBound(ref _gainDspSetGain, "yarg_gain_dsp_set_gain")(dsp, gain);

        internal static void GainDspDestroy(IntPtr dsp) =>
            EnsureBound(ref _gainDspDestroy, "yarg_gain_dsp_destroy")(dsp);

        internal static int FreeverbDspAttach(uint channel, float dryMix, float wetMix, float roomSize, float damp, float width, int priority, out BassFreeverbDsp dsp, out int bassError) =>
            EnsureBound(ref _freeverbDspAttach, "yarg_freeverb_dsp_attach")(channel, dryMix, wetMix, roomSize, damp, width, priority, out dsp, out bassError);

        internal static int FreeverbDspReset(BassFreeverbDsp dsp) =>
            EnsureBound(ref _freeverbDspReset, "yarg_freeverb_dsp_reset")(dsp);

        internal static int FreeverbDspSetParams(BassFreeverbDsp dsp, in BassFreeverbDsp.FreeverbParams parms) =>
            EnsureBound(ref _freeverbDspSetParams, "yarg_freeverb_dsp_set_params")(dsp, in parms);

        internal static void FreeverbDspDestroy(IntPtr dsp) =>
            EnsureBound(ref _freeverbDspDestroy, "yarg_freeverb_dsp_destroy")(dsp);

        internal static int DattorroReverbDspAttach(uint channel, float dryMix, float wetMix, float roomSize, float damp, float width, int priority, out BassDattorroReverbDsp dsp, out int bassError) =>
            EnsureBound(ref _dattorroReverbDspAttach, "yarg_dattorro_reverb_dsp_attach")(channel, dryMix, wetMix, roomSize, damp, width, priority, out dsp, out bassError);

        internal static int DattorroReverbDspReset(BassDattorroReverbDsp dsp) =>
            EnsureBound(ref _dattorroReverbDspReset, "yarg_dattorro_reverb_dsp_reset")(dsp);

        internal static int DattorroReverbDspSetParams(BassDattorroReverbDsp dsp, in BassDattorroReverbDsp.DattorroReverbParams parms) =>
            EnsureBound(ref _dattorroReverbDspSetParams, "yarg_dattorro_reverb_dsp_set_params")(dsp, in parms);

        internal static void DattorroReverbDspDestroy(IntPtr dsp) =>
            EnsureBound(ref _dattorroReverbDspDestroy, "yarg_dattorro_reverb_dsp_destroy")(dsp);

        internal static int NoiseGateDspAttach(uint channel, float threshold, float floorGain, float attackMs, float holdMs, float releaseMs, int priority, out BassNoiseGateDsp dsp, out int bassError) =>
            EnsureBound(ref _noiseGateDspAttach, "yarg_noise_gate_dsp_attach")(channel, threshold, floorGain, attackMs, holdMs, releaseMs, priority, out dsp, out bassError);

        internal static int NoiseGateDspReset(BassNoiseGateDsp dsp) =>
            EnsureBound(ref _noiseGateDspReset, "yarg_noise_gate_dsp_reset")(dsp);

        internal static int NoiseGateDspSetParams(BassNoiseGateDsp dsp, in BassNoiseGateDsp.NoiseGateParams parms) =>
            EnsureBound(ref _noiseGateDspSetParams, "yarg_noise_gate_dsp_set_params")(dsp, in parms);

        internal static void NoiseGateDspDestroy(IntPtr dsp) =>
            EnsureBound(ref _noiseGateDspDestroy, "yarg_noise_gate_dsp_destroy")(dsp);

        internal static int OneShotStreamCreate(in BassNativeOneShotStream.NativeConfig config, IntPtr pcm, ulong pcmSampleCount, IntPtr schedule, ulong scheduleCount, out BassNativeOneShotStream stream, out int bassError) =>
            EnsureBound(ref _oneShotStreamCreate, "yarg_one_shot_stream_create")(in config, pcm, pcmSampleCount, schedule, scheduleCount, out stream, out bassError);

        internal static int OneShotStreamAttach(BassNativeOneShotStream stream, uint mixer, double anchorSongPosition, float playbackSpeed, int paused, out int bassError) =>
            EnsureBound(ref _oneShotStreamAttach, "yarg_one_shot_stream_attach")(stream, mixer, anchorSongPosition, playbackSpeed, paused, out bassError);

        internal static int OneShotStreamResync(BassNativeOneShotStream stream, uint mixer, double anchorSongPosition, float playbackSpeed, int clearActiveVoices, out int bassError) =>
            EnsureBound(ref _oneShotStreamResync, "yarg_one_shot_stream_resync_ex")(stream, mixer, anchorSongPosition, playbackSpeed, clearActiveVoices, out bassError);

        internal static int OneShotStreamSetPaused(BassNativeOneShotStream stream, uint mixer, int paused, out int bassError) =>
            EnsureBound(ref _oneShotStreamSetPaused, "yarg_one_shot_stream_set_paused")(stream, mixer, paused, out bassError);

        internal static int OneShotStreamSetGain(BassNativeOneShotStream stream, float gain) =>
            EnsureBound(ref _oneShotStreamSetGain, "yarg_one_shot_stream_set_gain")(stream, gain);

        internal static int OneShotStreamDetach(BassNativeOneShotStream stream, out int bassError) =>
            EnsureBound(ref _oneShotStreamDetach, "yarg_one_shot_stream_detach")(stream, out bassError);

        internal static int OneShotStreamDestroy(IntPtr stream, out int bassError) =>
            EnsureBound(ref _oneShotStreamDestroy, "yarg_one_shot_stream_destroy")(stream, out bassError);

        internal static int SineSynthDspCreate(in BassSineSynthDsp.NativeConfig config, out BassSineSynthDsp dsp) =>
            EnsureBound(ref _sineSynthDspCreate, "yarg_sine_synth_dsp_create")(in config, out dsp);

        internal static int SineSynthDspAttach(BassSineSynthDsp dsp, uint channel, int priority, out int bassError) =>
            EnsureBound(ref _sineSynthDspAttach, "yarg_sine_synth_dsp_attach")(dsp, channel, priority, out bassError);

        internal static int SineSynthDspDetach(BassSineSynthDsp dsp, out int bassError) =>
            EnsureBound(ref _sineSynthDspDetach, "yarg_sine_synth_dsp_detach")(dsp, out bassError);

        internal static int SineSynthDspSetSchedule(BassSineSynthDsp dsp, IntPtr segments, ulong segmentCount, out int bassError) =>
            EnsureBound(ref _sineSynthDspSetSchedule, "yarg_sine_synth_dsp_set_schedule")(dsp, segments, segmentCount, out bassError);

        internal static int SineSynthDspSetTiming(BassSineSynthDsp dsp, double songTimeOffset, float playbackSpeed) =>
            EnsureBound(ref _sineSynthDspSetTiming, "yarg_sine_synth_dsp_set_timing")(dsp, songTimeOffset, playbackSpeed);

        internal static int SineSynthDspSetOutputChannel(BassSineSynthDsp dsp, uint outputChannel) =>
            EnsureBound(ref _sineSynthDspSetOutputChannel, "yarg_sine_synth_dsp_set_output_channel")(dsp, outputChannel);

        internal static int SineSynthDspDestroy(IntPtr dsp) =>
            EnsureBound(ref _sineSynthDspDestroy, "yarg_sine_synth_dsp_destroy")(dsp);

        internal static int ReadAheadStreamCreate(in ReadAheadConfig config, out BassReadAheadStream stream, out uint streamHandle, out int bassError) =>
            EnsureBound(ref _readAheadStreamCreate, "yarg_read_ahead_stream_create")(in config, out stream, out streamHandle, out bassError);

        internal static int ReadAheadStreamSetCallbackClock(BassReadAheadStream stream, int enabled) =>
            EnsureBound(ref _readAheadStreamSetCallbackClock, "yarg_read_ahead_stream_set_callback_clock")(stream, enabled);

        internal static int ReadAheadStreamPrefill(BassReadAheadStream stream, uint timeoutMilliseconds) =>
            EnsureBound(ref _readAheadStreamPrefill, "yarg_read_ahead_stream_prefill")(stream, timeoutMilliseconds);

        internal static int ReadAheadStreamFlush(BassReadAheadStream stream) =>
            EnsureBound(ref _readAheadStreamFlush, "yarg_read_ahead_stream_flush")(stream);

        internal static int ReadAheadStreamSetBufferLength(BassReadAheadStream stream, uint bufferMilliseconds) =>
            EnsureBound(ref _readAheadStreamSetBufferLength, "yarg_read_ahead_stream_set_buffer_length")(stream, bufferMilliseconds);

        internal static long ReadAheadStreamGetSourcePosition(BassReadAheadStream stream, uint sourceHandle, uint endpointDelayFrames, out int error) =>
            EnsureBound(ref _readAheadStreamGetSourcePosition, "yarg_read_ahead_stream_get_source_position")(stream, sourceHandle, endpointDelayFrames, out error);

        internal static int ReadAheadStreamGetPositionSnapshot(BassReadAheadStream stream, uint sourceHandle, uint endpointDelayFrames, ref ReadAheadPositionSnapshot snapshot) =>
            EnsureBound(ref _readAheadStreamGetPositionSnapshot, "yarg_read_ahead_stream_get_position_snapshot")(stream, sourceHandle, endpointDelayFrames, ref snapshot);

        internal static int ReadAheadStreamGetStats(BassReadAheadStream stream, ref ReadAheadStats stats) =>
            EnsureBound(ref _readAheadStreamGetStats, "yarg_read_ahead_stream_get_stats")(stream, ref stats);

        internal static int ReadAheadStreamDestroy(IntPtr stream, out int bassError) =>
            EnsureBound(ref _readAheadStreamDestroy, "yarg_read_ahead_stream_destroy")(stream, out bassError);

        private static T EnsureBound<T>(ref T? delegateField, string entryPoint) where T : Delegate
        {
            if (delegateField != null)
            {
                return delegateField;
            }

            EnsureLoaded();
            if (_libraryHandle == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Unable to load native library for {entryPoint}.");
            }

            delegateField = GetFunction<T>(_libraryHandle, entryPoint);
            if (delegateField == null)
            {
                throw new EntryPointNotFoundException($"Unable to find entry point '{entryPoint}' in native library.");
            }

            return delegateField;
        }

        private static void BindAll(IntPtr handle)
        {
            _getAbiVersion = GetFunction<GetAbiVersionDelegate>(handle, "yarg_audio_get_abi_version");
            _gainDspAttach = GetFunction<GainDspAttachDelegate>(handle, "yarg_gain_dsp_attach");
            _gainDspSetGain = GetFunction<GainDspSetGainDelegate>(handle, "yarg_gain_dsp_set_gain");
            _gainDspDestroy = GetFunction<GainDspDestroyDelegate>(handle, "yarg_gain_dsp_destroy");
            _freeverbDspAttach = GetFunction<FreeverbDspAttachDelegate>(handle, "yarg_freeverb_dsp_attach");
            _freeverbDspReset = GetFunction<FreeverbDspResetDelegate>(handle, "yarg_freeverb_dsp_reset");
            _freeverbDspSetParams = GetFunction<FreeverbDspSetParamsDelegate>(handle, "yarg_freeverb_dsp_set_params");
            _freeverbDspDestroy = GetFunction<FreeverbDspDestroyDelegate>(handle, "yarg_freeverb_dsp_destroy");
            _dattorroReverbDspAttach = GetFunction<DattorroReverbDspAttachDelegate>(handle, "yarg_dattorro_reverb_dsp_attach");
            _dattorroReverbDspReset = GetFunction<DattorroReverbDspResetDelegate>(handle, "yarg_dattorro_reverb_dsp_reset");
            _dattorroReverbDspSetParams = GetFunction<DattorroReverbDspSetParamsDelegate>(handle, "yarg_dattorro_reverb_dsp_set_params");
            _dattorroReverbDspDestroy = GetFunction<DattorroReverbDspDestroyDelegate>(handle, "yarg_dattorro_reverb_dsp_destroy");
            _noiseGateDspAttach = GetFunction<NoiseGateDspAttachDelegate>(handle, "yarg_noise_gate_dsp_attach");
            _noiseGateDspReset = GetFunction<NoiseGateDspResetDelegate>(handle, "yarg_noise_gate_dsp_reset");
            _noiseGateDspSetParams = GetFunction<NoiseGateDspSetParamsDelegate>(handle, "yarg_noise_gate_dsp_set_params");
            _noiseGateDspDestroy = GetFunction<NoiseGateDspDestroyDelegate>(handle, "yarg_noise_gate_dsp_destroy");
            _oneShotStreamCreate = GetFunction<OneShotStreamCreateDelegate>(handle, "yarg_one_shot_stream_create");
            _oneShotStreamAttach = GetFunction<OneShotStreamAttachDelegate>(handle, "yarg_one_shot_stream_attach");
            _oneShotStreamResync = GetFunction<OneShotStreamResyncDelegate>(handle, "yarg_one_shot_stream_resync_ex");
            _oneShotStreamSetPaused = GetFunction<OneShotStreamSetPausedDelegate>(handle, "yarg_one_shot_stream_set_paused");
            _oneShotStreamSetGain = GetFunction<OneShotStreamSetGainDelegate>(handle, "yarg_one_shot_stream_set_gain");
            _oneShotStreamDetach = GetFunction<OneShotStreamDetachDelegate>(handle, "yarg_one_shot_stream_detach");
            _oneShotStreamDestroy = GetFunction<OneShotStreamDestroyDelegate>(handle, "yarg_one_shot_stream_destroy");
            _sineSynthDspCreate = GetFunction<SineSynthDspCreateDelegate>(handle, "yarg_sine_synth_dsp_create");
            _sineSynthDspAttach = GetFunction<SineSynthDspAttachDelegate>(handle, "yarg_sine_synth_dsp_attach");
            _sineSynthDspDetach = GetFunction<SineSynthDspDetachDelegate>(handle, "yarg_sine_synth_dsp_detach");
            _sineSynthDspSetSchedule = GetFunction<SineSynthDspSetScheduleDelegate>(handle, "yarg_sine_synth_dsp_set_schedule");
            _sineSynthDspSetTiming = GetFunction<SineSynthDspSetTimingDelegate>(handle, "yarg_sine_synth_dsp_set_timing");
            _sineSynthDspSetOutputChannel = GetFunction<SineSynthDspSetOutputChannelDelegate>(handle, "yarg_sine_synth_dsp_set_output_channel");
            _sineSynthDspDestroy = GetFunction<SineSynthDspDestroyDelegate>(handle, "yarg_sine_synth_dsp_destroy");
            _readAheadStreamCreate = GetFunction<ReadAheadStreamCreateDelegate>(handle, "yarg_read_ahead_stream_create");
            _readAheadStreamSetCallbackClock = GetFunction<ReadAheadStreamSetCallbackClockDelegate>(handle, "yarg_read_ahead_stream_set_callback_clock");
            _readAheadStreamPrefill = GetFunction<ReadAheadStreamPrefillDelegate>(handle, "yarg_read_ahead_stream_prefill");
            _readAheadStreamFlush = GetFunction<ReadAheadStreamFlushDelegate>(handle, "yarg_read_ahead_stream_flush");
            _readAheadStreamSetBufferLength = GetFunction<ReadAheadStreamSetBufferLengthDelegate>(handle, "yarg_read_ahead_stream_set_buffer_length");
            _readAheadStreamGetSourcePosition = GetFunction<ReadAheadStreamGetSourcePositionDelegate>(handle, "yarg_read_ahead_stream_get_source_position");
            _readAheadStreamGetPositionSnapshot = GetFunction<ReadAheadStreamGetPositionSnapshotDelegate>(handle, "yarg_read_ahead_stream_get_position_snapshot");
            _readAheadStreamGetStats = GetFunction<ReadAheadStreamGetStatsDelegate>(handle, "yarg_read_ahead_stream_get_stats");
            _readAheadStreamDestroy = GetFunction<ReadAheadStreamDestroyDelegate>(handle, "yarg_read_ahead_stream_destroy");
        }

        private static T? GetFunction<T>(IntPtr handle, string name) where T : Delegate
        {
            var address = GetProcAddress(handle, name);
            if (address == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

#if UNITY_EDITOR
        private static string GetLibraryPath()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var sourcePath = GetSourcePluginPath(projectRoot);
            if (!File.Exists(sourcePath))
            {
                return sourcePath;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "YargAudioShadow");
            Directory.CreateDirectory(tempDir);

            var ext = Path.GetExtension(sourcePath);
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var writeTime = File.GetLastWriteTimeUtc(sourcePath).Ticks;
            var shadowPath = Path.Combine(tempDir, $"{baseName}_{writeTime}{ext}");

            if (!File.Exists(shadowPath))
            {
                File.Copy(sourcePath, shadowPath, overwrite: true);
            }

            return shadowPath;
        }

        private static string GetSourcePluginPath(string projectRoot)
        {
#if UNITY_EDITOR_OSX
            return Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Mac", "libyarg_audio.dylib");
#elif UNITY_EDITOR_LINUX
            return Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Linux", "x86_64", "libyarg_audio.so");
#else
            return Path.Combine(projectRoot, "Assets", "Plugins", "YargAudio", "Windows", "x86_64", "yarg_audio.dll");
#endif
        }
#else
        private static string GetLibraryPath()
        {
            var dataPath = PathHelper.ApplicationDataPath ?? string.Empty;
#if UNITY_STANDALONE_OSX
            return Path.Combine(dataPath, "Plugins", "libyarg_audio.dylib");
#elif UNITY_STANDALONE_LINUX
            return Path.Combine(dataPath, "Plugins", "x86_64", "libyarg_audio.so");
#elif UNITY_STANDALONE_WIN
            return Path.Combine(dataPath, "Plugins", "x86_64", "yarg_audio.dll");
#else
            return "yarg_audio";
#endif
        }
#endif

        private static IntPtr LoadNativeLibrary(string path)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return WindowsNative.LoadLibrary(path);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            return LinuxNative.dlopen(path, RTLD_NOW | RTLD_GLOBAL);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return MacNative.dlopen(path, RTLD_NOW | RTLD_GLOBAL);
#else
            return IntPtr.Zero;
#endif
        }

        private static IntPtr GetProcAddress(IntPtr handle, string symbol)
        {
            if (handle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return WindowsNative.GetProcAddress(handle, symbol);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            return LinuxNative.dlsym(handle, symbol);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return MacNative.dlsym(handle, symbol);
#else
            return IntPtr.Zero;
#endif
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint GetAbiVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GainDspAttachDelegate(uint channel, float gain, int priority, out BassGainDsp dsp, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GainDspSetGainDelegate(BassGainDsp dsp, float gain);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GainDspDestroyDelegate(IntPtr dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FreeverbDspAttachDelegate(uint channel, float dryMix, float wetMix, float roomSize, float damp, float width, int priority, out BassFreeverbDsp dsp, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FreeverbDspResetDelegate(BassFreeverbDsp dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int FreeverbDspSetParamsDelegate(BassFreeverbDsp dsp, in BassFreeverbDsp.FreeverbParams parms);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FreeverbDspDestroyDelegate(IntPtr dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DattorroReverbDspAttachDelegate(uint channel, float dryMix, float wetMix, float roomSize, float damp, float width, int priority, out BassDattorroReverbDsp dsp, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DattorroReverbDspResetDelegate(BassDattorroReverbDsp dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DattorroReverbDspSetParamsDelegate(BassDattorroReverbDsp dsp, in BassDattorroReverbDsp.DattorroReverbParams parms);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DattorroReverbDspDestroyDelegate(IntPtr dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NoiseGateDspAttachDelegate(uint channel, float threshold, float floorGain, float attackMs, float holdMs, float releaseMs, int priority, out BassNoiseGateDsp dsp, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NoiseGateDspResetDelegate(BassNoiseGateDsp dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NoiseGateDspSetParamsDelegate(BassNoiseGateDsp dsp, in BassNoiseGateDsp.NoiseGateParams parms);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NoiseGateDspDestroyDelegate(IntPtr dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamCreateDelegate(in BassNativeOneShotStream.NativeConfig config, IntPtr pcm, ulong pcmSampleCount, IntPtr schedule, ulong scheduleCount, out BassNativeOneShotStream stream, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamAttachDelegate(BassNativeOneShotStream stream, uint mixer, double anchorSongPosition, float playbackSpeed, int paused, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamResyncDelegate(BassNativeOneShotStream stream, uint mixer, double anchorSongPosition, float playbackSpeed, int clearActiveVoices, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamSetPausedDelegate(BassNativeOneShotStream stream, uint mixer, int paused, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamSetGainDelegate(BassNativeOneShotStream stream, float gain);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamDetachDelegate(BassNativeOneShotStream stream, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int OneShotStreamDestroyDelegate(IntPtr stream, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspCreateDelegate(in BassSineSynthDsp.NativeConfig config, out BassSineSynthDsp dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspAttachDelegate(BassSineSynthDsp dsp, uint channel, int priority, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspDetachDelegate(BassSineSynthDsp dsp, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspSetScheduleDelegate(BassSineSynthDsp dsp, IntPtr segments, ulong segmentCount, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspSetTimingDelegate(BassSineSynthDsp dsp, double songTimeOffset, float playbackSpeed);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspSetOutputChannelDelegate(BassSineSynthDsp dsp, uint outputChannel);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SineSynthDspDestroyDelegate(IntPtr dsp);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamCreateDelegate(in ReadAheadConfig config, out BassReadAheadStream stream, out uint streamHandle, out int bassError);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamSetCallbackClockDelegate(BassReadAheadStream stream, int enabled);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamPrefillDelegate(BassReadAheadStream stream, uint timeoutMilliseconds);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamFlushDelegate(BassReadAheadStream stream);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamSetBufferLengthDelegate(BassReadAheadStream stream, uint bufferMilliseconds);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long ReadAheadStreamGetSourcePositionDelegate(BassReadAheadStream stream, uint sourceHandle, uint endpointDelayFrames, out int error);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamGetPositionSnapshotDelegate(BassReadAheadStream stream, uint sourceHandle, uint endpointDelayFrames, ref ReadAheadPositionSnapshot snapshot);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamGetStatsDelegate(BassReadAheadStream stream, ref ReadAheadStats stats);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadAheadStreamDestroyDelegate(IntPtr stream, out int bassError);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static class WindowsNative
        {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private static class LinuxNative
        {
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so.2")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
        }
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private static class MacNative
        {
            [DllImport("libSystem.dylib")]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libSystem.dylib")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
        }
#endif
    }
}
