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

Three main clocks are maintained in [SongRunner.cs](file:///c:/Users/Phil/Development/YARG/Assets/Script/Playback/SongRunner.cs):
1. **InputTime:** `(inputSystemTime - InputTimeOffset) * SongSpeed`
   - **InputTimeOffset:** The offset used so that `InputTime` is 0 at the start of the song (the first note of the chart). This offset is established during initialization (`InitializeSongTime`) by recording the input system time when playback starts and subtracting the starting delay (`SONG_START_DELAY` scaled by `SongSpeed`). This anchors the beginning of the song's chart timeline at exactly `0`.
2. **SongTime:** `InputTime + (AudioCalibration * SongSpeed)`
3. **VisualTime:** `InputTime + (VideoCalibration * SongSpeed)`

---

## 2. Playback and Buffer Latencies

There are two main sources of latency when playing audio:

### 1. Device Playback Latency (used for startup/resume positioning)
This latency represents the physical delay between when audio is decoded/written to the stream and when it is actually heard from the speakers. It is calculated in [BassLatencyProvider.GetPlaybackStreamLatency](file:///c:/Users/Phil/Development/YARG/Assets/Script/Audio/Bass/BassLatencyProvider.cs#L19):
- macOS: `DeviceOutputLatency`
- Windows / Other: `DeviceOutputLatency + DeviceBufferLatency`

### 2. Tempo Stream Latency (used for speed changes)
This latency represents the delay before a speed change command takes effect in BASS. It is calculated in [BassLatencyProvider.GetTempoStreamLatency](file:///c:/Users/Phil/Development/YARG/Assets/Script/Audio/Bass/BassLatencyProvider.cs#L33) as:
\[\text{Tempo Latency} = \text{Remaining Buffer Time} + \text{Command Latency}\]

- **Remaining Buffer Time:** The tempo channel buffers decoded audio (bounded by the user-configured buffer length in the settings). We query BASS for the available bytes remaining in the tempo channel's internal buffer (`Bass.ChannelGetData`) and convert them to seconds (`Bass.ChannelBytes2Seconds`).
- **Command Latency:** BASS operates on an asynchronous update thread that only checks for new commands periodically (every 5ms in YARG). We estimate this command delay as half of BASS's update period (`Bass.UpdatePeriod / 2000.0` seconds).

---

## 3. Sources of Desynchronization

Audio desynchronization in YARG can occur in five main scenarios:

1. **Song Start:** Starting playback is delayed by the **Device Playback Latency**.
2. **Resume after Pause:** Resuming playback is delayed by the **Device Playback Latency**.
3. **Seeking (Practice Mode Section Restarts):** Jumping to a new position is delayed by the **Device Playback Latency**.
4. **Speed Adjustments (Practice Mode D-Pad):** Changing the playback speed is delayed by the **Device Playback Latency** because we flush the buffers to apply the speed change immediately in-place, creating a short audio blip.
5. **Audio Buffer Underruns:** Stalls in the audio processing thread or OS scheduling can cause BASS to temporarily run out of audio samples, causing sudden timing discrepancies. Note that BASS runs on its own internal audio thread and clock, meaning hardware clock drift relative to the system clock is possible but rare in practice.

---

## 4. The Challenge of Latency for Synchronization

To align the audio to the target time, we make micro-adjustments to the speed of the song. These speed changes do not change the pitch as BASS has a good algorithm for time stretching. However, each speed change is subject to the **Tempo Stream Latency** described above, creating a **dead time** (lag) before the new speed is reflected in BASS's reported position.

### Speed Change Delay
Speed changes take effect after the **BASS Tempo Stream Buffer** latency (i.e., the remaining buffer from where the channel is currently playing). The physical **Device Playback Latency** is irrelevant here because the latency we care about is how long until the speed change takes effect; the playback latency will already have been accounted for in the last call to play the audio.

### Why Compensating for Latency is Necessary
In a naive control loop, the error is calculated as:
\[\text{Error} = \text{targetPosition} - \text{CurrentPosition}\]
Where `CurrentPosition` is the currently playing position of the song reported by BASS.

A speed correction is then applied for some amount of time to correct this error. However, because of the tempo buffer delay (**dead time**), a speed change commanded now **will not take effect** immediately in the reported BASS position.

During this delay, the controller sees that the error remains uncorrected. On the next update loop, it checks the position again, sees the same error, and sends another speed adjustment to correct it. This sequence repeats, causing the controller to continuously ramp up the speed correction. Once the speed adjustments finally clear the buffer and propagate, the audio accelerates/decelerates far too fast, overshooting the target and forcing the controller to overcorrect in the opposite direction. This creates speed oscillations as we repeatedly overshoot and undershoot the target.

---

## 5. How the Model Works

To eliminate these oscillations, YARG implements a predictive control structure in [BufferedPlaybackTimeline.cs](file:///c:/Users/Phil/Development/YARG/Assets/Script/Audio/Bass/BufferedPlaybackTimeline.cs).

Instead of using the raw position from BASS directly, it calculates a delay-free **Control Position**:
\[\text{ControlPosition} = \text{Raw BASS Position} + (\text{Commanded Integral} - \text{Buffered Integral})\]

The [BufferedPlaybackTimeline](file:///c:/Users/Phil/Development/YARG/Assets/Script/Audio/Bass/BufferedPlaybackTimeline.cs) stores a history of speed changes and calculates two positions by integrating the speed rates over time:
1. **`_commandedRateHistory` (Commanded Integral):** Integrates the commanded speed rates assuming they take effect **immediately**.
2. **`_bufferedRateHistory` (Buffered Integral):** Integrates the commanded speed rates assuming they take effect **after the dead time delay** (e.g., `now + tempoLatency` for speed changes, or `now + startupLatency` when starting/resuming).

The difference `(Commanded Integral - Buffered Integral)` represents the accumulated song progress that is currently "in flight" (buffered/delayed inside the tempo buffer but not yet reflected in BASS's reported raw position).

### How Error is Calculated
Every frame, the feedback loop calculates the true, delay-free error using the predictive `ControlPosition`:
\[\text{Error} = \text{targetPosition} - \text{ControlPosition}\]

### How Error is Converted to a Speed Change
\[\text{Adjustment} = \frac{\text{Error}}{0.1}\]

Since playback speed is in units of song seconds per real-world second, we cannot directly convert a time offset (`Error` in seconds) to a speed adjustment without a time-scaling factor. YARG chooses a target correction window of 0.1 seconds (100ms) because we want the error correction to be rapid, but we do not want the resulting speed adjustment to be so high that the audio warps audibly.

Dividing the sync error (in seconds) by `0.1` calculates the speed rate offset required to entirely close the sync gap over the next 100ms. For example, if the audio is 10ms behind (`Error = 0.01` seconds), playing 10% faster (`0.01 / 0.1 = 0.1` speed adjustment) for 100ms will bring the playback perfectly back in sync.

This adjustment is immediately added to the `Commanded Integral` history. Because `ControlPosition` includes the difference `(Commanded Integral - Buffered Integral)`, the speed change is reflected in the predictive `ControlPosition` on the very next frame. This allows the controller to see the effect of its action immediately, preventing it from sending duplicate adjustments while the speed change is still propagating through the audio buffer.

### The Full Control Loop

During normal gameplay, small speed adjustments are continuously computed by the [AudioSynchronizer](file:///c:/Users/Phil/Development/YARG/Assets/Script/Playback/SongRunner.cs#L701):

#### The Control Loop
1. **Error Sampling:** Every frame, the synchronizer samples the difference between the target position and the predictor's control position:
   \[\text{Error} = \text{targetPosition} - \text{ControlPosition}\]
2. **Deadband Filter:** If the absolute error is less than `SYNC_DEADBAND_SECONDS` (1.5 milliseconds), no adjustment is applied (`adjustment = 0`). This deadband filters out high-frequency noise and minor floating-point jitter.
3. **Proportional Adjustment:** If the error exceeds the deadband, a proportional correction is calculated:
   \[\text{Adjustment} = \frac{\text{Error}}{\text{CORRECTION\_TIME\_SECONDS}}\]
   - `CORRECTION_TIME_SECONDS` is set to `0.1` seconds, meaning the proportional gain is `10`. The controller attempts to eliminate the sync error in 100ms.
4. **Clamping:** The speed correction is clamped to `±SYNC_CLAMP` (±50% of the requested speed) to prevent radical audio warping in extreme drift cases.
5. **Pitch-Preserving Tempo Speed:** The adjustment is applied to the mixer using `_mixer.SetPlaybackSpeed(songSpeed, adjustment, shiftPitch: false)`. By passing `false` to pitch shifting, BASS changes the tempo without altering the audio pitch, keeping the synchronization adjustments transparent to the player.
6. **Timeline Update:** The adjustment is sent to the `BufferedPlaybackTimeline`, which schedules the speed change on the commanded and buffered histories, maintaining the predictive playback timeline model.

---

## 6. How Each Desync Scenario is Handled

This section outlines how the system uses the predictive timeline model and BASS stream positioning to seamlessly handle each playback transition.

All positioning operations (starting, resuming, seeking, or changing speed in-place) delegate to the internal `SetPosition_Internal` method in [BassStemMixer.cs](file:///c:/Users/Phil/Development/YARG/Assets/Script/Audio/Bass/BassStemMixer.cs#L245). This method calculates a target audio-channel position by pre-compensating for the total latency delay:
\[\text{PreparedPosition} = \text{TargetPosition} + (\text{PlaybackStartOffset} \times \text{SongSpeed})\]
Where:
- `TargetPosition` (passed as `position` to the method) is the desired audio playback time (relative to the audio file).
- `PlaybackStartOffset` is the total delay before audio is heard: `OutputLatency` + `AlignmentDelay`.
- `OutputLatency` is the calibrated playback latency (including device hardware latency).
- `AlignmentDelay` is the processing delay introduced by DSP effects (like pitch shift/whammy) on the audio stems.
- `SongSpeed` (represented by `_songSpeed`) is the current playback speed rate.

Depending on whether the resulting `PreparedPosition` is positive or negative, the system handles synchronization using one of two mathematical branches:
- **Case A (`PreparedPosition` >= 0):** Used when resuming/seeking in the middle of a song. Here, latency is compensated for by **seeking ahead** in the audio stream so that the audio catches up to the clock by the time it is heard.
- **Case B (`PreparedPosition` < 0):** Used during song start or pre-roll periods where we need to play audio earlier than time 0 (which is physically impossible to seek to). Here, latency is compensated for by clamping the seek position to 0 (or target) and **scheduling playback to start later** using the BASS mixer start delay.

---

### Case A: Positive Prepared Position (`PreparedPosition` >= 0)
This occurs when resuming or seeking in the middle of a song where the target position is far enough along that `TargetPosition` + (`PlaybackStartOffset` * `SongSpeed`) >= 0. In this case, latency is compensated for by seeking ahead in the audio streams.

1. **Seek Forward:** We seek all underlying BASS audio streams forward in the audio file to:
   \[\text{SeekPosition} = \text{PreparedPosition}\]
2. **Mixer Delay:** The streams are added to the mixer with a start delay of zero (`playbackDelay = 0`).
3. **Timing Alignment:** When playback starts, BASS begins playing from `PreparedPosition`. However, due to hardware and DSP latency, this audio takes exactly `PlaybackStartOffset` real-world seconds to be heard. In those `PlaybackStartOffset` seconds, the gameplay clock (running at `SongSpeed`) advances by exactly:
   \[\text{PlaybackStartOffset} \times \text{SongSpeed}\]
   Consequently, when the audio finally exits the speakers, the gameplay clock has caught up and matches the heard audio position perfectly.

---

### Case B: Negative Prepared Position (`PreparedPosition` < 0)
This occurs when the latency-compensated position is negative (e.g., during a countdown or pre-roll period). It means we need to start playing the audio *earlier* than the beginning of the file (time 0), or earlier than our target restart point.

Since we cannot seek to a negative position in BASS, we clamp the seek position and schedule the playback to start later using the BASS mixer instead:
1. **Clamp Seek Position:** The physical stream seek position is clamped to the target start point (e.g., `0` for song start, or `TimeStart` for a Practice Mode restart).
2. **Mixer Start Delay:** We calculate a start delay (`PlaybackDelay = -PreparedPosition` in song seconds) and add the stems to the BASS mixer with a scheduled delay. The mixer outputs silence during the pre-roll countdown, starting the audio playback at the exact moment the gameplay clock hits the target start time.

---

### How Scenarios Map to These Cases

#### Play Song from Beginning (Quickplay)
1. **Gameplay Clock Start:** The gameplay clock starts at a negative pre-roll time of `-2.0 * SongSpeed` (utilizing `SONG_START_DELAY`).
2. **Audio Setup (Case B):** The mixer is prepared at this negative time. BASS streams are clamped to start at `0`, but we schedule playback slightly early using the BASS mixer start delay. We subtract the playback latency from the pre-roll delay so that BASS starts playing the streams early enough to compensate for the latency.
3. **Smooth Countdown:** The gameplay track starts scrolling immediately during the silent pre-roll period. As the countdown reaches exactly `0.0`, the first sound waves exit the speakers because we scheduled playback precisely to account for the latency, aligning perfectly with the visual notes crossing the strikeline.

#### Restart Section / Seek (Practice Mode)
1. **Gameplay Clock Start:** The gameplay clock starts at `TimeStart - (PracticeRestartDelay * SongSpeed)`.
2. **Audio Setup (Case B):** The mixer is prepared at this negative pre-roll time relative to the target. BASS streams are clamped to start at `TimeStart`, but we schedule playback slightly early using the BASS mixer start delay. We subtract the playback latency from the pre-roll delay so that BASS starts playing the streams early enough to compensate for the latency.
3. **Smooth Countdown:** The gameplay track starts scrolling immediately from the pre-roll start time. As the clock reaches `TimeStart`, the first sound waves exit the speakers because we scheduled playback precisely to account for the latency, aligning perfectly with the track.

> [!NOTE]
> If `PracticeRestartDelay` is set to `0`, the prepared position becomes positive (`PreparedPosition >= TimeStart`). In this scenario, the system automatically switches from **Case B** to **Case A**, seeking the BASS streams forward by the latency offset. The audio and visuals remain perfectly synchronized immediately upon restart, though the first few milliseconds of audio (equal to the latency) are skipped to compensate for the hardware delay.

#### Pause / Resume
* **Pause:** We stop playback immediately. The timeline stops tracking and discards any pending speed change commands that had not yet taken effect.
* **Resume:** The system updates the audio calibration, measures the current device latency, and calls `PrepareAudioAt` using the current gameplay time. If the position is positive (middle of the song), it triggers **Case A** (seeking BASS forward to pre-compensate for latency). If it is within the pre-roll, it triggers **Case B**.

#### Speed Changes (Practice Mode Slider)
Changing the playback speed on the fly normally incurs a noticeable delay before the speed shift is actually heard. This delay is determined by BASS's tempo stream buffer (which is bounded by the user-configured buffer size setting). To make speed adjustments feel instantaneous at the cost of a minor audio gap (or brief blip):
1. **Instant Rebuild and Flush:** YARG performs a quick "seek-in-place" instead of an in-place speed adjustment. It captures the current gameplay time, pauses the mixer, and resets the synchronizer.
2. **Re-anchor:** It calls `PrepareAudioAt` at the captured gameplay time with the new speed. This completely destroys and rebuilds the active BASS streams, clearing the old tempo buffer and resetting all pending rate histories.
3. **Immediate Effect:** When playback resumes, the new speed takes effect immediately. This avoids the buffer-propagation delay, keeping the gameplay clock and audio perfectly in phase at the expense of a brief gap in the audio stream.
