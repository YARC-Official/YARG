# ASIO & Audio Pipeline Architecture

This document provides a high-level guide to how audio playback, low-latency ASIO output, and microphone input work in YARG, along with where to find each component in the codebase.

---

## 1. Quick Code Map

| Area | Class / File | Primary Responsibility |
| :--- | :--- | :--- |
| **System Orchestration** | [`BassAudioManager`](../Assets/Script/Audio/Bass/BassAudioManager.cs) | High-level audio manager; handles device selection, initialization, and sample reloading. |
| **Switchboard / Routing** | [`BassAudioRouter`](../Assets/Script/Audio/Bass/BassAudioRouter.cs) | Connects active songs and live mic monitors to the active output without disrupting gameplay. |
| **Song State** | [`BassSong`](../Assets/Script/Audio/Bass/BassSong.cs) | Core song state (stem pipeline, speed/pitch tempo streams, volume fades, and positioning). |
| **Device Connection** | [`BassSongConnection`](../Assets/Script/Audio/Bass/BassSongConnection.cs) | Output bridge connecting a song to an audio device (song mixer, read-ahead stream, volume mixer). |
| **Read-Ahead Buffering** | [`BassReadAheadStream`](../Assets/Script/Audio/Bass/BassReadAheadStream.cs)<br>[`Native/YargAudio`](../Native/YargAudio/README.md) | Native C++ worker and ring buffer that pre-decodes song stems to prevent audio underruns. |
| **Outputs (Endpoints)** | [`BassOutput`](../Assets/Script/Audio/Bass/BassOutput.cs)<br>[`BassSharedOutput`](../Assets/Script/Audio/Bass/BassSharedOutput.cs)<br>[`BassAsioOutput`](../Assets/Script/Audio/Bass/Asio/BassAsioOutput.cs) | Base output class and its two endpoints: standard cross-platform playback (`Shared`) and low-latency [`BassAsioOutput`](../Assets/Script/Audio/Bass/Asio/BassAsioOutput.cs) (Windows only). |
| **ASIO Driver Control** | [`BassAsioDriver`](../Assets/Script/Audio/Bass/Asio/BassAsioDriver.cs) | Wraps BASSASIO initialization, channel binding, hardware buffer querying, and driver notifications. |
| **Microphone Signal** | [`BassMicSignal`](../Assets/Script/Audio/Bass/BassMicSignal.cs)<br>[`BassMicAnalyzer`](../Assets/Script/Audio/Bass/BassMicAnalyzer.cs) | Splits incoming mic audio into dual paths: pitch analysis (with EQ) and live vocal monitoring (with reverb). |
| **ASIO Mic Handling** | [`BassAsioInput`](../Assets/Script/Audio/Bass/Asio/BassAsioInput.cs)<br>[`BassAsioMics`](../Assets/Script/Audio/Bass/Asio/BassAsioMics.cs)<br>[`BassAsioMicSource`](../Assets/Script/Audio/Bass/Asio/BassAsioMicSource.cs) | Exposes individual hardware input channels as distinct microphone choices and manages driver claiming. |
| **Microphone Devices** | [`BassMicDevice`](../Assets/Script/Audio/Bass/BassMicDevice.cs)<br>[`IBassMicSource`](../Assets/Script/Audio/Bass/IBassMicSource.cs)<br>[`BassSharedMicSource`](../Assets/Script/Audio/Bass/BassSharedMicSource.cs) | Unified player microphone device and input source abstractions across ASIO and Shared Audio backends. |
| **Live Sound Effects** | [`BassSamplePlayer`](../Assets/Script/Audio/Bass/BassSamplePlayer.cs)<br>[`BassSampleChannel`](../Assets/Script/Audio/Bass/BassSampleChannel.cs) | Plays menu SFX, metronomes, and drum hit sounds directly into the output mixer with zero buffer delay. |

---

## 2. The Shared Audio Graph

Regardless of whether the player chooses **Shared** (standard OS audio on Windows, macOS, and Linux) or **ASIO** (Windows-only), the audio pipeline uses the exact same structure up until the final device output:

```text
[Song Branch]  Song Stems -> Stem Pipeline -> Tempo Stream -> Read-Ahead Buffer -> Volume Mixer --+
                                                                                                  |--> Final Mixer --> Audio Device
[Live Branch]  Drum Hits, Menu SFX & Microphone Monitors -----------------------------------------+    (ASIO or Shared)
```

1. **Song Branches (Buffered & Independent)**: Each song owns its own independent pipeline branch (stem decoding, tempo stream, and native read-ahead buffer). Multiple songs can exist simultaneously—such as when previewing tracks in the music library—enabling smooth, glitch-free crossfading between songs.
2. **Volume Mixer (Instant Whole-Song Control & Fades)**: Placed immediately *after* the read-ahead buffer for each song branch. Because the buffer holds pre-rendered audio, changing volume before the buffer would create a delayed reaction while waiting for queued audio to drain. Putting the Volume Mixer downstream ensures song volume changes, pause fades, and song-to-song crossfades take effect instantly across the **whole song**.
3. **Live Branch (Unbuffered)**: Drum hits, menu navigation sounds, and singer voice monitoring bypass the read-ahead buffer entirely and connect straight to the final mixer for zero-latency feedback.
4. **Final Mixer**: Combines all active song branches and unbuffered live sounds into a single master stream.

---

## 3. BassSong vs. BassSongConnection (Graph Rebuilding & Switching)

A key architectural pattern in YARG is the separation between core song state and device-specific audio graphs:

### `BassSong`
- Represents the song instance that lasts for the entire song / gameplay session.
- Owns device-independent state: audio stem decoding, speed/pitch shifting, tempo sync, volume levels, and timeline position tracking.
- **Never gets recreated** when changing audio devices or when an ASIO driver resets.

### `BassSongConnection`
- Represents the device-specific audio graph connecting a `BassSong` to a specific [`BassOutput`](../Assets/Script/Audio/Bass/BassOutput.cs).
- Owns the intermediate BASS mixer handles, the native [`BassReadAheadStream`](../Assets/Script/Audio/Bass/BassReadAheadStream.cs) ring buffer, and the volume mixer attached to that output's sample rate and channel count.
- **Torn down and recreated** whenever the output changes.

### Connection Boundary:

```text
BassSong (Kept alive across switches)
  Song Stems ──> Stem Pipeline ──> Tempo Stream
                                         │
                                  [ Detached Here ]
                                         │
BassSongConnection (Rebuilt for new device)
  Song Mixer ──> Read-Ahead Buffer (C++) ──> Volume Mixer
```

### How Device Switching Works:

1. **Output Switch Triggered**: The player selects a new output device (or an ASIO driver resets / changes sample rate).
2. **Old Connection Detached**: [`BassAudioRouter`](../Assets/Script/Audio/Bass/BassAudioRouter.cs) detaches and disposes the old `BassSongConnection`, freeing the old device mixers and read-ahead buffer.
3. **Song State Preserved**: The underlying [`BassSong`](../Assets/Script/Audio/Bass/BassSong.cs) is not touched—its audio decoders, tempo stream, and playback position remain completely intact.
4. **New Connection Built**: A new `BassSongConnection` is created matching the new device's sample rate and channel count, prefills its read-ahead buffer, and attaches to the new master mixer.
5. **Seamless Resume**: Playback resumes at the exact same position without reloading audio files from disk.

---

## 4. Audio Output & Endpoints

### Output Endpoints (Shared vs. ASIO)
The final mixer sends audio to one of two output types:
- **Shared Output** ([`BassSharedOutput`](../Assets/Script/Audio/Bass/BassSharedOutput.cs)): Connects the final mixer to standard OS playback through BASS (WASAPI/DirectSound on Windows, CoreAudio on macOS, PulseAudio/ALSA on Linux).
- **ASIO Output** ([`BassAsioOutput`](../Assets/Script/Audio/Bass/Asio/BassAsioOutput.cs)): Connects the decode final mixer directly to the low-latency ASIO hardware driver using `BASS_ASIO_ChannelEnableBASS` (Windows only).

### Pull Model & The Buffer Challenge
Audio output works on a **pull model**: the hardware callback fires every few milliseconds (e.g. 2–3 ms in ASIO) asking for the next slice of audio. BASS lets us attach a decode stream to feed this callback, but BASS decode streams do not have built-in buffering—and 2 ms is not enough time to decode, mix, and resample multiple song stems without audio dropouts.

---

## 5. Native Read-Ahead Buffer & Position Tracking

To prevent audio dropouts in low-latency playback without introducing audible delay, YARG uses a native C++ read-ahead stream ([`BassReadAheadStream`](../Assets/Script/Audio/Bass/BassReadAheadStream.cs) & [`Native/YargAudio`](../Native/YargAudio/README.md)):

### Buffer Architecture
- **Dedicated Background Worker** ([`RenderAheadMixer`](../Native/YargAudio/src/RenderAheadMixer.cpp)): Pre-decodes, resamples, and mixes song stems in 128-frame chunks ahead of time into a fast circular ring buffer ([`AudioRingBuffer`](../Native/YargAudio/src/AudioRingBuffer.cpp)). When the buffer reaches its target threshold, the worker sleeps until the buffer drops below a low-watermark level.
- **Fast Lock-Free Callback** ([`ReadAheadStream`](../Native/YargAudio/src/ReadAheadStream.cpp)): When the audio driver callback fires, it does zero heavy stem decoding or mixing. It performs a fast memory copy directly from the ring buffer in microseconds, preventing dropouts. Missing frames during unexpected stalls are zero-filled to prevent pops or crashes.
- **Hardware Safety Floor**: Even if the playback buffer is set to 0 ms in the app, the native layer still maintains an internal cushion sized to at least double the driver's hardware callback buffer (configured in your ASIO/OS device settings). However, ultra-low driver cushions (e.g. 2–5 ms) can still underrun if OS thread scheduling or multi-stem decoding takes longer than that tiny window to wake up and produce the next chunk.
- **Prefill on Start/Seek** ([`BassSongConnection.Play`](../Assets/Script/Audio/Bass/BassSongConnection.cs)): When playback begins or seeks, the output stream remains paused until the worker fills the ring buffer completely, guaranteeing glitch-free initial playback.

### Position Tracking & Anti-Stutter Protections
Because audio frames sit in the read-ahead buffer before reaching the speakers, calculating the exact **Heard Position** (what the player is currently hearing) requires specialized handling:

1. **Lock-Free Snapshots (Seqlock) ([`ReadAheadStream.getPositionSnapshot`](../Native/YargAudio/src/ReadAheadStream.cpp))**:
   - Position tracking requires reading multiple shared variables (consumed frames, timestamps, and buffer depth) as a single consistent snapshot.
   - Standard mutex locks cannot be used because blocking the real-time audio thread causes immediate audio dropouts.
   - Instead, we use an atomic version counter (a Seqlock):
     - The audio callback increments the counter before and after updating data (odd while writing, even when finished).
     - The game thread notes the counter, reads the data, and checks the counter again. If the counter was odd (write in progress) or the number changed mid-read (the audio thread updated data while it was reading), the game thread retries until it gets an uncorrupted snapshot.

2. **Between-Callback Smoothing & Drift Protection ([`ReadAheadStream.remainingPlaybackDelayFrames`](../Native/YargAudio/src/ReadAheadStream.cpp))**:
   - **The Concept**: Audio is decoded in advance and waits in line before reaching the speakers. What you are hearing at this exact moment is simply:
     $$\text{Heard Position} = \text{Decoded Position} - \text{Audio Waiting in Line}$$
   - **The Problem (Visual Freezing & Stutter)**: The screen updates very fast (e.g., 120+ times a second), but the sound card only takes audio in periodic batches (every 10–50 ms). In between those batches, the amount of audio waiting in line does not change. If the game relied only on this raw calculation, the song position would stay frozen for several video frames in a row, then suddenly leap forward on the next batch—causing falling notes to stutter and jerk on screen.
   - **The Fix (Smoothing)**: To keep notes moving smoothly between audio batches, the game runs a high-precision stopwatch (`std::chrono::steady_clock`). It checks how much time has passed since the sound card's last batch and advances the song position accordingly on every video frame.
   - **Drift Protection**: If the sound card clock and computer clock run at slightly different speeds, the game remembers the highest timestamp it has ever shown (`lastHeardFrame_`) to guarantee song time never goes backward.

3. **Position History & Startup Safety ([`BassRuntime.cs`](../Assets/Script/Audio/Bass/BassRuntime.cs) & [`RenderAheadMixer.cpp`](../Native/YargAudio/src/RenderAheadMixer.cpp))**:
   - The game uses `BASS_Mixer_ChannelGetPositionEx`: you pass it the total delay (buffered audio + sound card delay), and BASS automatically calculates and returns the exact timestamp currently coming out of the speakers (taking tempo and speed changes into account).
   - **Extended Timestamp Memory (`BASS_CONFIG_MIXER_POSEX`)**: To calculate timestamps from delayed audio, BASS must remember what it recently decoded. By default, BASS only remembers the last 2 seconds—which fails if large buffers or 50% Practice Mode slow-motion stretch the delay past 2 seconds. We increase this memory to **10,000 ms (10 seconds)** so BASS never forgets past timestamps and lookups never fail.
   - On song start or seek (when the delay reaches before the start of the song), the native layer pins the heard position to `0.000s` rather than returning errors or negative timestamps.

---

## 6. Audio Input: How Microphones Work

### Recording Sources
Both ASIO and Non-ASIO (Shared Audio) input use native BASS recording sources:
- **ASIO** ([`BassAsioInput`](../Assets/Script/Audio/Bass/Asio/BassAsioInput.cs)): The ASIO hardware driver delivers incoming mic samples at hardware buffer rates and pushes them directly into a BASS push stream.
- **Shared Audio** ([`BassMicrophoneCapture`](../Assets/Script/Audio/Bass/BassMicrophoneCapture.cs)): A callback-free BASS recording channel receives incoming OS audio so managed GC cannot pause capture delivery.

### The Split-Stream Design
A microphone must feed two independent consumers simultaneously:
1. **Pitch Analyzer** ([`BassMicAnalyzer`](../Assets/Script/Audio/Bass/BassMicAnalyzer.cs)): Receives live audio immediately with **0 ms additional buffering** to calculate pitch and score vocal gameplay with minimal input lag.
2. **Live Vocal Monitor**: Feeds live singing through DSP effects (high-pass filters, boxiness scoop, bite, de-esser, noise gate, auto-leveler, compressor, and stage reverb) into the final output mixer.

Because reading from a stream consumes the data, [`BassMicSignal`](../Assets/Script/Audio/Bass/BassMicSignal.cs) splits each source into two separate decode streams:

```text
Incoming Audio (ASIO or Shared Push Stream)
  │
  ├──> Analysis Split (Slave)  ──► Bandpass EQ Filter ──► Pitch Analyzer (Scoring - 0 ms delay)
  │
  └──> Monitor Split  (Master) ──► FX Chain & Reverb  ──► Final Mixer (Live Playback)
```

### Shared Audio Recording Buffer
Shared recording uses BASS's native recording buffer. Monitor playback pulls from the recording channel directly, so managed GC does not stop capture delivery or monitor refills.

- **Multi-channel interfaces**: Multi-input interfaces (like a 2-channel Focusrite interface) expose each input as an independent microphone device in the settings menu via [`BassAsioMics`](../Assets/Script/Audio/Bass/Asio/BassAsioMics.cs) or [`BassMicManager`](../Assets/Script/Audio/Bass/BassMicManager.cs).

---

## 7. Device Switching & Driver Lifecycles

Device switching is orchestrated by [`BassAudioManager`](../Assets/Script/Audio/Bass/BassAudioManager.cs) and [`BassAudioRouter`](../Assets/Script/Audio/Bass/BassAudioRouter.cs):

1. **Seamless Switching**: When switching between Shared and ASIO devices (or changing output devices), active songs and microphone streams are detached, moved to the new output, and reconnected without interrupting game state.
2. **Driver Notifications**: If the player changes buffer sizes or sample rates inside their ASIO control panel, [`BassAsioDriver`](../Assets/Script/Audio/Bass/Asio/BassAsioDriver.cs) catches the driver reset event and automatically re-initializes the pipeline on the fly.
3. **Safe Shutdown**: Songs disconnect from the master mixer before native read-ahead buffers are disposed, preventing audio callbacks from reading freed memory.

---

## 8. Latency & Synchronization

To ensure gameplay notes and input timing remain accurately synchronized with what the player hears:
- **Heard Latency**: Calculated directly from the ASIO driver’s reported hardware buffer delay (`BassAsio.GetLatency()`) or the OS endpoint delay.
- **Song Position**: Calculated by taking the decoder's raw position and subtracting both the queued frames currently sitting in the read-ahead buffer and the remaining hardware output delay.
