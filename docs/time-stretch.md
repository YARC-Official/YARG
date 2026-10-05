# Time Stretching in YARG

Time stretching allows audio to play slower or faster without altering its pitch. In YARG, this powers Practice Mode (slowing songs down to 10% speed or speeding them up to 200%) as well as micro-adjustments for syncing audio and visuals.

Choose **Quality** in the **Effects** dropdown under **Experimental** settings to use the native YargStretch engine. **Performance** uses the existing BASS_FX engine. The setting also selects the corresponding reverb mode. Time stretching processes the mixed stem output, not each stem separately.

This document explains how speed changes work, why they can affect sound quality, and what YARG does to reduce those effects. The main explanations use plain language; expandable sections contain implementation details.

## 1. Breaking Audio into Frequencies

### Frequency Bins, Volume, and Phase

Music combines many overlapping vibrations. A **short-time Fourier transform (STFT)** examines a short section of audio and describes it as a set of frequency ranges called **bins**. Repeating this analysis produces a picture of how the sound changes over time.

Each bin records:

- **Frequency:** How quickly a vibration repeats, measured in Hertz (Hz). Faster vibrations generally sound higher. Each bin has a fixed center frequency, but the vibration detected there can fall between bin centers.
- **Magnitude:** How strong that part of the sound is.
- **Phase:** Where a vibration is in its repeating cycle: for example, at a peak, at a trough, or crossing between them. Phase helps determine how overlapping waves combine.

A single note can spread across several bins, and several instruments can contribute to the same bin. Bins do not separate the recording into individual instruments.

### Windows and Overlap

A **window** is a short section of audio used for analysis. Longer windows help distinguish close frequencies, while shorter windows make it easier to locate sharp attacks such as drum hits.

The engine fades the edges of each window to avoid abrupt cuts, then overlaps and adds the processed sections to rebuild the waveform. It compensates for these fades so that overlapping windows do not themselves cause regular volume changes. Modifying the sound inside those windows can still introduce artifacts.

<details>
<summary>Window sizes and overlap</summary>

The native stream targets 60 ms, rounds up to a multiple of 512 samples, and uses a minimum of 1024 samples. At 44.1 kHz, this produces a 3072-sample window (approximately 69.7 ms). The output advances by one eighth of the window: 384 samples, or approximately 8.7 ms. Successive windows therefore overlap by 87.5%.

The relationship between analysis and reconstruction windows is described in [Phase Vocoder Done Right](https://www.eurasip.org/Proceedings/Eusipco/Eusipco2017/papers/1570343436.pdf).

</details>

### How Step Sizes Control Speed

The distance between successive windows is called a **hop**. The output hop stays fixed. Playback speed changes by adjusting how far the engine advances through the source between output windows:

- **100% speed:** Input and output advance by the same amount.
- **50% speed:** Input advances half as far as output. The same source audio takes twice as long to play.
- **200% speed:** Input advances twice as far as output. The source audio takes half as long to play.

The input step is also called the **analysis hop**, and the output step the **synthesis hop**.

### Why Phase Needs Correction

Moving windows apart or closer together changes how their vibrations line up. If they no longer line up correctly, overlapping waves can partly cancel each other or create unwanted changes in volume and tone.

A **phase vocoder** estimates how quickly each vibration is progressing and adjusts its phase for the new window spacing. This helps preserve pitch while changing speed. Estimation errors and overlapping instruments can still affect the result.

## 2. Phase Vocoder vs. WSOLA

The existing BASS_FX engine uses a **WSOLA-style** approach: it moves short pieces of the waveform and searches for positions where they match well enough to overlap. This is called a **time-domain** method because it works directly on the waveform.

A phase vocoder analyzes frequency bins and adjusts them before rebuilding the waveform. This is called a **time-frequency** method because it considers both frequency and how the sound changes over time.

| Feature | WSOLA | Phase vocoder |
| :--- | :--- | :--- |
| **Approach** | Moves and overlaps matching waveform segments. | Adjusts frequency bins and their phase. |
| **Steady, repeating sounds** | Can preserve waveform shape when good matches exist. | Can preserve pitch when frequency and phase estimates are reliable. |
| **Several instruments at once** | One shift may line up one instrument but misalign another. | Can treat frequency regions differently, but instruments may still share bins. |
| **Possible artifacts** | Audible repetition, graininess, or rough joins. | Hollow or metallic sound, blurred attacks, or loss of definition. |

Both methods have limits. Neither can reliably separate every instrument from a mixed recording or guarantee unchanged sound at very slow speeds.

## 3. Keeping Waves Aligned Over Time and Across Frequencies

There are two kinds of phase relationship to preserve:

1. **Over time (horizontal coherence):** Each vibration should continue smoothly from one window to the next.
2. **Across frequencies (vertical coherence):** Bins contributing to the same sound should retain the relationships that give it its shape. They do not need identical phases.

Correcting each bin independently can preserve the first relationship while damaging the second:

- **Notes can sound hollow:** One tone spreads across neighboring bins. If those bins stop working together, the reconstructed tone can change character.
- **Drum hits can blur:** A sharp attack combines many frequencies arriving together. Changing their phase relationships can spread the attack over time.
- **Vocals can sound metallic or robotic:** A voice combines a fundamental pitch, higher overtones, and noise. Errors in pitch, phase, or volume balance can change that texture.

The goal is to preserve meaningful relationships without forcing unrelated sounds to follow each other.

## 4. Phase Vocoder Done Right (PVDR)

[*Phase Vocoder Done Right*](https://www.eurasip.org/Proceedings/Eusipco/Eusipco2017/papers/1570343436.pdf), by Zdeněk Průša and Nicki Holighaus (EUSIPCO 2017), uses both kinds of relationship. It estimates how phase changes over time and between neighboring frequencies, then builds the output phase through the strongest parts of the sound first. This lets clearer, stronger regions guide weaker ones.

The technical name for these estimates is **phase gradients**. Changes over time describe frequency; changes across frequency describe where sound falls within a window. Combining them helps address the problems of correcting every bin independently.

YARG follows this approach with its own analysis windows, peak handling, attack protection, stereo corrections, and noise processing. It is an adaptation rather than an exact reproduction of the paper.

<details>
<summary>How the estimates differ from the paper</summary>

PVDR uses real-time phase-gradient heap integration and estimates phase derivatives with centered finite differences. YARG estimates frequency gradients from Gaussian-window log magnitudes and retains time-weighted-window estimates for attack timing. The finite Gaussian window and finite differences approximate the continuous phase-magnitude relationship.

</details>

---

## 5. How YARG Improves the Sound

The engine is based on Signalsmith Stretch 1.3.2 by Geraint Luff ([Signalsmith Audio](https://github.com/Signalsmith-Audio/signalsmith-stretch)), with YARG's PVDR-inspired reconstruction and adaptations described below. It stretches the mixed output rather than each stem separately.

The native stream keeps track of which source position each 256-frame output block represents and accounts for processing delay when reporting playback position. Attack protection and grain alignment preserve this timing, so they do not insert local speed changes or change the number of output samples.

Some processing paths differ when applying a separate pitch shift (**pitch mapping**) or changing the resonances that give voices and instruments their character (**formant processing**). The technical sections identify those exceptions. A **spectrum** is the set of frequency-bin measurements for one window; **power** measures their energy and is proportional to magnitude squared.

### Keeping Pitch and Phase Stable

#### A. Stable Phase References

The engine uses strong, clear parts of the sound as phase references: starting points that guide nearby frequencies. It favors references that stay strong from one window to the next, helping avoid unnecessary changes in which bin leads the reconstruction.

<details>
<summary>Technical details</summary>

The engine now integrates phase through a confidence-weighted complex tridiagonal system instead of a heap. Each bin has a positive temporal anchor from the existing phase-vocoder prediction. Adjacent significant bins contribute frequency constraints when their source-relative phases agree across input frames. Their target relationship combines the current source phase difference with the existing time-weighted gradient's stretch correction. The solver estimates complex spectral coefficients, using predicted magnitudes in its temporal targets and adjacent-bin ratios; reconstruction restores the predicted magnitude afterward. Frequency equations are scaled to unit coefficient energy, avoiding division by weak-bin magnitudes. Their confidence weight is multiplied by 16 to preserve coherence within spectral lobes while positive temporal anchors retain their phase reference. This adapts the numerical second stage of Fernandez et al. without its neural gradient predictor.

Established low-frequency peaks and sheltered high-frequency peaks receive stronger temporal anchors. The low-frequency preference requires a previous local peak, sufficient current energy relative to its previous frame and the global maximum, and prominence above the smoothed background. Tonal references retain the bounded source-phase cross-check below 2500 Hz. Reference history is invalidated by seeks, resets, sustained silence, and draining.

Transient bins selected for a phase reset are isolated from frequency coupling and anchored to source phase. Weak bins retain random phase and cannot connect separate significant regions. Pitch mapping and formant processing use temporal anchors without neighboring-bin coupling. Every bin uses the strongest channel as its phase reference; the existing relative-phase locking reconstructs other channels afterward. Noise resynthesis and envelope EQ remain downstream.

A forward elimination and backward substitution solve the Hermitian system in linear time. Positive temporal anchor weights make the system positive definite even when frequency edges are disconnected. Double-precision solver buffers are allocated during configuration; no extra FFT, lookahead, or processing delay is introduced. The magnitude-aware implementation builds and passes the native harmonic, vibrato, pitch, stereo, cancellation-power, split-computation, silence, grain, and reset checks. At 100% speed, the stationary harmonic consistency residual fell from 0.0794 for the first phase-only solver to 0.00030 after correction; the heap baseline was 0.000092. Some synthetic diagnostics still favor the heap. Perceptual quality on mixed recordings and CPU benefit remain unverified.

</details>

#### B. Pitch and Attack Analysis

Extra analysis helps estimate both pitch and attack timing. For stable notes below 2500 Hz, the engine cross-checks its pitch estimate against how the source phase actually changed. Nearby bins help judge whether that estimate is trustworthy. Corrections are limited so that uncertain measurements do not cause large pitch changes.

<details>
<summary>Technical details</summary>

Each slice is analyzed with a Gaussian window and a time-weighted window. Centered frequency differences of log power estimate instantaneous frequency through the Gaussian phase-magnitude relationship, replacing the derivative-window FFT. When a neighboring bin falls more than 20 dB below the current bin in power, the frequency estimate blends toward measured phase progression between input frames. The blend depends on the neighboring-bin deficit and the energy agreement between current and previous frames; weak previous frames retain the magnitude estimate. The temporal residual removes the bin-center phase advance and wraps to the principal interval before division by the actual analysis interval. This local reliability heuristic uses existing spectra without another FFT or added latency. It is not an implementation of the zeros paper's reconstruction method, and improvement in mixed-recording quality has not been established. The Gaussian standard deviation is one eighth of the window length. The retained time-weighted analysis estimates within-window timing for transient protection and propagation across frequency. Overlap-add uses the actual analysis/synthesis window products for normalization. Accuracy depends on finite-window truncation, finite differences, signal energy, and overlapping components. This experimental implementation builds and passes the native synthetic quality tests. CPU performance and quality on mixed recordings have not been measured.

Below 2500 Hz, qualified temporal roots also compare their magnitude-derived phase advance with measured progression between source spectra. The comparison uses the actual separation of those spectra: one output hop when the earlier spectrum is reanalysed, or the actual source-hop length when the previous spectrum is retained. It subtracts the bin-center advance, unwraps the residual onto the branch nearest the magnitude-derived estimate, and converts it to an output-hop phase advance.

Disagreement exceeding 0.025 radians per output hop gradually enables a blend over a further 0.25 radians. Similar endpoint powers increase confidence. The measured progression receives at most half the weight, and the added root rotation is bounded to 0.12 radians per hop. Only the dominant channel's selected root receives the correction before inter-channel phase transfer and dependent-bin reconstruction.

Confidence also considers up to two adjacent bins on each side. The scan follows descending portions of the candidate peak in both source spectra and stops when either endpoint falls below 10% of the reference's corresponding power. Neighboring phase advances are compared with a 0.1-radian-per-hop tolerance and weighted by their endpoint powers. This multiplies confidence by a factor between 0.25 and 1; no usable neighbors leave confidence unchanged.

These checks use the tonal-reference eligibility and reset rules above. They do not quantize pitch or apply long-term frequency smoothing. Genuine bends, vibrato, beating partials, and unresolved sources can make the estimates disagree; the bounded correction cannot separate those sources.

</details>

#### C. Keeping Related Frequencies Together

A single tone spreads across neighboring bins. The engine gives these bins phase references to follow, helping them rebuild the tone together. A reference bin is called a **parent**, and bins following it are its **children**. Strong high-frequency regions also protect nearby bins from arbitrary phase changes.

When no separate pitch shift is applied, bins below about 4850 Hz at a 44.1 kHz sample rate use the estimated phase changes between frequencies up to a sixfold stretch (about 16.7% playback speed). At greater slowdowns, they keep their source phase relationship to their parent instead. The parent is chosen by reconstruction priority and is not necessarily the center of a note.

#### D. Handling Very Quiet Frequencies

Very quiet bins have unreliable phase estimates, so the engine assigns them random phase. Louder noise textures use the separate grain processing described below.

### Preserving Attacks and Stereo Sound

#### E. Attack Timing and Protection

A **transient** is a sharp attack, such as the start of a drum hit. The engine detects sudden increases in energy, identifies the frequencies involved, and waits until the attack reaches the appropriate point in the analysis window. It then resets their phase together, using one timing decision for both stereo channels.

Those frequencies are also protected from later processing that would soften their decay. This helps retain attack definition while keeping the requested playback speed. It cannot guarantee perfectly unchanged attacks, especially when several hits or instruments overlap.

<details>
<summary>Technical details</summary>

The onset detector measures regional energy increases. It compares each bin with the strongest nearby bin in the previous analysis to reduce false triggers from pitch movement. A separate low-frequency detector considers bass attacks.

During slowdown without pitch mapping or formant processing, an onset starts collecting rising spectral lobes separated at amplitude minima. Their energy positions are estimated from the time-weighted analysis across all channels. The timing threshold is calibrated from the actual analysis window using the energy center of a windowed linear ramp.

The collected regions reset to their input phase together when at least half their current energy belongs to lobes whose estimated position has passed the threshold. Channels share the event and reset hop while retaining their own input phase. Collection and repeat suppression are bounded by one analysis-window length of source audio, including at 10% speed. Seeks, resets, sustained silence, draining, and leaving the eligible path clear the event.

This uses the center-timed reset and synchronized transient-set ideas in [Röbel's transient-processing paper](https://dafx.de/paper-archive/2003/pdfs/dafx32.pdf). Pitch mapping and formant processing use an immediate reset: bins above 1500 Hz snap, along with lower bins exhibiting a strong individual energy rise.

Noise replacement pauses during initial transient protection and around the delayed reset, rather than throughout collection. After the synchronized reset, selected attack bins remain marked until the source-timed event expires. All channels use the same selection.

These resets preserve the requested time mapping rather than playing attacks at a different local speed. They do not guarantee unstretched attack envelopes. Multiple attacks within a window and overlapping sources within selected bins remain limitations.

</details>

#### G. Stereo Preservation

The stronger channel at each frequency provides the main phase reference. The other channels follow it while keeping their original phase differences. This helps preserve where sounds appear between the left and right speakers. During an attack reset, each channel uses its own source phase.

### Preserving Noise Textures

#### I. Noise Detection and Grain Blending

The engine compares how sound varies over time and across nearby frequencies to distinguish notes, attacks, and noise textures. Notes and attacks receive protection. Suitable noisy regions, such as cymbal texture, are rebuilt from short waveform pieces called **grains**.

Each grain is compared with the previous grain so their overlap lines up as well as possible. Both stereo channels use the same shift. A **crossfade** gradually blends one grain into the next, with volume compensation based on how similar they are. Changes between the grain and vocoder paths are also gradual to avoid abrupt texture changes.

<details>
<summary>Technical details</summary>

Temporal and spectral medians classify sustained tones, attacks, and texture after 17 analysis-history entries. Strong or tonal peaks protect neighboring bins along descending spectral slopes, stopping at valleys or plateaus. Each bin also retains its own tonal confidence.

For mono or stereo with sufficient window overlap and without split computation, eligible texture is reconstructed from short waveform grains. Each grain spans two output hops with one hop of overlap. At 44.1 kHz in the native configuration, this is approximately 17.4 ms per grain with 8.7 ms of overlap. Alignment searches up to one quarter-hop in either direction.

Zero offset is evaluated first. Candidate scores subtract a shift penalty that reaches 0.05 at the search boundary; ties favor smaller shifts. Nonzero shifts require normalized correlation of at least 0.35 over the whole overlap and 0.20 in each temporal half, measured across the combined channels. Only candidates that could beat the current score receive the half-overlap checks. These checks are heuristic and cannot prove that a match represents genuine continuity.

Both channels share the selected offset. Equal-power crossfades are normalized using the selected correlation, clamped to zero through one, rather than the penalized alignment score. Startup with an empty tail retains zero offset. An all-zero replacement mask skips inverse FFTs and alignment searches while fading and clearing the previous tail.

Maximum replacement reaches 100% at 50% playback speed and below; the actual amount depends on classification, peak protection, and frequency weighting. Replacement begins above 2250 Hz and reaches full frequency weight at 4500 Hz. Lower-frequency texture stays on the vocoder path.

A full-scale replacement increase takes approximately 30 ms of output time; ordinary decreases take approximately 15 ms, rounded to output hops. Smaller changes settle sooner. Transient protection, pitch mapping, and formant processing clear the mask immediately. Vocoder attenuation and grain synthesis use the same mask with complementary power weights. Seeks and resets clear history and grain state; bypassed grains fade their previous tail.

</details>

#### J. Reducing Repetition in Noise

Repeated noise grains can develop an unwanted repeating, buzzy character. **Phase decorrelation** introduces controlled random phase changes to reduce that repetition. It becomes stronger at slower speeds and in confidently identified treble noise. Both stereo channels receive the same changes.

The engine also measures **source coherence**: how predictably phase progresses in the original audio. Where progression is predictable, it uses less randomization to retain more of the source character. Volume compensation accounts for how randomization changes the grain waveform. These are estimates; dense distorted guitar and other complex sounds can be misclassified.

<details>
<summary>Technical details</summary>

Phase decorrelation increases with the smoothed stretch ratio from zero at 50% speed to full strength at 25%. At 35%, the base amount is approximately 43% before per-bin weighting. Each bin's current replacement target scales the amount, so fully tone-protected bins receive no new randomization while an older mask releases. Rotations are shared across stereo channels and preserve each bin's magnitude and input inter-channel phase relationship.

Confident treble texture receives additional decorrelation. Texture confidence rises from 0.85 to 1, frequency weight rises from approximately 4500 to 6750 Hz, and speed weight reaches full at approximately 35.7% playback. Ambiguous regions retain the base amount.

A source-coherence estimate reduces randomization where source phase progression is predictable. It uses unmodified spectra separated by at least one complete source-analysis window, excludes held spectra, and demodulates progression by the bin-center advance for the actual interval. Averaged complex cross-products and endpoint powers estimate coherence per channel; powers weight the shared decision without cancelling opposite stereo phases.

After eight measurement pairs, an approximate finite-history noise bias is subtracted. Corrected coherence above 0.5 progressively reduces randomization, by at most half. The phase multiplier changes with a full-scale limit of 50 ms in output time. This adjustment changes phase randomization rather than classification or replacement targets. It observes raw input while replacement is bypassed; seeks, resets, sustained silence, and draining invalidate its history.

Grain-window normalization blends original window compensation with compensation based on mean analysis-window power, using shared power-weighted phase coherence. This avoids applying the full inverse-window edge boost to decorrelated noise. Randomization uses a reproducible sequence restarted on seeks, resets, and silence clearing; explicit seeded resets provide its seed.

Classification and coherence are heuristic. Dense guitar harmonics can resemble noise, and colored noise, frequency motion, and beating partials can affect coherence. Normalization assumes noise-like content and does not guarantee constant output power or correct treatment of misclassified harmonics.

</details>

### Preserving Tone and Volume

The F8/F9-controlled spectral shaping chain has been removed. Solver reconstruction, transient phase resets, noise resynthesis, and the envelope EQ remain active.

#### L. Matching the Original Tonal Balance

Gentle **EQ** adjustments help the stretched audio retain the source balance of bass, mids, and highs. The engine compares the source with the rebuilt waveform and slowly adjusts three broad bands, using the same settings for both stereo channels. Each band can be reduced by up to about 2 dB or boosted by up to about 1 dB. This changes tonal balance; it does not repair pitch errors or phase cancellation.

<details>
<summary>Technical details</summary>

Gentle EQ compares broad source-band power with reconstructed audio after overlap-add and grain mixing, before applying its own correction. The source reference reuses the analysis spectrum and accounts for the same two low-pass filters used to split and reconstruct three waveform bands. Nominal crossovers are 2000 and 6000 Hz, scaled down at low sample rates. Spectral power is normalized by transform length and analysis-window power.

Each reference is queued for its synthesis-center position and held for one output hop. Alignment follows the source-frame mapping. The windowed source estimate and reconstructed audio have different temporal support during stretching, so matching is approximate, especially near attacks and speed changes.

Source and pre-correction output powers are averaged over approximately 250 ms of output time. Matching starts after 250 ms of continuous eligibility. Gain targets update once per output hop using regularized square-root power ratios, bounded to approximately -2 dB through +1 dB per band. Gains approach their targets over approximately 150 ms. Channels share gains and filter responses. Unity gains reconstruct the waveform through a complementary split; other gains introduce the EQ's frequency and phase response. Per-band bounds are not bounds on the combined response.

Matching operates on the configured mono/stereo grain path during slowdown without pitch mapping or formant processing. Other speeds and modes relax gains toward unity after queued references arrive. Seeks, resets, and sustained silence clear filters, gains, power estimates, and references. Final draining applies current gains after tail mixing and cancellation, freezes adaptation, and clears history. EQ changes broad spectral balance rather than frequencies or phase cancellation.

</details>

### Processing Cost & Limitations

Grain alignment reuses previous-tail power across offsets and stops half-overlap checks after the first failure. All-zero masks skip grain inverse FFTs and alignment searches. Source-coherence measurements reuse existing spectra without another FFT or buffering delay. EQ adds two low-pass updates per channel per output sample, power averaging, and source-band accumulation per hop. Their arrays are allocated during configuration; CPU savings and added costs have not been benchmarked.

Practice playback extends down to 10% speed. Quality depends on the recording and stretch ratio. Unresolved overlapping sources, heuristic noise classification, and finite analysis windows can still produce pitch wobble, metallic texture, graininess, transient spreading, or reduced attack brightness.

### Offline Quality Measurements

The native quality tests report spectral convergence, normalized KL divergence, and mean Itakura–Saito divergence at 10%, 25%, 50%, 75%, 100%, and 200% speed. They compare time-averaged power spectra of a steady harmonic mixture with a quieter 6000 Hz tone against the known stationary source spectrum. Independent Kaiser analysis windows of 1024 and 4096 samples measure the second output second, excluding startup. The reference retains the same pitches and levels at every speed because the fixture is stationary.

Spectral convergence is the relative Euclidean error between RMS magnitudes. KL uses reference-to-output power divergence, normalized by total reference power. Itakura–Saito uses the reference/output power ratio and averages across bins. Both divergences add a power regularizer equal to 1e-8 times the strongest reference-bin power. Lower scores indicate closer spectra. Tests check identical spectra, known gain changes, scale invariance, and a deliberately attenuated quiet component. The reported playback scores are diagnostic; acceptance thresholds have not been calibrated.

STFT consistency is measured on the generated channel-zero complex spectra, captured after each complete hop. The harness reconstructs those spectra separately, then reanalyzes with the exact production Gaussian windows, offsets, modified FFT convention, and output hop. The reported error is the complex Euclidean residual divided by the original complex-spectrum norm. Boundary windows are excluded. A known noise spectrogram must reconstruct below 1e-6 relative error, and deliberately scrambled phases must produce substantial error. The measurement covers the STFT component; separately added grains and output EQ are excluded. It does not measure perceptual quality or synchronization.

Onset measurements use three decaying 4000 Hz bursts over a quiet sustained tone at each tested speed, spaced by half a second in scheduled output time. The harness locates the strongest 44-sample energy window within 3072 samples of each expected attack and reports signed timing error and early-to-post-attack energy ratio. Expected positions are input onset divided by playback speed plus output latency: this harness primes the input window with `seek()`, placing the initial analysis center at source time zero. Early energy excludes the last 44 samples before the expected attack. These are synthetic-fixture diagnostics, not calibrated acceptance thresholds or a measurement of full game A/V synchronization.

A close-tone fixture places opposing sinusoids at neighboring modified-FFT bin centers, producing recurring spectral cancellation. It reports retained tone energy, output power, and STFT consistency across all tested speeds. Finite stereo output and bounded power are required, with additional mapped-pitch, split-computation, and silence checks. Retained-tone and consistency scores remain diagnostic because this unresolved mixture already reconstructs poorly in the baseline algorithm.

These measurements run only in tests. The averaged spectral scores discard temporal detail and do not establish transient quality, phase coherence, or perceptual quality on mixed recordings. The existing harmonic, vibrato, pitch, stereo, grain, and reset checks remain separate.

---

## 6. References & Further Reading

- Fernandez, A., Azcarreta, J., Bilen, Ç., and Monge Alvarez, J. *Efficient Neural and Numerical Methods for High-Quality Online Speech Spectrogram Inversion via Gradient Theorem*. [arXiv:2505.24498](https://arxiv.org/abs/2505.24498).
- Vial, P.-H., Magron, P., Oberlin, T., and Févotte, C. *Phase retrieval with Bregman divergences and application to audio signal recovery*. [arXiv:2010.00392v2](https://arxiv.org/abs/2010.00392v2).
- Průša, Z., and Holighaus, N. (2017). *Non-iterative Filter Bank Phase (Re)Construction*. EUSIPCO 2017. [arXiv:2202.07498](https://arxiv.org/abs/2202.07498).
- Průša, Z., & Holighaus, N. (2017). *Phase Vocoder Done Right*. 25th European Signal Processing Conference (EUSIPCO 2017). [arXiv:2202.07382](https://arxiv.org/abs/2202.07382).
- Röbel, A. (2003). *A New Approach to Transient Processing in the Phase Vocoder*. 6th International Conference on Digital Audio Effects (DAFx-03). [Paper](https://dafx.de/paper-archive/2003/pdfs/dafx32.pdf).
- Průša, Z., Balazs, P., & Søndergaard, P. L. (2017). *A Noniterative Method for Reconstruction of Phase from STFT Magnitude*. IEEE/ACM Transactions on Audio, Speech, and Language Processing. [Author research page and preprint](https://ltfat.org/notes/040/).
- Luff, G. *Signalsmith Stretch*. [Source and documentation](https://github.com/Signalsmith-Audio/signalsmith-stretch). YARG's engine is derived from version 1.3.2.
