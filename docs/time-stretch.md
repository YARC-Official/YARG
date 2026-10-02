# Time Stretching in YARG

Time stretching allows audio to play slower or faster without altering its pitch. In YARG, this powers Practice Mode (slowing songs down to 10% speed or speeding them up to 200%) as well as micro-adjustments for syncing audio and visuals.

This document explains:

1. The foundations: Fourier transforms, STFT configuration (window & hop sizes), and how hop ratios dictate speed.
2. The tradeoffs between time-domain splicing (WSOLA) and phase vocoders on mixed music.
3. The two axes of alignment: horizontal (over time) vs. vertical (across frequency bins).
4. The theoretical foundation: Phase Vocoder Done Right (PVDR) and 2D phase gradients.
5. YARG's native engine: PVDR implementation pipeline and custom enhancements.

---

## 1. Bins, Phase, and Step Sizes

### Frequency Bins: Decomposing the Mix into Sinusoidal Components

The Short-Time Fourier Transform (STFT) decomposes complex audio into hundreds of separate frequency channels called **bins**.

Each bin is a complex coefficient on a fixed frequency grid. A source component can contribute to several bins, and several sources can contribute to the same bin. Each coefficient has:

- **Frequency:** The bin has a fixed center frequency in Hertz. The local frequency estimated from phase progression can differ from that center.
- **Magnitude:** The amplitude of that component.
- **Phase ($\phi$):** The angular position within the sinusoidal cycle (from 0 to $2\pi$ radians, or 0° to 360°), determining whether the wave starts at a peak, trough, or zero-crossing.

In YARG's engine configuration:

- **Window Duration:** The native stream targets 60 milliseconds and rounds the sample count up to a processing-block boundary. This trades frequency resolution against temporal resolution; overlapping sources are not guaranteed to be separable.
- **Overlap-Add:** The output hop is one eighth of the window length, giving 87.5% overlap. Window normalization supports continuous reconstruction, but spectral modifications can still introduce artifacts.

---

### Windowing and Overlap-Add Reconstruction

Abruptly splicing waveform segments can introduce discontinuities and broadband energy. Windowing and overlap-add reduce boundary effects:

1. **Windowing:** Each block is multiplied by a smooth window that reduces the contribution of its boundaries.
2. **Overlap-Add:** Successive windowed blocks are staggered in time and summed into the output buffer.
3. **Window Normalization:** Analysis and synthesis windows are chosen or normalized so their overlapping products provide the required reconstruction gain. For unchanged compatible spectra, this avoids gain variation from window overlap. It does not guarantee flat amplitude or artifact-free output after time stretching. The synthesis-window relationship is described in [Phase Vocoder Done Right](https://www.eurasip.org/Proceedings/Eusipco/Eusipco2017/papers/1570343436.pdf).

---

### How Step Sizes Control Speed: Input Hop vs. Output Hop

Playback speed is determined by the spacing between successive STFT blocks:

- **Synthesis Hop ($H_s$):** The fixed time step between consecutive output blocks written to the audio buffer. This output rate remains constant.
- **Analysis Hop ($H_a$):** The time step between consecutive input blocks read from the source audio file.

```
Output Timeline:   [--- Block 1 ---]
                          [--- Block 2 ---]
                                 [--- Block 3 ---]  (Fixed synthesis hop)

Slowing Down:      Read 1 -> Read 2 -> Read 3       (Smaller analysis hop)
Speeding Up:       Read 1 ------------> Read 2      (Larger analysis hop)
```

- **Normal Speed (100%):** The analysis hop equals the synthesis hop ($H_a = H_s$). Audio plays at original tempo.
- **Slowing Down (e.g., 50%):** The analysis hop is shorter than the synthesis hop ($H_a < H_s$). The engine advances through the source audio by fewer samples per block than it writes to the output buffer, taking longer to traverse the input.
- **Speeding Up (e.g., 200%):** The analysis hop is larger than the synthesis hop ($H_a > H_s$). The engine advances through the source file by more samples per block than it writes to the output buffer, traversing the input in less time.

---

### Why Changing Step Size Requires Phase Correction

Because audio tones are continuous oscillations, altering the analysis hop changes the phase at which each block begins:

1. A tonal instrument produces continuous sinusoidal oscillations over time.
2. When the analysis hop differs from the synthesis hop, successive input blocks sample the ongoing oscillation at a different phase angle than the synthesis grid expects.
3. Without appropriate phase adjustment, overlapping blocks can interfere destructively, causing amplitude loss, modulation, or comb filtering.

**The Phase Vocoder's Role:**
The phase vocoder estimates frequency and phase progression from the analysis, then reconstructs phase for the synthesis hop. Errors in those estimates, interactions between sources, and changes to spectral magnitudes can still produce artifacts.

---

## 2. Phase Vocoder vs. WSOLA

Time stretching algorithms operate either in the **time domain** (splicing raw audio) or the **frequency domain** (manipulating Fourier bins).

| Feature | WSOLA (Time Domain) | Phase Vocoder (Frequency Domain) |
| :--- | :--- | :--- |
| **Mechanism** | Searches for similar waveform segments and overlaps shifted blocks. | Analyzes frequency bins and reconstructs phase on the synthesis grid. |
| **Mostly periodic audio** | Can preserve waveform shape when suitable matches exist. | Can preserve pitch, but quality depends on phase estimation and reconstruction. |
| **Polyphonic mixes** | One shift may not align all overlapping components. | Can adjust spectral regions separately, but overlapping sources can share bins. |
| **Possible artifacts** | Repetition, discontinuities, or interference between mismatched segments. | Phasiness, transient smearing, metallic noise, or loss of definition. |

### The Single Time-Shift Constraint

WSOLA applies one shift to a composite waveform segment. A shift that aligns one component may misalign another with a different period. This makes some dense mixes difficult; it does not mean WSOLA always fails on polyphonic audio.

A phase vocoder offers more control over separate spectral regions. It does not independently recover every instrument from a mixed signal, and neither method guarantees transparent playback at large slowdown ratios.

---

## 3. The Two Axes of Alignment: Horizontal vs. Vertical

Aligning audio requires balancing two distinct dimensions on the STFT grid:

1. **Horizontal Alignment (Over Time):**
   Making each individual bin's sine wave connect smoothly from slice to slice along the timeline.
2. **Vertical Alignment (Across Bins at the Same Instant):**
   Preserving the relative phase relationships that describe a component across neighboring bins; the bins do not need identical phases.

### The Limitation of Independent Horizontal Alignment

Independent phase advancement can lose relationships that matter to the reconstructed waveform:

- **Tonal phasiness:** Windowing spreads a tone across neighboring bins. Inconsistent phase reconstruction across that region can change its waveform and apparent definition. The region's width depends on the window and frequency resolution.
- **Transient smearing:** Broadband phase relationships help localize an attack in time. Changing those relationships can spread its energy over time.
- **Unnatural vocal texture:** Changes to relationships between partials can alter waveform shape. Pitch estimation, magnitude shaping, and noise processing can also contribute to robotic or metallic sound.

Preserving appropriate relationships across time and frequency is the objective. Forcing unrelated bins or instruments into a common phase relationship can itself introduce artifacts.

---

## 4. The Theoretical Foundation: Phase Vocoder Done Right (PVDR)

[*Phase Vocoder Done Right*](https://www.eurasip.org/Proceedings/Eusipco/Eusipco2017/papers/1570343436.pdf) (Zdeněk Průša and Nicki Holighaus, EUSIPCO 2017) reconstructs synthesis phase by integrating estimates of phase change along both time and frequency. It uses real-time phase-gradient heap integration and estimates phase derivatives with centered finite differences.

The temporal derivative describes local frequency progression; the frequency derivative describes local timing. Integrating both addresses limitations of advancing bins independently. The published results motivate the approach, but do not establish artifact-free behavior for every recording or modified implementation.

YARG uses a derived implementation with derivative-window and time-weighted-window estimates, additional peak handling, transient detection, stereo corrections, and texture resynthesis. It should not be described as an exact reproduction of the paper. Gaussian-window identities relating log-magnitude and phase derivatives also should not be assumed to apply unchanged to every analysis window.

---

## 5. YARG's Native Engine: PVDR Implementation & Custom Enhancements

YARG stretches the mixed output using a PVDR-derived phase reconstruction pipeline, transient protection, stereo correction, and noise resynthesis. The mechanisms below describe the current implementation. Their effect depends on the recording and playback speed; remaining artifacts require listening comparisons.

### Tier 1: Core Phase Reconstruction

#### A. Priority Heap Traversal

The engine processes significant spectral regions through an energy-prioritized heap. Strong regions provide phase references for neighboring bins, reducing the influence of weak, unreliable phase estimates. Additional peak protection guides the high-frequency region. Transient frames use the same traversal so each dependent bin follows its phase reference in the processing order.

A new continuity preference favors an established tonal reference in the lower-frequency traversal. A bin qualifies when it was a previous-frame temporal root and local energy peak, remains prominent above the smoothed spectral background, and retains at least half both its previous power and the strongest adjacent-bin power. Its current power must also exceed 0.1% of the strongest current or previous bin power. Qualified references receive a priority multiplier of 1.5 for both their temporal entry and their propagation entry in the heap. This is a soft preference, not a fixed assignment: other references can still outrank it, and parents are rebuilt in processing order every frame.

The preference uses shared reference decisions across channels and applies only to new input analyses during slowdown without pitch mapping or formant processing. It bypasses active transient protection and pending transient collection. Seeks, resets, sustained silence, and draining invalidate its history. It does not alter phase gradients, freeze frequency estimates, add waveform corrections, or change sample timing. It reuses the previous parent assignments rather than implementing a full spectral-lobe tracker, so it cannot distinguish unresolved sources sharing a spectral region. Listening feedback suggests improved pitch coherence with this preference, although intermittent bass wobble remains and its cause has not been established.

#### B. Reassigned 2D Spectral Gradients

Each slice is analyzed with the regular window, a derivative window, and a time-weighted window. These evaluations estimate instantaneous frequency and time-of-arrival information. The engine uses those estimates to advance phase over time and propagate phase across neighboring frequencies.

These are numerical estimates. Their accuracy depends on the window, signal energy, and overlapping components; no measured percentage improvement is established here.


A new tonal-root frequency cross-check compares the derivative-window temporal phase advance with the measured phase progression between current and earlier source spectra. It applies only on new analyses to temporal roots already qualified by the established-reference preference, below 2500 Hz. That preference excludes active transient protection, pending transient collection, pitch mapping, and formant processing. Its history is invalidated by seeks, resets, silence clearing, and draining, so a fresh frame cannot qualify immediately.

The source comparison uses the actual separation of those spectra: one output hop when the earlier source spectrum is reanalysed, or the actual source-hop length when the existing previous spectrum is retained. It subtracts the expected advance at the bin's center frequency, unwraps the residual onto the branch nearest the derivative estimate, and converts the result to an output-hop phase advance. This follows the phase-difference estimation principle described in [Phase Vocoder Done Right](https://www.eurasip.org/Proceedings/Eusipco/Eusipco2017/papers/1570343436.pdf); the blend and eligibility thresholds are experimental additions, not results established by that paper.

A disagreement exceeding 0.025 radians per output hop gradually enables the blend over a further 0.25 radians. Similar current and earlier bin powers increase the weighting; a weak endpoint reduces it. The measured progression receives at most half the weight, and the added root rotation is bounded to 0.12 radians per hop. Only the dominant channel's selected root receives this correction before the existing inter-channel phase transfer and dependent-bin reconstruction. The underlying derivative history and frequency-direction gradients remain unchanged. There is no frequency quantization or long-term pitch smoothing.

A local agreement check now refines that confidence using up to two adjacent bins on each side of the reference. The scan stops at an edge, plateau, or rise in either the current or earlier source spectrum, and stops when either endpoint falls below 10% of the reference's corresponding power. This confines the comparison to strong descending portions of the same candidate peak. Neighboring source-phase advances are compared with the reference's advance, unwrapped relative to it, and converted to output-hop units. Agreement falls smoothly with squared deviation relative to a 0.1-radian-per-hop tolerance, weighted by the geometric mean of each neighbor's current and earlier powers.

The result multiplies the previous confidence by a factor between 0.25 and 1. No usable neighboring evidence leaves the prior behavior unchanged; agreement preserves its strength, while disagreement reduces it. The correction cap, reference selection, derivative history, EQ, and phase locks are unchanged. This does not average the neighbors into a replacement frequency or quantize pitch. The comparison reads only source spectra, so its result is independent of the output reconstruction order. Overlapping unresolved notes and genuine frequency motion can still make a candidate peak's bins disagree; the thresholds are experimental.

This targets piano and higher bass notes reported as wobbly or out of tune at 35%, including the listening example of Tiny Dancer. Equal endpoint powers do not establish a single reliable partial, and the two estimators average different source-time intervals during slowdown. Genuine bends and vibrato can also cause disagreement. The limited blend cannot separate unresolved piano unisons. Listening feedback reported a substantial improvement from the original frequency cross-check; the additional local-agreement refinement has not yet been confirmed by listening. Noise resynthesis, EQ matching, phase locks, and sample timing remain unchanged.

#### C. Parent-Child Peak Protection & Vertical Locking

Windowing spreads a tone across neighboring frequency bins. The reconstruction assigns parent references and propagates phase to dependent bins, helping preserve the tone's spectral shape. Strong high-frequency peaks also protect nearby bins from arbitrary phase changes.

Without pitch mapping, dependent bins in the lower-frequency region use scaled frequency-gradient integration through a stretch factor of 6, and preserve their current input phase relationship to the already-processed parent above that factor. The crossover is approximately 11% of the sample rate, or 4850 Hz at 44.1 kHz. The parent comes from the energy-prioritized traversal and is not necessarily a tracked spectral peak. A proposed experiment extending input-relative propagation to moderate slowdowns was briefly edited into the source, then withdrawn before listening feedback on that specific change was established.

This protection belongs to the phase reconstruction. The separate noise classifier uses the lobe-based protection described below.

#### D. Noise Floor Phase Randomization

Bins below an energy threshold relative to the strongest spectral region receive randomized phase. This handles very weak regions whose phase is unreliable. Audible broadband texture is handled separately by noise resynthesis.

---

### Tier 2: Custom Audio Enhancements

#### E. Transient Detection & Phase Snapping

The onset detector measures energy increases across frequency regions. It compares each bin with the strongest nearby bin in the previous analysis to reduce false triggers from pitch movement. A separate low-frequency detector also considers bass attacks.

During slowdown without pitch mapping or formant processing, a detected onset starts collecting rising spectral lobes rather than immediately resetting every bin above 1500 Hz. The lobes are separated at amplitude minima. Their energy position is estimated from the time-weighted analysis, combining all channels. The timing threshold is calibrated from the actual analysis window using the energy center of a windowed linear ramp.

The collected regions reset to their input phase together when at least half their current energy belongs to lobes whose estimated position has passed the timing threshold. Both channels use the same event and reset hop, while retaining their own input phase. Collection and the event's repeat suppression are bounded by one analysis-window length of source audio, so they remain active long enough at 10% speed without depending on output-buffer boundaries. Seeks, resets, sustained silence, draining, and leaving this processing path clear the pending event.

This follows the center-timed reset and synchronized transient-set ideas in [Röbel's transient-processing paper](https://dafx.de/paper-archive/2003/pdfs/dafx32.pdf), while retaining the existing energy-rise onset detector. It operates during eligible slowdowns, including 35% and 50%, and leaves the overall time mapping unchanged. Pitch mapping and formant processing retain the earlier immediate onset reset: bins above 1500 Hz snap, along with lower bins exhibiting a strong individual energy rise.

Noise replacement pauses during the brief initial transient protection and again around a delayed reset; it does not pause throughout the pending event. This also pauses texture processing for simultaneous sustained noise, such as distorted guitars under blastbeats. Selective, synchronized resets target attack smearing without forcing a local playback speed of 100%. They do not guarantee unstretched attack envelopes, and multiple attacks inside one analysis window or unresolved overlapping sources remain limitations.

#### F. Attack Gain Restoration

Bins above 250 Hz with a strong energy increase receive a bounded gain increase. A separately configured, gentler path handles medium attacks. Both use one gain for all channels, with a maximum amplitude multiplier of 1.25. This is intended to preserve attack prominence; later dynamics stages can modify the result.

#### G. Stereo Coherence & Bass Phase Preservation

The main reconstruction uses a dominant channel as a phase reference and transfers the input's relative channel phase to the other channels. During transient phase resetting, each channel retains its own input phase.

An additional stereo correction between approximately 800 and 5000 Hz steers the weaker output channel toward the input's relative phase while preserving that channel's spectral magnitude. Its correction fades from 3500 to 5000 Hz.

Further stereo processing targets the input's channel relationships. Below 100 Hz, bass correction targets drift from the input stereo phase difference. Its strength decreases when the input channel levels are very unequal, and effectively silent channels receive no correction.

#### H. Remaining Local Phase Locking

Neighboring-bin locking adjusts the immediate neighbors of prominent peaks using a shared phase rotation across channels. Main-lobe locking adjusts nearby descending peak regions separately by channel. These corrections are restricted below approximately 5000 Hz and fade from 3500 to 5000 Hz. Both remain active alongside the core phase reconstruction.

Both stages were temporarily removed to target slight piano pitch wobble reported at 60% speed. Listening feedback suggested possibly more coherent pitch, but much stronger bass wobble and less sharpness than the previous version. Both were restored after that comparison. Removing only the main-lobe correction subsequently received feedback that pitch sounded good, with occasional low-frequency wobble still present. Later feedback reported wobbly, slightly detuned bass guitar and a possible increase in metallic coloration following recent changes. Both stages and the original low-frequency propagation condition have now been restored. The feedback does not isolate which change affected each artifact or establish the cause of the remaining wobble.

The remaining vocal-presence correction has been removed to target persistent vocal graininess at 35% speed. It steered adjacent-bin phases toward the current input relationship, which can vary between hops for irregular vocal texture. The audible effect of this removal remains unconfirmed.

Additional sidelobe phase steering has been removed. Its exclusion marking has now also been removed because the vocal-presence stage was its only consumer. The additional vocal correction that linked peaks near summed frequencies has been removed; frequency proximity alone does not establish that peaks belong to the same source in the mixed output.

#### I. Noise Classification & Grain-Based Texture Resynthesis

The classifier uses temporal and spectral medians to distinguish sustained tones, attacks, and texture. It waits for 17 analysis-history entries before replacement begins. The grain path is used for mono or stereo processing with sufficient window overlap and without split computation. Texture is reconstructed from short waveform grains, aligned against the previous tail and crossfaded into the output. Alignment now favors smaller shifts: a shift at the search boundary must improve normalized correlation by approximately 0.05 over zero shift to be selected. Zero shift is always evaluated, and exact score ties favor the smaller shift. Both channels share the selected offset; crossfade normalization uses the actual correlation rather than the penalized selection score. Listening feedback reported less metallic vocal coloration after the alignment change, with residual drum and vocal artifacts. This is not a benchmark establishing general improvement.

During slowdown, the grain path adds controlled phase decorrelation. Its amount rises smoothly with the smoothed stretch ratio from zero at 50% playback speed toward its maximum at 25%; at 35%, the overall amount is approximately 43% before per-bin protection and frequency weighting. There is no separate preset or toggle. Each bin's current noise-replacement target further scales the amount, so fully tone-protected bins receive no new randomization even while an earlier replacement mask is releasing. Each eligible bin receives a unit phase rotation shared by both channels, retaining its spectral magnitude and input inter-channel phase relationship in the calculation. This extends decorrelation to 35% playback to target persistent correlated, buzzy cymbal texture; it does not apply a general treble cut.

A selective treble increase now raises decorrelation for bins where the existing classifier strongly favors unprotected texture. Confidence rises as the attack-aware texture weight after tonal shelter increases from 0.85 to 1. The added frequency weight rises from zero at approximately 4500 Hz to full at 6750 Hz with the current crossover configuration. Its speed weight reaches full by approximately 35.7% playback; at 35%, fully eligible bins receive full decorrelation before replacement-target weighting. Ambiguous bins retain the base amount, and fully protected bins receive no new randomization. This changes phase statistics rather than applying treble attenuation or changing the replacement mask. Stereo rotations and the normalization calculation use the same final phase amount. The classifier remains a heuristic and can misclassify dense guitar harmonics. Grain-window normalization uses a shared power-weighted coherence estimate, so changing treble phase statistics can also affect the reconstructed grain envelope outside the increased-decorrelation region; unchanged per-bin phase amounts do not guarantee identical output there. Listening feedback also reports that noisy material sounds more hollow at 35% than at normal speed. This treble experiment has not established a remedy for that loss of body.

Randomized phases change the expected within-frame noise envelope. Grain normalization therefore blends the original window compensation with compensation based on mean analysis-window power, using a power-weighted estimate of phase coherence. This avoids applying the full inverse-window edge boost to decorrelated noise. The estimate assumes noise-like content; it does not guarantee constant output power or correct treatment of misclassified harmonics. Grain duration and the alignment search remain unchanged.

Decorrelation uses a separate reproducible random sequence. Seeks, resets, and silence clearing restart that sequence, and explicit seeded resets seed it. Startup and transient, pitch-mapping, and formant bypasses generate no new randomized grains; the existing previous-tail fade remains. The classifier is only called when the grain path was configured, including its channel-count, computation, and overlap requirements.

- **Lobe-based protection:** Strong or tonal peaks protect neighboring bins along descending spectral slopes. Protection stops at valleys or plateaus, while each bin retains its own tonal confidence. This replaces the former broad fixed-radius sheltering.
- **Full replacement at 50%:** Maximum replacement reaches 100% at 50% playback speed and below. Actual replacement still depends on classification, tonal protection, and the frequency taper.
- **Frequency transition:** Replacement begins above 2250 Hz and reaches its full frequency weight at 4500 Hz. Lower-frequency texture remains on the vocoder path.
- **Replacement transitions:** A full increase from zero to 100% is limited to approximately 30 ms of output time. Ordinary classification decreases now have a full-scale release of approximately 15 ms, rounded to output hops; smaller changes settle sooner. This reduces abrupt switching but can briefly retain replacement after a bin becomes tone-protected. Detected-transient, pitch-mapping, and formant bypasses still clear the mask immediately. Vocoder attenuation and grain synthesis use the same replacement amount, retaining complementary power weights.
- **Lifecycle:** Pitch mapping, formant processing, and transient protection bypass replacement. Seeks and resets clear history and grain state; a bypass clears the replacement mask. The previous grain tail fades through the existing output path when replacement is bypassed.

The classifier is a heuristic. Dense distorted guitar harmonics can resemble noise, so stronger replacement can reduce phasiness while changing guitar body. Listening feedback reports a substantial improvement at 10% after grain-phase decorrelation, with smeared and grainy transients still present. The replacement ramp smooths changes in the mask; once the mask is steady, grain duration and alignment continue to determine the texture's continuity.

#### J. Spectral Shaping & Slowdown Safeguards

The following magnitude adjustments remain active. Values below describe individual stages, not guaranteed final-output bounds; later processing and overlap-add can change the result. “Shared” means the stage applies the same gain to every channel at a bin.

| Enhancement | Current behavior | Channel gains |
| :--- | :--- | :--- |
| Spectral contrast and anti-ringing | Attenuates input bins below the smoothed spectral background, with an amplitude floor of 0.88 for this stage. | Separate |
| Causal pre-echo suppression | Uses measured within-window timing and rising energy to attenuate content associated with an upcoming attack; gain is at least 0.70. | Shared |
| Peak sharpening | Reduces neighboring peak skirts, with gain at least 0.80, and boosts peak centers by at most 2%. Its frequency taper is described below. | Shared |
| Bark-band valley suppression | Attenuates weak, non-prominent regions below a frequency-band masking estimate between approximately 350 and 16000 Hz; nearby prominent peaks are protected and gain is at least 0.82. | Shared |
| De-essing | Attenuates prominent spectral regions between 5000 and 12000 Hz, with gain at least 0.85. This is a spectral heuristic, not a vocal-source detector. | Shared |
| Extreme-slowdown attenuation | Begins below approximately 45% speed and increasingly attenuates bins below the smoothed background. | Separate |
| Dynamic modulation restoration | Scales magnitudes using their energy relative to the smoothed spectral background; amplitude gain is limited to 0.86–1.16. The recent high-frequency taper on added gain has been withdrawn after listening feedback. | Shared |
| High-frequency decay damping | Attenuates falling input energy with increasing frequency weight. On output hops without a new spectrum, it applies gentler damping above 4000 Hz. | Separate |
| Post-transient midrange damping | During cooldown after an attack, attenuates falling energy between approximately 260 and 1350 Hz, with frequency fades and gain at least 0.78. | Shared |
| Spectral boost limiting | Softly limits power above 1.25 times the predicted reference power, approaching a maximum ratio of 2. | Shared |
| Spectral gain diffusion | Redistributes power between neighboring bins toward their predicted reference proportions, preserving the pair's summed spectral power in the calculation. | Shared |
| Gain floor | Raises nonzero output bins below a slowdown-dependent fraction of predicted reference power, reaching a reference-power fraction of 0.25. It does not restore an exactly zero bin. | Separate |

Peak sharpening fades above 3500 Hz and is inactive at 5000 Hz and above. The taper applies to both the peak frequency and neighboring-bin frequency, so sharpening cannot attenuate a neighbor at or above the cutoff. Peak boosts derive from the reduced skirt attenuation and retain their existing limit. This targets piercing cymbal coloration without a general treble cut. Listening feedback reported little improvement from this change.

The stage labeled dynamic modulation restoration measures spectral contrast rather than modulation over time. Its recent high-frequency added-gain taper was withdrawn after listening feedback found no improvement in piercing, grainy cymbals at 35%. The original amplitude-gain range of 0.86–1.16 is restored. This stage does not cap the combined gain of every stage or correct phase-related coloration.

Most enhancement strengths increase with the smoothed slowdown ratio. They use individual bypass conditions: many pause during transient protection, some also pause during cooldown or pitch mapping, and some require a new input spectrum. There is no single bypass condition shared by every stage. The post-transient midrange stage specifically runs during cooldown, outside active transient protection.

Shared gains preserve the per-bin channel ratio at that stage; separately calculated gains can change it. Phase stages and the later mixture of vocoder output and grains also influence the final stereo image. Complementary vocoder/grain power weights do not guarantee constant waveform power when the two paths are correlated.

Practice playback extends down to 10% speed, with recording-dependent quality. These mechanisms are intended to control artifacts and dynamics; their combined effect remains subject to listening evaluation.

#### K. Grain Processing CPU Optimizations

The alignment search computes the previous tail's power once and reuses it across candidate offsets. Candidate waveform power retains its original summation order.

An all-zero replacement mask skips inverse FFTs and alignment searches. The previous tail still fades into queued output and is then cleared. CPU savings have not been benchmarked.

### Source-to-Output Tonal Balance

A new EQ experiment compares broad source-band power with the reconstructed waveform after overlap-add and grain mixing, before this EQ applies its own correction. The source reference reuses the existing analysis spectrum. Its weights include the frequency responses of the same two low-pass filters used to measure and reconstruct three broad waveform bands, with nominal crossover frequencies of 2000 and 6000 Hz. The crossovers scale down at low sample rates. Spectral power is normalized using the transform length and analysis-window power.

Each source-analysis reference is queued for the corresponding synthesis-center position in output time, then held for one output hop. This follows the engine's source-frame mapping rather than comparing the latest incoming chunk with delayed output. Alignment is at the analysis-frame center; the windowed source estimate and reconstructed waveform do not have identical temporal support during stretching, so matching is approximate, particularly near attacks and speed changes.

Both source and pre-correction output power are averaged over approximately 250 ms of output time. Matching starts after 250 ms of continuous eligibility. Gain targets update once per output hop, use square-root power ratios with a small regularization term for weak bands, and are bounded to approximately -2 dB and +1 dB per band. Applied gains approach those targets over approximately 150 ms. Both channels receive the same gains and filter responses. Unity gains reconstruct the original waveform through a complementary split; non-unity gains introduce the frequency and phase response of the broad EQ. The per-band bounds do not guarantee an identical bound on the combined frequency response.

The experiment operates on the configured mono/stereo grain path during slowdown without pitch mapping or formant processing. Other speeds and modes relax gains toward unity after their queued references arrive. It adds no output-buffer delay or FFT, but does add two low-pass updates per channel per output sample, power averaging, and broad-band source-power accumulation per hop. Allocations occur during configuration. Seeks, resets, and sustained-silence clearing reset the filters, gains, power estimates, and queued references. Final draining applies the current gains after tail mixing and cancellation, freezes adaptation, and then clears history.

This targets sharper highs and reduced warmth reported at 35% compared with normal playback. It does not move frequencies or repair phase cancellation, and has not been confirmed to restore body or reduce metallic texture. Listening comparison remains required.

### Current Tuning Decisions

Controlled grain-phase decorrelation originally began below approximately 20% playback speed and received listening feedback of a substantial improvement at 10%, with transients still smeared and grainy. Its transition now begins below 50% speed and reaches full strength at 25%, targeting sharp, buzzy cymbals reported at 35%. Tonal and transient protections and expected grain-window normalization remain active. Listening feedback found cymbals slightly improved at 35%, but still sharper and more metallic than the comparison stretcher. A further selective increase now targets confidently classified treble texture while retaining the base amount for ambiguous regions. Its audible improvement and preservation of dense guitar texture remain unconfirmed.

Selective, center-timed transient phase resetting now collects rising spectral lobes and resets them on a shared hop. Its source-based event lifetime addresses the mismatch between slow source progression and short output-based protection at extreme slowdown. The transient traversal also now rebuilds phase parents instead of sorting bins independently of their references. The existing 10% noise decorrelation remains active. The audible effect of these transient changes is unconfirmed.

Removing additional neighboring-bin and main-lobe phase locking was tried for piano wobble at 60% speed. Listening feedback reported possible pitch-coherence improvement, but much stronger bass wobble and reduced sharpness, so both stages were restored. Removing only main-lobe phase locking subsequently received feedback that pitch sounded good, with occasional low-frequency wobble remaining. Further feedback reported wobbly, slightly detuned bass guitar and a possible increase in metallic coloration following recent changes. Both phase-locking stages are restored; their removal did not establish a satisfactory overall improvement. Spectral-lobe tracking across frames has not been added.

Extending input-relative parent propagation in the lower-frequency region to moderate slowdowns was briefly edited into the source, then withdrawn while the recent phase-locking comparisons were reconsidered. The original propagation condition and minimum stretch-factor clamp remain in place. Listening feedback was not established for that specific experiment, so it should not be treated as a confirmed cause or cure of the bass wobble or metallic coloration.

The traversal-priority preference for established tonal references is retained after listening feedback suggested improved pitch coherence. Its removal for a bass-wobble comparison has been reversed; the preference was not established as a cause of the remaining wobble. Both phase-locking stages and the original phase-propagation formulas remain active. General pitch stability and preservation of sharpness still require broader listening evaluation.

The recent high-frequency taper on added gain in the spectral-contrast-based modulation-restoration stage has been withdrawn. Listening feedback found no improvement and possibly worse piercing, grainy cymbals at 35%, so the prior gain behavior is restored. This feedback does not establish that the taper caused the artifacts. A bounded, broad-band source-to-output EQ experiment is now implemented as described above. It could address tonal-balance differences, but does not correct pitch errors or guarantee removal of phase and texture artifacts.

Ordinary decreases in noise replacement now use a short release, targeting intermittent vocal and transient-like buzziness reported at 50% and below, more audibly at 35%. Global bypass conditions still take effect immediately. The release delays per-bin classifier protection briefly; its audible benefit remains unconfirmed.

Reverb-tail damping has been removed. Its stereo measure depended on channel levels rather than phase or measured temporal coherence, so equal-level noise could receive full protection while a dry panned source could be attenuated. Its spectral heuristic also did not establish that the content was reverberation. Removal avoids this attenuation but may retain more tail energy; its audible effect remains unconfirmed.

Vocal-presence phase correction was first revised to use the same unit rotation on every channel, resolving asymmetric corrections. The stage has now been removed entirely for a comparison targeting persistent graininess in vocals at 35% speed. Its unused exclusion flags and marking pass have also been removed. Core phase reconstruction, neighboring-peak locking, main-lobe locking, stereo corrections, and grain processing remain active. The neighboring-peak and main-lobe stages were temporarily removed and then restored after the piano-wobble comparisons described above.

Overlap-cancellation gain compensation has been removed. It boosted individual bins based on phase advance relative to the bin center frequency, which can differ during valid frequency progression and does not by itself measure cancellation in the overlapping waveforms. Removing it avoids that additional phase-dependent gain modulation. The audible effect on metallic vocals and cymbals at 35% speed remains unconfirmed.

Sidelobe phase steering has been removed to target remaining metallic drum and vocal coloration at 35% speed. Its exclusions were retained initially and removed once vocal-presence processing was also removed. Listening feedback suggested a possible improvement, with metallic cymbals still present at 35% speed.

Local phase-curvature correction has been removed to avoid additional individual-bin rotations after the main phase reconstruction. Listening feedback reported an improvement after removal, with metallic vocal coloration still present at 35% speed.

Strongest-peak bass pitch steering, fixed harmonic phase steering, and the separate sum-frequency steering stage have been removed. Listening comparisons reported less wobble in low distorted guitar and less dissonance in piano chords after removing these corrections. The remaining vocal phase-steering correction has now also been removed to target residual metallic coloration at 35% speed. Its measured-frequency timing, confidence checks, and error bounds did not establish that the linked peaks belonged to the same source. Listening feedback reported an improvement after removal, with slight metallic and grainy growling vocals still present at 35% speed.

The mid/side width and phase correction has also been removed. Listening feedback reported a substantial improvement in stereo coherence. Core channel coupling and bass stereo phase correction remain active. The audit identified channel-dependent spectral contrast, extreme-slowdown attenuation, high-frequency decay damping, and gain-floor adjustments as further possible sources of balance changes; these stages remain active. The overlap-cancellation compensation identified in that audit has now been removed.

The replacement ramp has not been established as a general improvement across a representative song set. Listening observations are subjective comparisons, not controlled measurements. At 35% speed, the latest listening feedback found residual vocal graininess acceptable, although some heavy metal recordings have also exhibited metallic growling vocals and piercing, metallic cymbals. Graininess has also been reported at 10% speed. Piano tuning, distorted-guitar body, transient clarity, and stereo coherence remain useful comparison targets.

Phase-predictability-based noise classification was discussed but has not been implemented. It should not be listed as an active enhancement. The latest overlap-cancellation removal still needs listening confirmation; the source audit does not establish its audible benefit.

---

## 6. References & Further Reading

- Průša, Z., & Holighaus, N. (2017). *Phase Vocoder Done Right*. 25th European Signal Processing Conference (EUSIPCO 2017). [arXiv:2202.07382](https://arxiv.org/abs/2202.07382).
- Röbel, A. (2003). *A New Approach to Transient Processing in the Phase Vocoder*. 6th International Conference on Digital Audio Effects (DAFx-03). [Paper](https://dafx.de/paper-archive/2003/pdfs/dafx32.pdf).
- Průša, Z., Balazs, P., & Søndergaard, P. L. (2017). *A Noniterative Method for Reconstruction of Phase from STFT Magnitude*. IEEE/ACM Transactions on Audio, Speech, and Language Processing. [arXiv:1605.07474](https://arxiv.org/abs/1605.07474).
