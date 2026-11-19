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
