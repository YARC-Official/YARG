# Signalsmith dependencies

Signalsmith Linear is an unmodified header-only dependency. YARG's time stretching engine (`src/stretch/YargStretch.h`) is derived from Signalsmith Stretch 1.3.2 and uses Linear 0.3.1 for STFT and FFT processing.

| Library | Version | Upstream commit | License | Location |
| --- | --- | --- | --- | --- |
| [Signalsmith Linear](https://github.com/Signalsmith-Audio/linear) | 0.3.1 | 5668673560146a9cfe38c25315071e3fd68c8317 | [MIT](signalsmith-linear/LICENSE.txt) | `third_party/signalsmith-linear/` |
| [Signalsmith Stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch) | 1.3.2 | 57b93f4e9206a089a45387eaa39bdc9f310d3308 | [MIT](signalsmith-stretch-LICENSE.txt) | Integrated in `src/stretch/YargStretch.h` |

The current reconstruction, transient protection, stereo handling, noise processing, and spectral shaping are documented in [Time Stretching in YARG](../../../docs/time-stretch.md). Phase reconstruction is adapted from Průša and Holighaus, *Phase Vocoder Done Right*, EUSIPCO 2017, pp. 976–980.

Both original MIT notices are included in `Assets/StreamingAssets/YargAudio-Licenses.txt`, so Unity includes them alongside the native plugins in player builds.
