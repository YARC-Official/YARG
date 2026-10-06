# Time Stretching in YARG

YARG changes playback speed in Practice Mode and makes temporary speed corrections to synchronize audio with gameplay. Choose **Quality** in the **Effects** dropdown under **Sound** settings to use YargStretch. **Performance** uses BASS_FX. This setting also chooses the corresponding reverb implementation.

All stems are mixed before one tempo stream processes the combined audio. The custom engine does not maintain separate playback clocks for individual stems. Its frequency processing can still affect different instruments differently because several sources can overlap within a frequency bin.

## Processing and Timing

YargStretch is derived from Signalsmith Stretch 1.3.2 and uses the bundled `signalsmith-linear` STFT and FFT code. It analyzes overlapping windows, modifies their frequency spectra, and reconstructs the waveform through normalized overlap-add.

The native adapter targets a 60 ms window, rounded up to a multiple of 512 samples, with a minimum of 1024 samples. The synthesis hop is one eighth of that window. At 44.1 kHz, the window is 3072 samples (69.7 ms), and the hop is 384 samples (8.7 ms). The adapter serves BASS requests from 256-frame processing blocks, carrying fractional input-frame counts between blocks.

At 50% playback speed, input advances half as far as output. At 200%, it advances twice as far. Practice Mode exposes 10% through 200%; the native adapter accepts a wider range for other callers, including temporary synchronization corrections.

Startup and seeks reset and prime the engine. Priming supplies the analysis latency and discards the synthesis warm-up output. The adapter records each processed block's input-frame mapping; playback-position queries interpolate that history. Read-ahead and endpoint delay are handled by the surrounding song pipeline. Processing does not add independent stem offsets or insert local transient speed changes.

The custom engine reports a command-response estimate of 256 frames, about 5.8 ms at 44.1 kHz. This is the processing-block interval, not a measurement of the complete audible response of the overlapping windows. See [song synchronization](song_sync.md) and [the audio pipeline](audio_pipeline.md) for buffering and control behavior.

## Preserving Audio at 100%

When the current processing call consumes and produces the same number of samples, the analysis and synthesis hops match, and no pitch or formant mapping is active, the engine preserves each channel's source spectrum instead of reconstructing its phase from estimates. Startup and seeks at 100% also use this path. Buffering and normalized overlap-add remain in place.

Playback started or reset at that rate uses source phase immediately. After a speed change, a shared weight increases over one analysis window. The engine rotates reconstructed phase toward source phase while preserving spectral magnitude, then copies the source spectrum once the weight reaches one. This avoids an abrupt phase reset that could make overlapping windows partially cancel.

Noise replacement is disabled on this path, and the audible envelope EQ correction fades out with the same weight. The EQ continues updating its filter state so later speed changes can resume processing normally.

Gameplay can temporarily request a different effective speed even when the selected song speed is 100%. Those hops use phase reconstruction, and the return to source phase begins when the input and output hops match again. Separate pitch or formant mapping also uses reconstruction.

## Phase Reconstruction

A phase vocoder estimates how a sound's phase progresses so that changing window spacing does not simply change its pitch. YARG estimates frequency from the imaginary part of its Gaussian-window time-weighted analysis relative to the source spectrum. The real part estimates timing within a window. Measured source-phase progression cross-checks the temporal prediction. This reuses the existing time-weighted FFT and avoids approximating the frequency gradient from neighboring log magnitudes, which can be inaccurate where partials overlap. Synthetic overlapping-harmonic tests show improved frequency stability at 35% playback speed; improvement on dense vocal mixes still requires listening verification.

Reconstruction uses a complex tridiagonal system. Temporal predictions anchor individual bins; compatible neighboring bins supply frequency constraints. Significant bins share phase relationships, while weak bins cannot couple separate significant regions. A forward elimination and backward substitution solve the system. This implementation does not use the earlier loudest-first heap traversal.

Without pitch or formant mapping, reconstruction then restores bin magnitudes and refines phase with one ascending and one descending coordinate sweep. The sweeps reuse the original temporal targets and frequency constraints, hold magnitudes fixed, and leave attack-reset and insignificant bins unchanged.

Stable low-frequency peaks and prominent high-frequency peaks receive stronger temporal anchors. Qualified low-frequency references cross-check their estimated advance against measured source phase. These corrections are bounded; they cannot separate unresolved instruments within the same bins.

Each bin uses its strongest channel as the reconstruction reference. Other channels retain their source phase relationship to that reference. At a selected attack reset, each channel uses its own source phase.

## Attack Protection

Attack protection is enabled when the stretch factor exceeds 1.1, corresponding to playback below approximately 90.9%. It is not the mechanism used to preserve 100% audio.

During eligible slowdown without pitch or formant mapping, the detector collects rising spectral regions and estimates their position within the analysis window. The regions reset together when enough of the selected energy has crossed the timing threshold. Selection and repeat suppression are bounded in source time. Pitch and formant processing use different reset selection.

The noise path is bypassed during the initial protection interval and around the delayed reset. Seeks, resets, sustained silence, and leaving the eligible path clear the transient event. Overlapping attacks and instruments remain limitations; protection does not guarantee an unchanged attack envelope at every stretch ratio.

## Noise Resynthesis

During gameplay with Quality effects selected, noise morphing runs automatically whenever its eligibility conditions are met. It bypasses source-phase playback at 100%, pitch and formant mapping, transient protection, incomplete classification history, and unsupported channel or processing configurations.

The noise path runs for mono or stereo when window overlap is sufficient and split computation is disabled. The native adapter uses this configuration. More than two channels use the main vocoder path without noise resynthesis or envelope EQ.

After 17 analysis-history entries, temporal and spectral medians estimate tonal and transient content. The remainder is treated as texture. This classification is heuristic; distorted guitar and overlapping sources can be misclassified.

Replacement strength is `clamp(2 * (stretchFactor - 1) / stretchFactor, 0, 1)`, using the smoothed stretch factor. It reaches one at 50% speed and below, but replacement is capped at 25% of each bin's power. A frequency weight begins at 4500 Hz and reaches one at 9000 Hz for the native 1500 Hz transient cutoff. The classification and frequency weights determine the actual per-bin replacement target. At least 75% of each bin's power remains on the vocoder path.

The earlier broadband extension into the midrange is disabled to protect vocals and guitars. High-frequency replacement requires a nine-bin spectral power flatness measure, rising from zero at 0.50 flatness to one at 0.75. This prevents narrow tonal peaks with changing frequency from being randomized when the median classifier mistakes them for noise. Tonal and transient classification still limits replacement; this does not identify individual vocals or instruments in a mix. The reduced replacement strength and frequency range still require listening verification.

The mask increases by an amount corresponding to 30 ms for a full-scale change and decreases over approximately 15 ms, quantized to output hops. Pitch mapping, formant processing, and transient protection request zero replacement immediately. While the analysis history is incomplete, replacement is also zero.

`TextureGrains` reconstructs each block using source magnitudes and a shared random rotation per bin. Each channel retains its own source phase, preserving the inter-channel relationship. It uses an inverse FFT and an equal-power overlap between successive waveform blocks. An all-zero mask skips the inverse FFT and fades the previous tail.

The replacement spectrum uses a triangular source-power average, weighted by the replacement masks, to soften narrow spectral fluctuations. It uses five bins at 50% playback speed and above, widening gradually to nine bins at 35% speed and below using the smoothed stretch factor. Fractional widths interpolate the triangular weights without an abrupt change at integer radii. Dividing by the combined mask weights keeps the source envelope bounded as individual masks change. Smoothing stays within bins selected for replacement and preserves their combined spectral power. Both channels receive the same gain and rotation per bin, preserving their relative levels and phase. Wider averaging is a tuning experiment for residual metallic texture; its effect on growling vocals in dense mixes still requires listening verification.

Below 50% playback speed, the replacement envelope also receives temporal smoothing in output time. Its time constant rises from zero at 50% speed to 40 ms at 25% speed and below, approximately 17 ms at 35%. The current replacement mask and total spectral power still determine the rendered energy. Envelope history clears on zero replacement, including attack protection, and on resets or when a bin has no replacement power.

There is no waveform alignment search, correlation-based shift, source-coherence estimator, or speed-dependent decorrelation controller in this implementation. Random phase excitation is shared across channels and restarted on reset. Vocoder attenuation uses `sqrt(1 - mask)`; resynthesis uses the complementary masked source power.

## Broad-Band Volume Matching

The mono/stereo noise path includes an envelope EQ. It compares broad source-band power with reconstructed audio before applying its correction. The nominal crossovers are 2000 and 6000 Hz, scaled down at low sample rates. Source references are aligned to the synthesis delay.

Power estimates use a 250 ms time constant and gain changes use 150 ms. Gains are limited to approximately -2 dB through +1 dB. Adaptation requires an eligible slowdown above a 1.01 stretch factor, no pitch or formant mapping, and a warm-up interval. Other modes relax gains toward unity. Seeks, resets, and sustained silence clear the state.

This adjusts broad spectral balance; it does not undo phase-vocoder errors or guarantee unchanged perceptual loudness. Dense mixtures, very slow playback, finite analysis windows, and noise misclassification can still produce metallic texture, pitch wobble, transient spreading, or reduced attack definition.

## Source and Licensing

- [Native adapter](../Native/YargAudio/src/stretch/StretchTempoStream.cpp)
- [Phase and transient processing](../Native/YargAudio/src/stretch/YargStretch.h)
- [Noise classification](../Native/YargAudio/src/stretch/NoiseMorph.h)
- [Noise waveform synthesis](../Native/YargAudio/src/stretch/TextureGrains.h)
- [Envelope EQ](../Native/YargAudio/src/stretch/EnvelopeEq.h)
- [Signalsmith Stretch licence](../Native/YargAudio/third_party/signalsmith-stretch-LICENSE.txt)
- [Signalsmith Linear licence](../Native/YargAudio/third_party/signalsmith-linear/LICENSE.txt)

The distribution includes these notices in [YargAudio-Licenses.txt](../Assets/StreamingAssets/YargAudio-Licenses.txt).
