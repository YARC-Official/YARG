# Time Stretching in YARG: A Plain-English Guide

This is the human-readable guide to YARG's time-stretch engine. It explains what the engine does, why it's hard, what we borrowed, and what we built ourselves.

> For developers: the engine lives in `Native/YargAudio/src/stretch/` (`StretchTempoStream.{h,cpp}` and `YargStretch.h`), built with `signalsmith-linear` 0.3.1 (see `Native/YargAudio/third_party/signalsmith-linear/`). C ABI v23. Audio renders in blocks of 256 frames.

## 1. What it does (in game terms)

Practice Mode lets you slow a song to 10% speed or push it to 200% without changing its pitch. Chipmunk/pitch modes and tiny synchronizer nudges use the same engine.

Naive slowdown just plays samples back slower, which drops pitch (the "slow tape" effect). Time stretching keeps pitch fixed while changing tempo, so a guitar chord at half speed still sounds like the same chord, just longer.

## 2. What a phase vocoder is

Think of sound as a flipbook of frozen moments:

1. **Slice.** The engine chops audio into short overlapping snapshots (about 60–80 ms each, overlapping heavily). This is called a Short-Time Fourier Transform (STFT).
2. **Photograph each slice.** Every snapshot is broken into narrow pitch bands ("bins"), like the strings on a piano. For each band we record two things:
   - **Magnitude:** how loud that pitch is right now.
   - **Phase:** where that pitch is in its vibration cycle (its timing).
3. **Respacing.** To play slower, we space the snapshots further apart; to play faster, we pack them closer. The pitch bands stay where they are, so pitch doesn't move.
4. **Glue.** Overlapping snapshots are blended back together (overlap-add). The hard part is step 3: once you move snapshots around, the timing (phase) no longer lines up, so you have to invent plausible timing for every band in every snapshot. That invention is the "phase vocoder."

A good phase vocoder keeps sustained notes smooth. A bad one makes everything sound swimmy, metallic, or underwater.

## 3. Why drums break it (transients)

Long snapshots are great for bass pitch (you need a long look to tell low notes apart) but terrible for drums. A snare hit lasts a few milliseconds; smeared across an 80 ms window it turns into two audible defects:

- **Pre-echo ("swish"):** you hear the hit faintly *before* it lands, like a reversed cymbal.
- **Soft hits:** the attack peak gets flattened, so drums lose punch.

There is no free lunch here: one window size cannot be perfect for both a 60 Hz bass note and a 2 ms drum crack. Everything below is about picking the right tradeoff per sound, per moment.

## 4. What Signalsmith gives us

Signalsmith Stretch is the base engine we vendor (unmodified Linear FFT/STFT plumbing plus a patched Stretch core). Out of the box it handles the fundamentals well: slicing, overlap-add, basic transient detection, pitch shifting, seeking, and the per-block pull model our audio callback drives.

We keep its scaffolding and replace or extend its judgment calls: when a drum is happening, whose timing to trust, how strict to be, and what to do with stereo and harmonics.

## 5. Where YARG diverges (our enhancements)

Each item below is: the problem you would hear, what we do, and what you hear instead.

### A. Smarter drum detection

**Problem:** pitch wobble (guitar vibrato sliding across bands) looks like a drum attack to naive detectors, so protection fires constantly and smears real music.

**What we do:** a SuperFlux-style check. Each band is compared against the loudest of its 3-bin neighborhood in the previous frame, in three high bands above 1500 Hz. Only broadband jumps that are loud in absolute terms, loud relative to total power (0.3%), and rising fast (25% filtered, 45% per-bin unfiltered) count. Protection holds for half a block, then ignores new triggers for ~120 ms so decays can't machine-gun.

**You hear:** vibrato and slides pass through untouched; real drum hits engage protection within ~1 ms.

### B. Drum protection (snap the highs, spare the lows)

**Problem:** during a hit, invented timing smears the crack across the window.

**What we do:** while protection holds, bands above 1500 Hz snap to the measured input timing instead of the invented one. Bands below 1500 Hz stay locked to previous output — snapping bass destroys its body and causes hollow cancellation.

**You hear:** no pre-echo on snares and hats, bass stays full underneath.

### C. Punch restoration

**Problem:** windowing and blending dilute peak energy, so hits feel flat even when timed right.

**What we do:** attack frames get a small ~4% loudness lift to restore the crest the math shaved off.

**You hear:** drums hit like drums, without clipping.

### D. Protect drum hits

**Problem:** even detected hits get smeared if the stretcher treats them like sustains.

**What we do:** a confirmed onset opens a short protection window (half a block, then new triggers are ignored for 1.5 blocks so decay bumps can't machine-gun). While it holds, texture replacement is bypassed and the hit keeps transient phase locking, so the attack renders with its timing intact. Song position still maps exactly through the block history.

**You hear:** sharp attacks at any practice speed, with average tempo still exact.

### E. Better pitch tracking (PVDR)

**Problem:** stock neighbor-blending guesses each band's timing from adjacent bands with rough heuristics, which wanders on sweeps and chords.

**What we do:** we integrated the "Phase Vocoder Done Right" idea (Průša & Holighaus): every band picks one explicit parent — its own past or an already-solved frequency neighbor — via a loudest-first heap. Time estimates are averaged across frames; frequency estimates follow the parent. We also fixed a scaling bug where loud inputs bent phase differently than quiet ones (normalized vertical predictions to linear amplitude scale).

**You hear:** steadier pitch on bends and chords, identical tone at any volume.

### F. Sharper measurement (reassigned gradients)

**Problem:** comparing consecutive snapshots to estimate pitch is noisy — one wobbly hop makes the next one wobble more.

**What we do:** two extra analyses per slice (a derivative window and a time-weighted window) measure each band's true pitch deviation and arrival time directly, instead of differencing hops.

**You hear:** far less warble on slow sweeps (measured pitch jitter down ~85%).

### G. Looser is more natural (relaxation + noise decoupling)

**Problem:** gluing every band rigidly to its neighbor forces inharmonic sounds (cymbals, breath, room) to pretend they're pure tones — metallic, phasey "air."

**What we do:** we relax the glue by frequency (firm ~0.8 below 2 kHz, easing to ~0.45 above 10 kHz) and relax it further (0.6x) for bands sitting below the smoothed background (i.e. noise, not tone).

**You hear:** highs breathe instead of ringing; reverb tails stay diffuse.

### H. Stereo stays wide

**Problem:** running left and right through independent vocoders scrambles arrival-time differences, collapsing the stereo image into mono mush.

**What we do:** during attacks, all channels follow the loudest channel's timing. In calm regions, ambient stereo locking only engages when channels genuinely agree (correlation above 0.96).

**You hear:** drums stay punchy and centered, wide reverbs stay wide.

### I. Vocals and guitars stay full (harmonic lock)

**Problem:** tiny independent pitch errors per harmonic accumulate into a random walk that scrambles the wave shape — vocals turn hollow and "underwater."

**What we do:** strong low fundamentals lock their 2nd and 3rd harmonics to the measured input relationship (gentle 0.5 / 0.4 steering, shared across channels). Pure sines, noise, and transients are unaffected by construction.

**You hear:** voice and guitar keep their body and timbre at slow speeds.

### J. Clean crawling (extreme slowdown)

**Problem:** below ~17% speed everything wants to flutter or fizz.

**What we do:** three guards. Strong high peaks shelter their 4-bin neighborhood from randomization. Below the split at extreme stretch, lows lock to measured input relationships instead of estimated gradients. A smoothed stretch estimate (fast snap on real >10% rate changes) stops fractional-hop FM wobble. Above the vocoder band, recorded texture grains take over the highs (2→4 kHz ramp, movement-proof peak shelter) so hiss energy stays flat instead of thinning out.

**You hear:** 10–20% speed stays listenable instead of disintegrating.

### K. Shipped default: preset 4

Presets stack cumulatively (0 = stock-ish, 5 = everything including distortion polish). YARG ships preset 4, baked in natively so every song gets it with no settings needed:

- 1 Transient (attack punch, snap)
- 2 SubBass (low-end alignment)
- 3 Phase (tonal mask, phase polish)
- 4 Harmonic (overtone lock)
- 5 Distortion polish stays off (audibly worse; kept as an experiment flag only)

At 100% speed the engine is near-passthrough: protection idles, no diffusion is added, impulses land within a sample of their true position.

## 6. The three-way tradeoff (phase vs pitch vs loudness)

You can't have all three perfect at once:

- **Phase (timing):** do all frequencies in a hit land at the same instant? Matters most for drums.
- **Pitch (frequency):** does each sustained note hold its exact center? Matters most for bass, voice, bends.
- **Magnitude (loudness/texture):** does energy stay natural — no pumping highs, no ringing noise floor?

Our choices, in one paragraph: on attacks we spend the budget on timing (detection + snap) and accept a small loudness lift; on sustains we spend it on pitch (PVDR + gradients + harmonic lock) and accept relaxed highs; on noise we spend nothing (decouple it and let it diffuse). The block history keeps the song clock exact regardless, so protection never drifts sync.

`position()` maps the output timeline to song time so the note highway and audio remain locked together.

## 7. How it plugs into YARG

Stems decode → stem mixer → `StretchTempoStream` → song mixer (one-shots and guide tones added *after* stretch, on the stretched timeline) → read-ahead → master volume → output. Everything is preallocated at setup; the audio callback never allocates, locks, or touches C#. Seeking purges delay lines and position history, reprimes, and discards pre-roll so output starts exactly at song zero.

## 8. Speed ("potato test")

Measured on dense stereo mixes at 44.1 kHz (higher is better; 1x = exactly keeping up):

| Speed | Throughput | Meaning |
| :--- | :--- | :--- |
| 0.10x | ~10.7x | Extreme slow-mo uses ~9% of one core |
| 0.20–0.50x | ~5.5–6.1x | Practice speeds use ~16–18% of one core |
| 1.00x | ~8.9x | Full speed uses ~11% of one core |
| 0.95–1.05x nudges | ~340x+ | Effectively free |

Verdict: even a weak dual-core has >80% headroom at half speed. No underruns.

## 9. Licensing & credit

- **Signalsmith Linear 0.3.1 + Signalsmith Stretch 1.3.2 (Base):** MIT licensed, vendored with `LICENSE.txt` under `Native/YargAudio/third_party/signalsmith-linear/`. Binary releases must include those MIT notices alongside YARG's own license.
- **Rubber Band:** GPL — zero code or algorithms taken. No copyleft contamination.
- **Ideas, not code:** PVDR (Průša & Holighaus 2022), SELEBI evaluation, SuperFlux onset detection (Böck & Widmer 2013), and reassignment techniques inspired the math; all YARG implementations were written from scratch for this engine.
