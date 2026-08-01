# YargAudio native DSP library

YargAudio hosts allocation-free native Gain and Freeverb BASS DSP callbacks.
Managed code reaches them through the C ABI and `SafeHandle` wrappers.

Core BASS symbols resolve from already-loaded `bass.dll`, `libbass.so`, or
`libbass.dylib`. This prevents channel handles from being passed to a second
BASS instance. Missing required symbols fail attachment.

Gain state uses atomic bit storage. Freeverb preserves managed topology:
sample-rate-scaled comb/all-pass delay lines, stereo spread, wet/dry mixing,
callback-safe reset, channel locking, and safe DSP removal.

## Build

Install CMake 3.25+ and platform C++ tools.

```bash
cmake --preset linux-x64
cmake --build --preset linux-x64-release
ctest --preset linux-x64-release
```

Use `windows-x64-release` or `macos-universal-release` on those platforms.
