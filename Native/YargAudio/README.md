# YargAudio Native Audio Library

A lightweight, cross-platform C++ audio library built for YARG. It handles audio streaming, time stretching, and DSP processing outside Unity’s managed runtime.

Unity's managed C# code talks to this library through standard C ABI functions (`include/yarg_audio.h`) and .NET `SafeHandle` wrappers in `Assets/Script/Audio/Bass/Native/YargAudioBindings.cs`.

---

## What is it and why is it native?

In rhythm games, audio timing must be rock-solid. Garbage collection pauses or thread stalls in managed C# can cause audible pops, stutters, or timing desyncs. 

To prevent this, YARG delegates low-level audio streaming, phase-vocoder time stretching, and real-time DSP effects to compiled native code (`.dll` on Windows, `.so` on Linux, `.dylib` on macOS).

### Key Features

#### 1. Real-Time Time Stretching & Pitch Shifting (`yarg_stretch_stream`)
Used in Practice Mode to slow songs down (to 10%) or speed them up (to 200%) without altering their pitch.
- Built on top of `signalsmith-linear` STFT/FFT.
- Preserves source spectra at 100% speed and uses transient protection during slowdown; quality still depends on the recording and stretch ratio.
- Accepts tempo commands from gameplay synchronization; changes apply at native processing-block boundaries.
- See [`docs/time-stretch.md`](../../docs/time-stretch.md) for an in-depth explanation of how the phase vocoder works.

#### 2. Song Read-Ahead Streaming (`yarg_read_ahead_stream`)
Decodes and pre-buffers audio stems into lock-free ring buffers on a dedicated background native thread.
- Decouples song decoding from audio device output callbacks.
- Protects against playback hiccups caused by CPU spikes or tight ASIO / Shared buffer windows.
- Maintains accurate song position by tracking buffered frames and endpoint latency.

#### 3. Sample-Accurate Audio Cues (`yarg_one_shot_stream`)
Plays one-shot sound effects (metronomes, hit sounds, crowd cheers) with sub-millisecond precision.
- Keeps PCM audio data in native memory.
- Schedules playback at exact sample counts rather than waiting for Unity frame renders.

#### 4. Real-Time DSP Effects
Custom audio effects attached directly to BASS channels:
- **Gain & Pan (`yarg_gain_dsp`)**: Atomic, lock-free volume, stereo panning, and channel muting without locks or allocations.
- **Freeverb (`yarg_freeverb_dsp`)**: Schroeder-Moorer model reverb with sample-rate-scaled comb/all-pass delay lines.
- **Dattorro Plate Reverb (`yarg_dattorro_reverb_dsp`)**: High-quality figure-eight tank plate reverb with input diffusers, high-frequency damping, pre-delay, and decay controls.
- **Noise Gate (`yarg_noise_gate_dsp`)**: Linked multi-channel envelope follower that silences microphone background noise when the player is not singing.
- **Sine Synth (`yarg_sine_synth_dsp`)**: Real-time vocal guide pitch generator that renders scheduled pitch segments synchronized with the song, featuring smooth fades and speaker-pair output routing.

For overall audio pipeline topology, see [`docs/audio_pipeline.md`](../../docs/audio_pipeline.md).

---

## Directory Structure

```text
Native/YargAudio/
├── CMakeLists.txt        # CMake build configuration
├── CMakePresets.json     # Standard presets for Windows, Linux, and macOS
├── include/
│   └── yarg_audio.h      # Public C ABI interface shared with C#
├── src/
│   ├── dsp/              # DSP effects (Gain, Freeverb, Dattorro, Noise Gate, Sine Synth)
│   ├── one_shot/         # Scheduled sample player & metronome
│   ├── stretch/          # Time-stretch and transient detection engine
│   ├── ReadAheadStream.* # Song pre-buffering and ring buffer management
│   └── yarg_audio_c_api.cpp # C API entry points exported by the library
├── tests/                # C++ unit tests
└── third_party/          # Vendored header-only dependencies (Signalsmith)
```

---

## How to Build

### 1. Unity Editor (Current Platform Only)
You do not need to manually recompile C++ code during normal development:
- **Automatic Build on Play**: Whenever you press **Play** (or build the game), Unity checks if any C++ files have changed since the last build. If changes are found, Unity synchronously compiles the library and updates the plugin file. Rebuilding during Play leaves the loaded version unchanged. Exit Play and enter it again to use the rebuilt plugin; restarting the editor is not required.
- **Shows Up in Git**: The newly built binary (`.dll`, `.so`, or `.dylib`) is copied directly into `Assets/Plugins/YargAudio/`, so it will appear as a modified file in Git.
- **Safe Fallback**: If compilation fails, Unity logs a warning in the Console and continues using the last working version so your play testing is not interrupted.
- **Manual Menu Trigger**: You can force a rebuild at any time by choosing **YARG > Audio > Rebuild Native Audio (This Platform)** from the top menu bar.

Native function bindings stay with one library version for the entire managed domain. Rebuilding does not replace functions used by existing native objects. Initial loading is serialized, including retries after a missing library. Ordinary playback calls use their cached delegates without taking the load lock. If a rebuilt plugin is pending when Play ends, the editor requests a script reload. If domain reload is disabled and an update is still pending when Play starts, it reloads scripts before automatically continuing into Play. Before a domain reload, the audio cleanup handler closes active audio and frees BASS; old shadow libraries remain mapped for callbacks and finalizers.

### 2. Command Line (Current Platform Only)
This does the same thing as the Editor's manual rebuild option, but runs from your terminal without needing Unity open:
```bash
dotnet run --project scripts/NativeBuild -- build
```
It configures CMake, compiles the library, runs the test suite, and copies the resulting binary into `Assets/Plugins/YargAudio/<Platform>/`.

### 3. GitHub Actions (All Platforms)
Building binaries for Windows, Linux, and macOS locally requires dedicated build tools for each OS. You can build each platform with GitHub Actions (requires the [GitHub CLI](https://cli.github.com/) authenticated via `gh auth login`):

1. **Commit changes**: Commit your native code changes locally.
2. **Push to remote**: Push your branch to GitHub. GitHub Actions builds from the remote branch, so unpushed local changes will not be included.
3. **Run the package helper**:
   ```bash
   dotnet run --project scripts/NativeBuild -- package --ref <your-remote-branch>
   ```
   This command triggers `.github/workflows/native-audio.yml`, waits for the Windows (`x86_64`), Linux (`x86_64`), and macOS (`universal`) runners to finish, and downloads the built binaries directly into `Assets/Plugins/YargAudio/`.
4. **Commit the binaries**: Commit the newly downloaded binaries and their `.meta` files to your branch so all platforms have pre-built plugins.

---

## How to Make Changes

When modifying or adding native features, follow this workflow:

### 1. Modify or Add C++ Code
- Implement your logic in `src/`.
- **Audio-Thread Rules**: Code executed inside BASS callbacks or DSP routines must be strictly real-time safe:
  - **No heap allocations** (`malloc`, `new`, `std::vector` resizing).
  - **No blocking locks** (`std::mutex`, semaphores). Use atomic flags or lock-free queues.
  - **No file or socket I/O**.

### 2. Update the C ABI (`include/yarg_audio.h`)
- Expose new functions with `YARG_AUDIO_API` and `YARG_AUDIO_CALL`.
- Use C-compatible types (`int32_t`, `uint32_t`, `float`, plain structs, opaque pointers).
- If changing any existing struct layouts or function signatures, increment `YARG_AUDIO_ABI_VERSION`.
- Implement exported wrapper functions in `src/yarg_audio_c_api.cpp`.

### 3. Add or Update Managed C# Bindings
- In `Assets/Script/Audio/Bass/Native/YargAudioBindings.cs`, declare matching Cdecl delegate signatures and dynamic export bindings. Keep ownership in the corresponding .NET `SafeHandle` wrappers.
- Keep `BassHelpers.YARG_AUDIO_ABI_VERSION` synchronized with the native header; `YargAudioNative` checks it during initialization.

### 4. Test Locally
- Add C++ test cases under `tests/`.
- Run `dotnet run --project scripts/NativeBuild -- build` to verify tests pass and update your local Unity plugin.
- Verify behavior in the Unity Editor.

### 5. Publish Multi-Platform Binaries
- Commit and push your changes to your branch.
- Run `dotnet run --project scripts/NativeBuild -- package --ref <your-branch>` to compile and download binaries for all platforms so other developers don't have to build them manually.

---

## Important Technical Notes

- **Dynamic BASS Linking**: The library does not link BASS at compile time. Instead, it hooks into the BASS library already loaded by Unity in memory, preventing multiple audio engine instances from conflicting.
- **Linux Compatibility**: Linux binaries are compiled on Ubuntu 20.04 (`glibc 2.31`) so they work reliably on all Linux distributions supported by Unity 6.
- **Thread Safety & Buffering**:
  - Song decoding happens on a background worker thread.
  - Endpoint callbacks consume song PCM from a lock-free ring buffer. Stretch processing and position history run on the decode side; history access uses a mutex.
  - When stopping or seeking, the stream safely waits for active callbacks to finish before resetting buffers.
