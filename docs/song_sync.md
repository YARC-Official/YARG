# Song Playback Synchronization in YARG

This document explains how YARG coordinates gameplay clocks with audio playback, handles the inherent delay of the audio hardware and software streams, and maintains stable synchronization without audible pitch/speed oscillations.

---

## 1. Clocks

In YARG, the gameplay clocks are driven directly by the hardware input system's high-precision timer. This timer is retrieved via Unity's `InputState.currentTime` in the input update loop:
```csharp
// InputManager.cs
public static double CurrentInputTime => InputState.currentTime;
```

This ensures that:
- Audio, visuals, and chart events are all synced to this common clock.
- Inputs are independent of frame rate.

Three main clocks are maintained in [SongRunner.cs](../Assets/Script/Playback/SongRunner.cs):
1. **InputTime:** `(inputSystemTime - InputTimeOffset) * SongSpeed`
   - **InputTimeOffset:** The offset used so that `InputTime` is 0 at the start of the song (the first note of the chart). This offset is established during initialization (`InitializeSongTime`) by recording the input system time when playback starts and subtracting the starting delay (`SONG_START_DELAY` scaled by `SongSpeed`). This anchors the beginning of the song's chart timeline at exactly `0`.
2. **SongTime:** `InputTime + (AudioCalibration * SongSpeed)`
3. **VisualTime:** `InputTime + (VideoCalibration * SongSpeed)`

---

## 2. Playback and Buffer Latencies

There are two main sources of latency when playing audio:

### 1. Device Playback Latency (used for startup/resume positioning)
This latency represents the physical delay between starting output and hearing it. Shared output uses [BassLatencyProvider.StartupLatency](../Assets/Script/Audio/Bass/BassLatencyProvider.cs); ASIO output uses the driver's reported latency frames.

### 2. Tempo Stream Latency (used for speed changes)
This latency represents the delay before a speed command reaches output. [BassSongConnection.GetCommandDelay](../Assets/Script/Audio/Bass/BassSongConnection.cs) calculates it as:
`TempoLatency = (QueuedReadAheadFrames + EndpointDelayFrames) / SampleRate`

- **Queued Read-Ahead Frames:** PCM already rendered at the previous tempo and waiting in the native ring buffer.
- **Endpoint Delay Frames:** PCM already submitted to the shared or ASIO output endpoint.

### 3. Startup Position Advance Latency (variable start lag)
This is the delay between `ChannelPlay` and the first observed advance of the BASS channel position. It is separate from physical speaker/output latency.

#### Windows
On Windows, measurements show that startup latency tracks the device buffer plus one device period and one BASS update period:
`StartupLatency = DeviceBufferLength + DevicePeriod + UpdatePeriod`

Each component is clamped to a non-negative value. With YARG's typical 20ms device buffer, 10ms device period, and 5ms update period, this estimates 35ms of startup latency.

`Bass.Info.Latency` changes with the configured device buffer on Windows, so it must not be added to `DeviceBufferLength`; doing so counts the buffer growth twice.

#### Other Platforms
On other platforms with an available device buffer length, the best estimate is based on the configured device buffer length:
`StartupLatency = DeviceBufferLength = DevicePeriod * BufferMultiplier`

`ChannelPlay` can occur at any point within the device's callback period, so the exact delay varies by approximately half a device period around this estimate:
`StartupLatency` is approximately `DeviceBufferLength`, plus or minus `DevicePeriod / 2`.

For example, with a 10ms device period:
- 20ms device buffer: best estimate 20ms, approximately 15–25ms
- 40ms device buffer: best estimate 40ms, approximately 35–45ms

On these platforms, the device buffer length is the best single latency estimate. The device period determines the expected uncertainty.

#### macOS Platform Differences
On macOS, `Bass.DeviceBufferLength` is unavailable. Instead, macOS uses the device's output latency directly:
`StartupLatency = DeviceOutputLatency`
Where `DeviceOutputLatency` is retrieved from `Bass.Info.Latency`.

### Heard Position Through the Read-Ahead Buffer

The song mixer is a decode mixer created with `BASS_MIXER_POSEX`. The native read-ahead worker pulls this mixer into its PCM ring buffer. To report what reaches the listener, YARG calls `BASS_Mixer_ChannelGetPositionEx` with this output-frame delay:

`PositionDelay = QueuedReadAheadFrames + RemainingEndpointDelayFrames`

BASS maps that delayed mixer-output point back to the corresponding tempo-stream source position, including tempo changes and resampling. `BASS_CONFIG_MIXER_POSEX` retains 10,000 ms of source-position history so the lookup covers YARG's maximum 5,000 ms read-ahead buffer plus endpoint delay.

Immediately after a start, seek, or resume prefill, the requested delay can reach before the current mixer generation began. The native renderer first queries retained BASS history, which preserves position across live read-ahead buffer changes. If BASS has no earlier position and the delay provably predates the current generation, the renderer reports relative position `0`. Other BASS position failures are propagated instead of substituting the decode-head position or silently rewinding to the generation origin.

---

## 3. Sources of Desynchronization

Audio desynchronization in YARG can occur in five main scenarios:

1. **Song Start:** Starting playback is delayed by the **Device Playback Latency**.
2. **Resume after Pause:** Resuming playback is delayed by the **Device Playback Latency**.
3. **Seeking (Practice Mode Section Restarts):** Jumping to a new position is delayed by the **Device Playback Latency**.
4. **Speed Adjustments (Practice Mode D-Pad):** The gameplay clock changes speed immediately, while BASS continues consuming audio already queued at the old speed until the command passes through the **Tempo Stream Latency**. Practice Mode changes speed in place; it does not flush or rebuild the streams.
5. **Audio Buffer Underruns:** Stalls in the audio processing thread or OS scheduling can cause BASS to temporarily run out of audio samples, causing sudden timing discrepancies. Note that BASS runs on its own internal audio thread and clock, meaning hardware clock drift relative to the system clock is possible but rare in practice.

We handle starting, resuming, and seeking by pre-compensating their positions for latency. In-place Practice Mode speed changes are measured at the decoded PCM frontier so their buffered transition remains visible to the synchronizer. See [Section 6](#6-how-each-desync-scenario-is-handled). Because hardware/driver latency estimates are not exact, minor offsets may remain and require ongoing proportional correction.

---

## 4. The Challenge of Latency for Synchronization

To align the audio to the target time, we make micro-adjustments to the speed of the song. These speed changes do not change the pitch as BASS has a good algorithm for time stretching. However, each speed change is subject to the **Tempo Stream Latency** described above, creating a **dead time** (lag) before the new speed is reflected in BASS's reported position.

### Speed Change Delay
Speed changes take effect after the **BASS Tempo Stream Buffer** latency (i.e., the remaining buffer from where the channel is currently playing). The physical **Device Playback Latency** is irrelevant here because the latency we care about is how long until the speed change takes effect; the playback latency will already have been accounted for in the last call to play the audio.

### Why Compensating for Latency is Necessary
In a naive control loop, the error is calculated as:
`Error = targetPosition - CurrentPosition`
Where `CurrentPosition` is the currently playing position of the song reported by BASS.

A speed correction is then applied for some amount of time to correct this error. However, because of the tempo buffer delay (**dead time**), a speed change commanded now **will not take effect** immediately in the reported BASS position.

During this delay, the controller sees that the error remains uncorrected. On the next update loop, it checks the position again, sees the same error, and sends another speed adjustment to correct it. This sequence repeats, causing the controller to continuously ramp up the speed correction. Once the speed adjustments finally clear the buffer and propagate, the audio accelerates/decelerates far too fast, overshooting the target and forcing the controller to overcorrect in the opposite direction. This creates speed oscillations as we repeatedly overshoot and undershoot the target.

---

## 5. How the Model Works

YARG measures both ends of the rendered output pipeline in one native snapshot:

1. **Decode Head:** `BASS_Mixer_ChannelGetPositionEx` with zero delay reports the source position represented by the newest decoded PCM.
2. **Heard Position:** The same query with queued read-ahead frames plus remaining endpoint frames reports the source position reaching the listener.
3. **Output Delay:** The snapshot returns the exact output-frame delay used by the heard-position query.

The native snapshot holds the source lock and retries if callback consumption overlaps the query. Both positions and the frame delay therefore describe one queue state.

The synchronizer converts the decode head into a delay-free **Control Position**:

`ControlPosition = DecodeHeadPosition - (RequestedSongSpeed * OutputDelay) - (OutputLatency * RequestedSongSpeed)`

`RequestedSongSpeed * OutputDelay` projects the gameplay target from the current instant to the time when the decoded tail reaches the listener. `OutputLatency` applies the calibrated heard-target offset. Unlike a scheduled command predictor, the decode head already reflects tempo changes BASS has rendered, including partially applied transitions and variable queue depth.

### How Error is Calculated
Every frame, the feedback loop calculates:

`ControlError = targetPosition - ControlPosition`

### How Error is Converted to a Speed Change
`Adjustment = ControlError / CorrectionTime`

Since playback speed is in units of song seconds per real-world second, we cannot directly convert a time offset (`Error` in seconds) to a speed adjustment without a time-scaling factor. YARG calculates `CorrectionTime = Math.Clamp(0.100 + tempoLatency * 0.10, 0.100, 0.500)`.

This provides a bounded correction horizon that gently accounts for estimation variance on large buffers without crippling the controller's responsiveness:
- **Low-latency ASIO (20 ms buffer):** `CorrectionTime` $\approx 102\text{ ms}$ (target $\approx 100\text{ ms}$)
- **Standard Shared Audio (100 ms buffer):** `CorrectionTime` $\approx 110\text{ ms}$
- **Large Audio Buffer (5000 ms buffer):** `CorrectionTime` is capped at $500\text{ ms}$ (rather than stretching out to $5.0\text{ seconds}$).

The adjustment becomes visible to the controller when BASS renders it at the decode frontier. PCM already rendered at an older rate remains represented between the decode head and heard position instead of being approximated from command timestamps.

### The Full Control Loop

During normal gameplay, small speed adjustments are continuously computed by the [AudioSynchronizer](../Assets/Script/Playback/SongRunner.cs):

#### The Control Loop
1. **Error Sampling:** Every frame, the synchronizer samples the difference between the target position and the delay-free control position:
   `ControlError = targetPosition - ControlPosition`
2. **Dual-Threshold Hysteresis:**
   - **Start Threshold (`SYNC_START_SECONDS = 0.003` / 3.0 ms):** While in `Idle`, corrections only start if `|ControlError| >= 3.0 ms`.
   - **Stop Threshold (`SYNC_STOP_SECONDS = 0.0015` / 1.5 ms):** Once actively `Correcting`, adjustments continue until `|ControlError| < 1.5 ms`.
   - This asymmetric deadband prevents rapid oscillation between active and idle states.
3. **Proportional Adjustment & Clamping:** When correcting, the speed adjustment is calculated:
   `Adjustment = Math.Clamp(ControlError / CorrectionTime, MinimumAdjustment, SYNC_CLAMP)`
   - `SYNC_CLAMP` limits positive adjustment to `+2.00` (+200 percentage points).
   - `MinimumAdjustment` prevents synchronization from reducing effective playback below `0.50 * RequestedSongSpeed`. Extreme near-zero BASS_FX tempo rates can render excessive stretched PCM before a reversal reaches the decode frontier.
4. **Pitch-Preserving Tempo Speed:** The adjustment is applied to the mixer using `_mixer.SetPlaybackSpeed(songSpeed, adjustment, shiftPitch: false)`. This changes tempo without applying pitch shifting to temporary synchronization corrections.
5. **Settling Window:** When a correction ends, the synchronizer enters `Settling` for `tempoLatency + 25 ms`. This prevents another correction until the final base-speed command reaches measured output. No predictor bias offset is applied.

---

## 6. How Each Desync Scenario is Handled

This section outlines how the system uses measured BASS stream positions to handle each playback transition.

All positioning operations (starting, resuming, and seeking) delegate to the internal `SetPosition_Internal` method in [BassSong.cs](../Assets/Script/Audio/Bass/BassSong.cs). Requested speed changes outside active Practice Mode may also rebuild playback through this path. Practice Mode D-Pad speed changes do not; they update the active tempo stream in place. `SetPosition_Internal` calculates a target audio-channel position by pre-compensating for the total latency delay:
`PreparedPosition = TargetPosition + (PlaybackStartOffset * SongSpeed)`
Where:
- `TargetPosition` (passed as `position` to the method) is the desired audio playback time (relative to the audio file).
- `PlaybackStartOffset` is the full preparation advance: `OutputLatency` + `PlaybackStartDelay` + `AlignmentDelay`.
- `OutputLatency` is the calibrated target offset (including device hardware latency when automatic hardware-latency compensation is enabled).
- `PlaybackStartDelay` is the output-specific transport delay before prepared audio reaches the endpoint.
- `AlignmentDelay` is the processing delay introduced by DSP effects (like pitch shift/whammy) on the audio stems.
- `SongSpeed` (represented by `_songSpeed`) is the current playback speed rate.

Depending on whether the resulting `PreparedPosition` is positive or negative, the system handles synchronization using one of two mathematical branches:
- **Case A (`PreparedPosition` >= 0):** Used when resuming/seeking in the middle of a song. Here, latency is compensated for by **seeking ahead** in the audio stream so that the audio catches up to the clock by the time it is heard.
- **Case B (`PreparedPosition` < 0):** Used during song start or pre-roll periods where we need to play audio earlier than time 0 (which is physically impossible to seek to). Here, latency is compensated for by clamping the seek position to 0 (or target) and **scheduling playback to start later** using the BASS mixer start delay.

---

### Case A: Positive Prepared Position (`PreparedPosition` >= 0)
This occurs when resuming or seeking in the middle of a song where the target position is far enough along that `TargetPosition` + (`PlaybackStartOffset` * `SongSpeed`) >= 0. In this case, latency is compensated for by seeking ahead in the audio streams.

1. **Seek Forward:** We seek all underlying BASS audio streams forward in the audio file to:
   `SeekPosition = PreparedPosition`
2. **Mixer Delay:** The streams are added to the mixer with a start delay of zero (`playbackDelay = 0`).
3. **Timing Alignment:** `PreparedPosition` includes both the calibrated heard-target offset and the transport delays. While prepared audio traverses `PlaybackStartDelay`, the gameplay clock advances by the same duration. When audio reaches the endpoint, its heard position matches the calibrated heard target while the measured control position matches the input-clock target.

---

### Case B: Negative Prepared Position (`PreparedPosition` < 0)
This occurs when the latency-compensated position is before the beginning of the audio file (for example, during the initial countdown or a Practice Mode restart near the beginning of a song).

Since we cannot seek to a negative position in BASS, we clamp the seek position and schedule the playback to start later using the BASS mixer instead:
1. **Clamp Seek Position:** The physical stream seek position is clamped to `0`.
2. **Mixer Start Delay:** We calculate a start delay (`PlaybackDelay = -PreparedPosition` in song seconds) and add the stems to the BASS mixer with a scheduled delay. The mixer outputs silence during the pre-roll countdown, starting the audio playback at the exact moment the gameplay clock hits the target start time.

---

### How Scenarios Map to These Cases

#### Play Song from Beginning (Quickplay)
1. **Gameplay Clock Start:** The gameplay clock starts at a negative pre-roll time of `-2.0 * SongSpeed` (utilizing `SONG_START_DELAY`).
2. **Audio Setup (Case B):** The mixer is prepared at this negative time. BASS streams are clamped to start at `0`, but we schedule playback slightly early using the BASS mixer start delay. We subtract the playback latency from the pre-roll delay so that BASS starts playing the streams early enough to compensate for the latency.
3. **Smooth Countdown:** The gameplay track starts scrolling immediately during the silent pre-roll period. As the countdown reaches exactly `0.0`, the first sound waves exit the speakers because we scheduled playback precisely to account for the latency, aligning perfectly with the visual notes crossing the strikeline.

#### Restart Section / Seek (Practice Mode)
1. **Gameplay Clock Start:** The gameplay clock starts at `TimeStart - (PracticeRestartDelay * SongSpeed)`.
2. **Audio Setup:** The mixer prepares that absolute audio-file position plus the playback-latency advance. Most section restarts are therefore **Case A**: BASS seeks into the lead-in before `TimeStart`, and that lead-in audio plays during the countdown. If the requested lead-in reaches before the beginning of the file, the mixer uses **Case B**, clamps the stream to `0`, and schedules its start after the necessary silence.
3. **Smooth Countdown:** The gameplay track starts scrolling from the pre-roll position. At `TimeStart`, heard audio and the gameplay clock are aligned at the selected section boundary.

> [!NOTE]
> If `PracticeRestartDelay` is set to `0`, the mixer uses **Case A** for a section inside the song and seeks forward by the latency offset. Audio and visuals are synchronized immediately upon restart, though the first few milliseconds after `TimeStart` are skipped to compensate for playback latency.

#### Pause / Resume
* **Pause:** We stop playback immediately. Resume rebuilds and prefills the read-ahead pipeline from the current gameplay position.
* **Resume:** The system updates the audio calibration, measures the current device latency, and calls `PrepareAudioAt` using the current gameplay time. If the position is positive (middle of the song), it triggers **Case A** (seeking BASS forward to pre-compensate for latency). If it is within the pre-roll, it triggers **Case B**.

##### Play/Pause Delay Differences
When resuming, the delay before the audio starts is dictated by the platform-specific `StartupLatency`:
- **macOS:** Determined by `Bass.Info.Latency` (`DeviceOutputLatency`).
- **Windows:** Device buffer length plus one device period and one BASS update period.
- **Other Platforms:** Determined by the configured `Bass.DeviceBufferLength`.

#### Speed Changes (Practice Mode D-Pad)
Practice Mode deliberately changes speed without seeking, pausing, flushing, or rebuilding playback:
1. **Preserve Position and Correction:** YARG captures the current gameplay time, updates `SongSpeed`, and sends the new requested speed to the mixer while preserving the synchronizer's active correction.
2. **Re-anchor Gameplay:** The gameplay timeline is re-anchored at the captured time, so changing the clock rate does not jump `InputTime` or `VisualTime`.
3. **Measure Buffered Transition:** BASS continues outputting samples already buffered at the old rate. The atomic position snapshot measures the decoded tail and heard head around that queued PCM.
4. **Continue Playback:** The measured control position exposes any target mismatch created by the old-rate audio still in flight. The synchronizer corrects that mismatch without a seek gap or flush blip.

Other callers of `SetSongSpeed` use the rebuild path while playback is active. That path pauses and prepares audio again at the current gameplay position so requested speed and audible playback restart together. Practice Mode uses `AdjustSongSpeedInPlace` specifically to avoid that interruption.

> [!IMPORTANT]
> Running the Unity Editor from the Flatpak build of Unity Hub can make `BASS_ChannelGetPosition` exhibit PipeWire-quantum-sized timing modulation. The same position test is stable when the Editor and standalone BASS test run through the host audio environment. Do not use a Flatpak-launched Unity Editor to evaluate Linux BASS position stability or song-sync behavior; this is an artifact of the Flatpak audio environment and is not representative of a native YARG build.
