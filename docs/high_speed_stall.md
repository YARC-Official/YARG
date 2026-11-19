# Low frame rate at very high song speeds

At 2000% song speed and above, gameplay drops to about 1 FPS. The main thread is not doing heavy work. It is blocked inside the per-frame audio position read, waiting on a native mutex that the audio render worker almost never releases.

## Short version

1. At 20x, every 128 output frames need about 2,560 input frames from every stem. Each stem is decoded and processed, mixed, compressed and time-stretched. The render worker can barely keep up: it runs about 4% faster than real time, with chunk spikes well above the real-time budget.
2. The worker only pauses when its ring buffer is full. Because it can't fill the buffer, it never pauses. It takes `sourceMutex_`, renders a chunk, releases it, and takes it again right away.
3. Every frame, `SongRunner` reads the audio position through `StemMixer.GetSyncPosition()`. That call reaches the native read-ahead snapshot, which needs the same `sourceMutex_`.
4. `std::mutex` is not fair. When the worker releases the mutex and immediately takes it again, the waiting main thread rarely wins. A single position read then waits 0.3 to 2.7 seconds, and that wait is the frame time.

So two things combine. Audio rendering at 20x has almost no spare time, and the game thread reads the position through a lock that the starved worker holds nearly all the time.

## Call path

Main thread, every frame:

- `GameManager.Update` → `SongRunner.Update` → `UpdatePlayback` (`Assets/Script/Playback/SongRunner.cs`)
- `AudioSynchronizer.Synchronize` → `_mixer.GetSyncPosition()` (`SongRunner.cs`, inside `lock (this)` in `YARG.Core/YARG.Core/Audio/StemMixer.cs`)
- `BassSong.GetSyncPosition_Internal` → `TryGetSongPositionSnapshot` → `BassSongConnection` → native `ReadAheadStream::getPositionSnapshot`
- `RenderAheadMixer::sourcePositionSnapshotAfterLock` takes `std::lock_guard lock(sourceMutex_)` (`Native/YargAudio/src/RenderAheadMixer.h`)

Render worker (`RenderAheadMixer::run`, `Native/YargAudio/src/RenderAheadMixer.cpp`):

```cpp
while (!stopping_) {
    if (ring_.available() >= targetFrames()) { /* sleep until low watermark */ continue; }
    {
        std::lock_guard sourceLock(sourceMutex_);
        rendered = source_->read(scratch_.data(), requested);   // 128 frames
        ...
    }
}
```

`source_->read` pulls the whole song chain while holding the mutex: stem decode, per-stem DSP (Quality reverb and the rest) on the BASS mixer processing threads, the stem mixer and compressor, the YargStretch tempo stream, and the song mixer. At 20x that read takes most of the wall-clock time. The only gap between releasing and re-taking the mutex is a few instructions.

## Measurements

The game ran in the editor in play mode, song "Drained" (Vangough, RBCON, 8 stems), Quality effects mode (YargStretch tempo, Quality reverb), one player. Probes were added at runtime through the PlayerLoop with `unity command script eval`. No project code was changed. Each probe logged per-frame timings, read-ahead stats from `BassReadAheadStream.GetStats()`, and a timed extra `GetSyncPosition()` call and BASS output-channel lock.

| Speed | In-song FPS | Snapshot avg | Snapshot max | Underrun events | Ring fill (target 1368) | Audio behind chart at end |
|-------|-------------|--------------|--------------|-----------------|-------------------------|---------------------------|
| 1000% | 60          | 2.2 ms       | 20 ms        | 0               | ~1416 (full)            | 0 s                       |
| 1500% | 53          | 7.5 ms       | 124 ms       | 0               | ~1373 (full)            | 0 s                       |
| 2000% | ~1          | 573 ms       | 2702 ms      | 346             | ~399                    | ~10 s                     |

Supporting observations at 2000%:

- **The main thread is blocked, not busy.** Sampling `/proc` thread states, the main thread used 0 to 10% CPU during the song. It sat in `S` (sleeping) for runs of 0.5 to 2.1 s. Before and after the song it used 80 to 90%.
- **The wait happens while holding the `StemMixer` lock.** A watchdog thread tried `Monitor.TryEnter` on the `StemMixer` during long frames, and the lock was held for the whole stall. That matches `Synchronize` → `GetSyncPosition()`.
- **It isn't the BASS output lock.** `Bass.ChannelLock` on the output mixer took about 0.001 ms every frame, while `GetSyncPosition()` took up to 2.7 s in the same frame. In that path, the only other place to block is `sourceMutex_`.
- **The worker has almost no spare time.** At 20x, production was about 45.8k frames/s against the 44.1k needed. The ring stayed around 400 frames against a 1368-frame target, and the largest single 128-frame render was about 8.7 to 10.5 ms. That is 3 to 4 times the 2.9 ms the chunk lasts. Before the stems started, renders peaked at 1.7 ms.
- **Rendering is parallel and expensive.** During the song, the render worker (around 60% CPU) and about 8 BASS mixer processing threads (20 to 50% CPU each, one per stem; `BassStemPipeline.UpdateThreading`) used about 3 cores combined. The worker spends much of its own wall time waiting for those threads inside `source_->read`, still holding `sourceMutex_`.
- **Sync correction makes it worse.** Once audio falls behind, `AudioSynchronizer` pins the correction at the stretch clamp (+0.5, `STRETCH_SYNC_CLAMP`). That asks for 20.5x, which means even more input per output frame. The error kept growing anyway, to 10 to 17 s of song time.
- **The stall stops when rendering stops.** After the last stem ends, the ring fills (1379 to 1464 frames), the worker sleeps, and the snapshot drops to 0.01 ms.

At 1500% the same contention is already visible: 7.5 ms average and 124 ms worst case per position read. The worker still keeps its buffer full, though, so it sleeps between refills and the main thread gets in.

## Why the 3000% report looked like a single 3-second frame

In the Unity profiler (non-deep), all of this lands as self time of `GameManager.Update()`. Native waits and unmarked managed calls are attributed to the nearest marker. A 2.9 s frame with 99.5% self time in `GameManager.Update` is one blocked `GetSyncPosition()` call.

## What is not the cause

- Rendering, present and GC: under 3 ms per frame in the capture.
- Gameplay CPU (note engines, track players): the main thread was idle during the stall.
- Prefill or flush timeouts: they only run on start, seek and rebuild, not per frame.
- BASS output channel lock: measured at about 0.001 ms.

## A second lock with the same problem

The worker reads with `BASS_ChannelGetData` on the song mixer, and BASS holds that channel's internal lock for the whole read. Any main-thread call that locks the song mixer or its tempo source gets starved the same way. At 2000%, `BASS_ChannelLock` on the song mixer measured up to 864 ms per call.

The per-frame path hits this when sync correction changes speed. `BassSong.SetPlaybackSpeed_Internal` re-anchors every one-shot channel: `BassOneShotChannel.Reanchor` calls `Bass.ChannelGetPosition` on the tempo stream, then the native `resync`, which calls `lockChannel` on the song mixer. This song has 3 one-shot channels, so a single speed change can take more than 2 s.

## Fix

The render worker now steps aside whenever the main thread needs the source.

- `RenderAheadMixer` counts active source accessors in `sourceAccessors_`. After each chunk, the worker spins with `std::this_thread::yield()` until the count is zero, or until it is stopping (`yieldToSourceAccess`).
- Position reads (`sourcePositionSnapshotAfterLock`, `bufferedSourcePositionAfterLock`, `bufferedSourcePosition`) register through a scoped `SourceAccess` guard before taking `sourceMutex_`.
- `yarg_read_ahead_stream_begin_source_access` and `yarg_read_ahead_stream_end_source_access` expose the same counter. `BassSong.SetPlaybackSpeed_Internal` wraps the tone refresh and one-shot re-anchor in them, through `BassSongConnection` and `BassReadAheadStream`.

The worst-case wait is now the rest of one chunk render. Position values and lock order don't change. The worker still holds `sourceMutex_` for every read, and readers still see a consistent ring and BASS position.

Tests in `Native/YargAudio/tests/RenderAheadMixerTests.cpp` cover this. One keeps the worker permanently busy and checks that 50 position reads each finish in under 50 ms. Before the fix, the reads were starved until the ring filled. The other checks that the worker stops producing while source access is held and resumes afterward.

### Results after the native part of the fix

Same setup as before, with the position-read fix only (before the one-shot change):

| Speed | In-song FPS | Snapshot avg | Snapshot max | Frames over 100 ms |
|-------|-------------|--------------|--------------|--------------------|
| 2000% | 49          | 2.5 ms       | 8.0 ms       | 2                  |
| 3000% | 53          | 4.0 ms       | 8.2 ms       | 0                  |

The one remaining 2.2 s frame at 2000% was the one-shot re-anchor described above. The `BassSong` change targets it, but it has not been measured in-game yet.

## Still open

The audio itself still can't keep up at these speeds. Underruns continue, and audio falls further behind the chart: 17 s by the end at 2000%, about 100 s at 3000%. The game stays responsive, but what you hear drifts. Options:

- **Reduce render cost at extreme speeds.** Above some speed (around 15 to 20x on this machine), drop per-stem Quality reverb and other per-stem DSP, or switch to the cheaper BASS_FX tempo engine. Or cap the speed. Today `SongRunner.ClampSongSpeed` allows up to 5000%.
- **Stop correction from feeding the overload.** While the read-ahead is underrunning, a positive sync adjustment only adds input work. It could be suppressed while `UnderrunEvents` is increasing.

Other main-thread calls that lock the song mixer, such as pause, seek and output-channel changes, are not wrapped. They are one-off user actions rather than per-frame calls, but at extreme speeds they can still stall for a moment.

## 15 FPS at 5000% on most songs: the whammy pitch shifter

After the position-read fix, most songs still ran at about 15 FPS at 5000%. The test song was "The Dark Side" (Muse, 4 OGG stems: guitar, bass, drums, vocals) in Performance effects mode (BASS_FX tempo, whammy oversample 8), one guitar player.

### What happens

Guitar, bass and rhythm stems get BASS's FFT pitch shifter for whammy (`BassHelpers.SetPitchParams`). It is attached to both the dry split and the reverb split of each stem, so this song had four instances. They sit before the tempo stream, so at 50x they process 50 times as much audio as at 1x. They also run on every sample whether or not anyone touches the whammy.

At 50x that makes one 128-frame output chunk take 60 to 90 ms to render, against about 3 ms of audio. Each per-stem mixer thread and the render worker ran at about 85% CPU, and the worker produced about half of real time. Every main-thread source access (position snapshot, speed change) waits for the chunk in progress to finish, because of the preemption fix above. A whole chunk now lasts about 64 ms, so frames took about 70 ms.

### Measurements (The Dark Side, 5000%, editor)

| Whammy FX | Frame time (median) | Worst chunk render | Underrun events | Audio behind chart at end |
|-----------|---------------------|--------------------|-----------------|---------------------------|
| On        | ~70 ms (~14 FPS)    | 92 ms              | 290             | ~134 s                    |
| Off       | 16.7 ms (60 FPS)    | 11 ms              | 0               | ~0 s                      |

With the whammy effect off, nothing else changed. Snapshot reads dropped to about 0.01 ms (with occasional 5 to 7 ms), the ring stayed full, and sync error settled at 0.

### Fix

The BASS_FX pitch shifter is replaced by a native YargAudio DSP, `yarg_pitch_shift_dsp` (`Native/YargAudio/src/dsp/PitchShifter.cpp`). It works on the waveform rather than the spectrum:

- A read tap trails the input by a delay and moves at the pitch ratio, so the delay drifts by `1 - ratio` per frame.
- When the delay drifts about 12.5 ms past its rest point, the tap crossfades over 12 ms to a new position about one jump (15 to 35 ms) away. The new position is the one whose upcoming audio correlates best with the old tap, found with a decimated search followed by a full-rate refinement. If the best match correlates below 0.8, the splice waits 2.5 ms and searches again, for up to 5 ms, before committing. Well-matched splices use a linear crossfade and poorly matched ones an equal-power crossfade.
- The latency is fixed at about 34.7 ms (1529 frames at 44.1 kHz). `BassStemPipeline.AttachPitchShift` reads it from the DSP and uses it as the stem's alignment delay. The delay is only counted when the shifter attached to both the dry and the reverb split.
- When the whammy is released, the tap splices back to within 2.5 ms of the rest point, then glides there at 0.4%. Once there, the DSP copies its delay line straight to the output, so an idle shifter costs two `memcpy` calls per block.
- Seeks reset the delay line through `BassStemChannel.SetPosition_Internal`, like the reverb.

Whammy works at every speed again. The 500% cutoff from the first version of this fix is gone.

Cost, measured with the native test (`PitchShifterTests.cpp`, stereo, 44.1 kHz): about 72,000x realtime at rest, and 530x while bending white noise, the worst case because every splice retries. Six bending instances at 50x need 300x, so even that worst case fits on about half of one core.

Quality, measured against BASS_FX on recorded guitar clips (`Native/YargAudio/build/pitch-ab`, rendered through real BASS with the whammy wobbling and then held):

- At rest, the output is exactly the input delayed. BASS_FX is never an exact copy.
- Splice correlation had a median of 0.98 to 1.0. The worst splice per clip was 0.79 or better on five of six clips; one had a single forced splice at 0.17.
- While the whammy moves, the volume of 20 ms windows changes about as much as in the dry signal. With BASS_FX it changes 3 to 5 times as much.
- Second-difference click energy stays near the dry signal's level. With BASS_FX it is 1.5 to 4 times higher.

### Result with the fix

Same song and setup, patched build, whammy setting on, whammy amount 5 semitones. Guitar and bass were bent continuously (a 3 Hz wobble) for the whole song.

| Frames in song | Average FPS | Median frame | 95th percentile | Worst frame | Underrun events | Sync error |
|----------------|-------------|--------------|-----------------|-------------|-----------------|------------|
| 282            | 59.6        | 16.7 ms      | 25.6 ms         | 40.5 ms     | 0               | ~0 s       |

The worst chunk render was 13.1 ms, compared with 92 ms for BASS_FX. The one frame over 33 ms came at the song's end.

### Limits

- The shifter crossfades between two points in time, so it can be heard on dense material with no clear period: the jump never lines up perfectly, and the 12 ms crossfade sounds slightly phasey. The retry search avoids most bad splices. The ones it can't avoid use an equal-power crossfade.
- During a bend, the stem drifts up to about ±20 ms against the other stems, as with any time-domain shifter. It returns to exact alignment within about 0.6 s of release.
- The native plugin must export `yarg_read_ahead_stream_begin_source_access`. The plugin committed with the position-read fix did not, so `BassSong.SetPlaybackSpeed_Internal` threw `EntryPointNotFoundException` on every speed change until the plugin was rebuilt. The pitch shifter raised the ABI to 28, so Windows and macOS plugins need rebuilding too.
- Songs whose other per-stem DSP (Quality reverb on many stems) can't keep up at extreme speeds will still block the main thread for one chunk per source access. That cost is bounded by chunk render time, which grows with speed.
