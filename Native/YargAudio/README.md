# YargAudio native audio library

Portable 64-bit desktop native Gain, Freeverb, noise gate, scheduled one-shot
source, and song read-ahead stream.

Managed code reaches them through the C ABI and `SafeHandle` wrappers.
Scheduled one-shots keep their PCM, schedule, BASS stream callback, and
lifecycle state native. Read-ahead stream owns worker, ring, and decode stream
used by both Shared and ASIO playback.
It accepts float BASS channels, or channels processed as float through
`BASS_CONFIG_FLOATDSP`. Failed native attachment disables normalization for that
mixer; there is no Burst fallback.

Core BASS symbols resolve from already-loaded `bass.dll`, `libbass.so`, or
`libbass.dylib`. This prevents channel handles from being passed to a second
BASS instance. Missing required symbols fail attachment.

Gain state uses atomic bit storage. Freeverb preserves managed topology:
sample-rate-scaled comb/all-pass delay lines, stereo spread, wet/dry mixing,
callback-safe reset, channel locking, and safe DSP removal.
Noise gate uses channel-linked envelope detection, smooth gain transitions,
callback-safe reset, channel locking, and safe DSP removal.

Audio structure, native DSP boundary, scheduled one-shot source, and output split
are documented in [`docs/audio_pipeline.md`](../../docs/audio_pipeline.md).

## Read-ahead runtime invariants

- Worker exclusively pulls song decode mixer.
- BASS stream callback exclusively consumes ring and zero-fills underruns.
- Flush closes consumer, waits active callbacks, stops worker, then clears ring.
- Final mixer must stop consuming stream before native object is destroyed.
- Buffer changes rebuild renderer only while consumer and worker are stopped.
- Source position subtracts queued ring frames plus endpoint delay from caller.

## Build

Install CMake 3.25+ and platform C++ tools.

```bash
cmake --preset linux-x64
cmake --build --preset linux-x64-release
ctest --preset linux-x64-release
```

Use `windows-x64-release` or `macos-universal-release` on those platforms.

## Compatibility

Linux binaries are built inside an Ubuntu 20.04 container (glibc 2.31,
gcc 10) to keep the glibc floor at 2.31, matching Unity 6's Ubuntu 20.04
support. Toolchains linked against glibc >= 2.34 emit `dlopen@GLIBC_2.34`
and produce a plugin that fails to load on Ubuntu 20.04, Debian 11, and
RHEL 8. CI must not move to newer containers or runners without re-evaluating
this.
