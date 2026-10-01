# Signalsmith dependencies

Signalsmith Linear is an unmodified header-only dependency. YARG's time stretching engine (`src/stretch/YargStretch.h`) is an in-house engine based on Signalsmith Stretch 1.3.2 and Linear 0.3.1, containing YARG's transient protection and PVDR phase integration. PVDR is adapted from Holighaus and Průša, "Phase vocoder done right", Proc. EUSIPCO 2017, pp. 976–980.

| Library | Version | Upstream commit | License | Location |
| --- | --- | --- | --- | --- |
| [Signalsmith Linear](https://github.com/Signalsmith-Audio/linear) | 0.3.1 | 5668673560146a9cfe38c25315071e3fd68c8317 | [MIT](signalsmith-linear/LICENSE.txt) | `third_party/signalsmith-linear/` |
| [Signalsmith Stretch](https://github.com/Signalsmith-Audio/signalsmith-stretch) (Base) | 1.3.2 | 57b93f4e9206a089a45387eaa39bdc9f310d3308 | [MIT](signalsmith-linear/LICENSE.txt) | Integrated in `src/stretch/YargStretch.h` |

`YargStretch` requires Linear's `stft.h` and `fft.h`.

## YARG stretch engine (`src/stretch/YargStretch.h`)

- Transient detection (`protectTransients`): high-frequency spectral flux over three bands above 1500 Hz (`setTransientFrequency`). Compares each bin with the maximum previous magnitude within three bins on either side, suppressing false attacks caused by pitch movement. Fires when a region exceeds absolute power, 0.3% of total power, and 25% filtered rising-energy ratio. Per-bin attack shaping retains its separate 45% unfiltered rising-energy threshold. Holds protection for half a block, then ignores new triggers for 1.5 blocks (~120ms) so decay bumps cannot machine-gun.
- Split-band diffusion: lows stay phase-coherent up to 6x stretch (`maxCleanLow`), so 20% speed stays clean for vocals and bass, while highs diffuse above 1x (split at 0.11 of sample rate) to de-ring hats. Randomization is disabled while transient protection holds.
- Low-speed tonal neighbors: above 6x stretch, low bins preserve their phase difference from a processed parent when either bin exceeds the smoothed spectral background. This avoids randomizing the immediate neighbors of strong tones; bins without a strong neighbor retain diffusion.
- High-peak shelter (`PEAK_SHELTER_RADIUS`, `PEAK_SHELTER_RATIO`): strong high peaks shelter neighbors within four bins when the peak exceeds four times the smoothed background, so sustained high tones keep their skirts while true noise floor still diffuses.
- Extreme-stretch vertical lock: past 6x stretch, low bins inherit phase through the directly measured input ratio instead of the amplified frequency-direction gradient, whose estimate modulation otherwise dominates swept tones there. The gradient path stays for moderate stretch, where it measures better.
- Smoothed stretch factor (`TIMEFACTOR_SNAP_RATIO`): the per-spectrum input advance is fractional (89.6 samples at 20% speed) while sample positions are integers, so the measured factor alternates and wobbles every vertical twist. An exponential moving average (0.05) smooths the factor used for continuous twist magnitudes at slowdown; deviations above 10% snap through for real rate changes, regime decisions keep the raw factor, unity/fast paths are bit-identical, and the silence shortcut keeps tracking per-call ratios so the estimate never goes stale.
- Reassigned phase gradients: two extra analyses use the derivative of the analysis window and its centered time-weighted form. Their complex ratios with the ordinary spectrum estimate temporal and frequency-direction phase gradients. Temporal gradients are averaged across output frames; frequency gradients are averaged across parent/child bins before propagation. The existing half-bin rotation, pitch mapping, phase traversal, stereo reference and transient overrides remain in place. Both analyses use the existing processing-step schedule, restore the ordinary window after each step, and add no latency or callback allocations.
- Transient phase path: protected spectra bypass PVDR parent propagation and use vertical-only phase locking, while texture re-synthesis is bypassed (see `NoiseMorph.h`). PVDR traversal (`preparePvdrTraversal`) is adapted from Holighaus and Průša, "Phase vocoder done right", EUSIPCO 2017.
