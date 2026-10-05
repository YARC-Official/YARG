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

PVDR uses real-time phase-gradient heap integration and estimates phase derivatives with centered finite differences. YARG uses derivative-window and time-weighted-window estimates. Mathematical relationships derived for Gaussian analysis windows do not automatically apply unchanged to other window shapes.

</details>

---

## 5. How YARG Improves the Sound

The engine is based on Signalsmith Stretch 1.3.2 by Geraint Luff ([Signalsmith Audio](https://github.com/Signalsmith-Audio/signalsmith-stretch)), with YARG's PVDR-inspired reconstruction and enhancements described below. It stretches the mixed output rather than each stem separately.

The native stream keeps track of which source position each 256-frame output block represents and accounts for processing delay when reporting playback position. Attack protection and grain alignment preserve this timing, so they do not insert local speed changes or change the number of output samples.

Some processing paths differ when applying a separate pitch shift (**pitch mapping**) or changing the resonances that give voices and instruments their character (**formant processing**). The technical sections identify those exceptions. A **spectrum** is the set of frequency-bin measurements for one window; **power** measures their energy and is proportional to magnitude squared.

### Keeping Pitch and Phase Stable

#### A. Stable Phase References

The engine uses strong, clear parts of the sound as phase references: starting points that guide nearby frequencies. It favors references that stay strong from one window to the next, helping avoid unnecessary changes in which bin leads the reconstruction.

<details>
<summary>Technical details</summary>

The engine processes significant spectral regions through an energy-prioritized heap. Strong regions provide phase references for neighboring bins, reducing the influence of weak phase estimates. Additional peak protection guides the high-frequency region. Transient frames use the same traversal so each dependent bin follows its reference in processing order.

The lower-frequency traversal favors established tonal references. A bin qualifies when it was a previous-frame temporal root and local energy peak, remains prominent above the smoothed spectral background, and retains at least half both its previous power and the strongest adjacent-bin power. Its current power must exceed 0.1% of the strongest current or previous bin power. Qualified references receive a priority multiplier of 1.5 for both their temporal and propagation entries. Other references can still outrank them, and parents are rebuilt every frame.

This preference uses shared decisions across channels and applies to new input analyses during slowdown without pitch mapping or formant processing. It bypasses active transient protection and pending transient collection. Seeks, resets, sustained silence, and draining invalidate its history. It reuses previous parent assignments rather than tracking complete spectral lobes, so it cannot distinguish unresolved sources sharing a spectral region.

</details>

#### B. Pitch and Attack Analysis

Extra analysis helps estimate both pitch and attack timing. For stable notes below 2500 Hz, the engine cross-checks its pitch estimate against how the source phase actually changed. Nearby bins help judge whether that estimate is trustworthy. Corrections are limited so that uncertain measurements do not cause large pitch changes.

<details>
<summary>Technical details</summary>

Each slice is analyzed with the regular window, a derivative window, and a time-weighted window. These estimate instantaneous frequency and within-window timing for phase advancement over time and propagation across frequency. Accuracy depends on the window, signal energy, and overlapping components.

Below 2500 Hz, qualified temporal roots also compare their derivative-window phase advance with measured progression between source spectra. The comparison uses the actual separation of those spectra: one output hop when the earlier spectrum is reanalysed, or the actual source-hop length when the previous spectrum is retained. It subtracts the bin-center advance, unwraps the residual onto the branch nearest the derivative estimate, and converts it to an output-hop phase advance.

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

Noise replacement pauses during initial transient protection and around the delayed reset, rather than throughout collection. After the synchronized reset, selected attack bins remain marked until the source-timed event expires. During event cooldown they are exempt from high-frequency decay damping and post-transient midrange damping, including on held spectra. All channels use the same selection.

These resets preserve the requested time mapping rather than playing attacks at a different local speed. They do not guarantee unstretched attack envelopes. Multiple attacks within a window and overlapping sources within selected bins remain limitations.

</details>

#### F. Attack Reinforcement

Frequencies above 250 Hz receive a limited volume boost when their energy rises sharply. Every channel gets the same boost. This stage limits the amplitude multiplier to 1.25, though later processing can change the result. A separate option for reinforcing weaker attacks is disabled in the native configuration.

#### G. Stereo Preservation

The stronger channel at each frequency provides the main phase reference. The other channels follow it while keeping their original phase differences. This helps preserve where sounds appear between the left and right speakers. During an attack reset, each channel uses its own source phase.

Between approximately 800 and 5000 Hz, an extra correction helps the quieter channel retain its original phase relationship to the louder one without changing the quieter channel's magnitude. The correction gradually decreases from 3500 to 5000 Hz.

Below 100 Hz, bass correction reduces changes to the original phase difference between channels. It weakens when their levels are very unequal and leaves effectively silent channels alone.

#### H. Aligning the Bins Around a Note

A **spectral peak** is a strong frequency region that may represent a note or part of one. **Phase locking** helps nearby bins keep their source relationship to that peak. One stage adjusts the immediate neighbors with the same rotation in every channel. Another adjusts a wider region around the peak, called its **main lobe**, separately for each channel. Both work below approximately 5000 Hz and gradually weaken between 3500 and 5000 Hz. These stages pause during pitch mapping and formant processing.

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

#### K. Controlling Unwanted Sound

The engine also adjusts the strength of individual frequency regions to control harshness, ringing, and unwanted volume changes. The limits below apply to each stage, not to the final output. **Gain** means a volume multiplier: 1 leaves amplitude unchanged, 0.8 reduces it to 80%, and 1.2 raises it to 120%. “Shared” means every channel gets the same multiplier; “Separate” means each channel is calculated individually.

| Enhancement | Current behavior | Channel gains |
| :--- | :--- | :--- |
| Spectral contrast and anti-ringing | Turns down bins weaker than the local average across channels, keeping at least 88% of their amplitude. | Shared |
| Causal pre-echo suppression | Reduces sound spreading ahead of an attack, keeping at least 70% of amplitude. | Shared |
| Peak sharpening | Turns down the edges around strong peaks to at least 80% of amplitude and boosts their centers by at most 2%. | Shared |
| Bark-band valley suppression | Turns down weak regions near stronger sounds between approximately 350 and 16000 Hz. Strong peaks are protected; at least 82% of amplitude is retained. | Shared |
| De-essing | Softens strong regions between 5000 and 12000 Hz, retaining at least 85% of amplitude. It responds to frequencies rather than detecting a voice. | Shared |
| Extreme-slowdown attenuation | Below approximately 45% speed, increasingly turns down bins weaker than the local average across channels. | Shared |
| Dynamic modulation restoration | Adjusts bins according to their strength relative to the local average, with amplitude multipliers from 0.86 to 1.16. | Shared |
| High-frequency decay damping | Softens fading sound more strongly at higher frequencies. When an analysis is reused, gentler softening applies above 4000 Hz. | Separate |
| Post-transient midrange damping | After an attack, softens fading sound between approximately 260 and 1350 Hz, retaining at least 78% of amplitude. Selected attack bins are protected. | Shared |
| Spectral boost limiting | Gradually limits excessive energy above 1.25 times the reconstruction estimate, approaching a ceiling of twice that estimate. | Shared |
| Spectral gain diffusion | Shares energy between neighboring bins to bring their balance closer to the reconstruction estimate, keeping their combined energy unchanged. | Shared |
| Gain floor | Raises bins that have become too weak relative to the reconstruction estimate. At strong slowdowns, the floor reaches 25% of estimated power; silent bins stay silent. | Separate |

Peak sharpening fades above 3500 Hz and is inactive at 5000 Hz and above. The taper applies to both peak and neighboring-bin frequencies. The stage named dynamic modulation restoration measures spectral contrast rather than modulation over time.

Most strengths increase with the smoothed slowdown ratio. Bypass conditions vary: many stages pause during transient protection, some also pause during cooldown or pitch mapping, and some require a new input spectrum. Post-transient midrange damping runs during cooldown outside active protection, with the selected-attack exemptions described above.

Shared gains preserve the per-bin channel ratio at that stage; separate gains can change it. Later phase processing, overlap-add, and grain mixing affect the final sound and stereo image. Complementary vocoder/grain power weights do not guarantee constant waveform power when the paths are correlated.

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

---

## 6. References & Further Reading

- Průša, Z., & Holighaus, N. (2017). *Phase Vocoder Done Right*. 25th European Signal Processing Conference (EUSIPCO 2017). [arXiv:2202.07382](https://arxiv.org/abs/2202.07382).
- Röbel, A. (2003). *A New Approach to Transient Processing in the Phase Vocoder*. 6th International Conference on Digital Audio Effects (DAFx-03). [Paper](https://dafx.de/paper-archive/2003/pdfs/dafx32.pdf).
- Průša, Z., Balazs, P., & Søndergaard, P. L. (2017). *A Noniterative Method for Reconstruction of Phase from STFT Magnitude*. IEEE/ACM Transactions on Audio, Speech, and Language Processing. [Author research page and preprint](https://ltfat.org/notes/040/).
- Luff, G. *Signalsmith Stretch*. [Source and documentation](https://github.com/Signalsmith-Audio/signalsmith-stretch). YARG's engine is derived from version 1.3.2.
