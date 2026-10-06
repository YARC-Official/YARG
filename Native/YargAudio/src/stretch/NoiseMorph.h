#ifndef YARG_AUDIO_STRETCH_NOISE_MORPH_H
#define YARG_AUDIO_STRETCH_NOISE_MORPH_H

#include "stretch/TextureGrains.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <vector>

// NoiseMorph - fuzzy sines/transients/noise split (Fierro & Valimaki 2023) with
// noise-morphing resynthesis (Moliner et al. 2024):
//
//   - tonalness from temporal median vs spectral median, transientness mirrored
//   - soft masks through a saturating sine curve; noise is whatever is neither
//   - texture removed from the vocoder output, resynthesized as shaped noise
//
// Bypassed (mask 0) until history is full, and during pitch shifts, formant
// work, and transients, where the main path already handles the spectrum.
namespace yarg::audio {

template<typename Sample>
class NoiseMorph {
    using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
    using Complex = std::complex<Sample>;
    static constexpr int HISTORY = 17;
    static constexpr int RADIUS = 16;
    static constexpr Sample REPLACEMENT_RISE_SECONDS{Sample(0.030)};
    static constexpr Sample REPLACEMENT_RELEASE_SECONDS{Sample(0.015)};
    static constexpr Sample SINE_LOW{Sample(0.70)};
    static constexpr Sample SINE_HIGH{Sample(0.80)};
    static constexpr Sample TRANSIENT_LOW{Sample(0.75)};
    static constexpr Sample TRANSIENT_HIGH{Sample(0.85)};
    static constexpr Sample CUTOFF_TIMES_MINIMUM{Sample(1.5)};
    static constexpr Sample SILENCE_FLOOR_RATIO{Sample(1e-6)};
    TextureGrains<Sample> grains;
    bool useGrains = false;
    std::vector<Sample> grainMask;
    Sample replacementStep = 0;
    std::vector<Sample> history, magnitude, noise;
    Sample peakMagnitude = 0;
    int channels = 0;
    int bands = 0;
    int historyIndex = 0;
    int historyCount = 0;

public:
    void configure(const STFT &analysis, int count, Sample sampleRate, bool grainPath) {
        useGrains = grainPath;
        channels = std::min(count, 2);
        bands = int(analysis.bands());
        replacementStep = Sample(analysis.defaultInterval()) / (sampleRate * REPLACEMENT_RISE_SECONDS);
        history.assign(bands * HISTORY, Sample(0));
        magnitude.assign(bands, Sample(0));
        noise.assign(bands, Sample(0));
        if (useGrains) {
            grains.configure(analysis, channels);
            grainMask.assign(bands, Sample(0));
        }
        clearHistory();
    }

    void clearHistory() {
        historyIndex = historyCount = 0;
        peakMagnitude = 0;
        std::fill(history.begin(), history.end(), Sample(0));
        std::fill(grainMask.begin(), grainMask.end(), Sample(0));
        if (useGrains) {
            grains.reset();
        }
    }

    bool usesGrains() const {
        return useGrains;
    }


    Sample readGrain(int channel) const {
        return grains.read(channel);
    }

    void advanceGrain() {
        grains.advance();
    }

    // Per-block split: measure bands, fuzzy-classify, move texture to the renderer.
    template<class Input>
    void apply(STFT &output, Input input, Sample strength, Sample minimumFrequency) {
        pushMagnitudes(input);
        if (historyCount < HISTORY || strength == 0) {
            std::fill(grainMask.begin(), grainMask.end(), Sample(0));
            grains.add(output, input, grainMask);
            return;
        }
        detectFuzzy();
        replaceTexture(output, input, strength, minimumFrequency);
    }

private:
    static Sample saturate(Sample value, Sample low, Sample high) {
        if (value <= low) {
            return Sample(0);
        }
        if (value >= high) {
            return Sample(1);
        }
        const Sample s = std::sin((value - low) / (high - low) * Sample(1.5707963267948966));
        return s * s;
    }

    // Records per-band magnitudes into the history ring.
    template<class Input>
    void pushMagnitudes(Input input) {
        for (int b = 0; b < bands; ++b) {
            Sample power = 0;
            for (int c = 0; c < channels; ++c) {
                power += std::norm(input(c, b));
            }
            magnitude[b] = std::sqrt(power);
            peakMagnitude = std::max(peakMagnitude, magnitude[b]);
            history[historyIndex * bands + b] = magnitude[b];
        }
        historyIndex = (historyIndex + 1) % HISTORY;
        historyCount = std::min(historyCount + 1, HISTORY);
    }

    // Temporal median finds steady tones; spectral median finds broadband hits.
    // A band steady over time but loud against its neighbours is a tone; the
    // mirror image is a transient; whatever is neither is texture.
    void detectFuzzy() {
        const Sample floor = peakMagnitude * SILENCE_FLOOR_RATIO;
        for (int b = 0; b < bands; ++b) {
            if (magnitude[b] < floor) {
                noise[b] = 0;
                continue;
            }
            std::array<Sample, HISTORY> temporal;
            std::array<Sample, RADIUS * 2 + 1> spectral;
            for (int i = 0; i < HISTORY; ++i) {
                temporal[i] = history[i * bands + b];
            }
            for (int i = -RADIUS; i <= RADIUS; ++i) {
                spectral[i + RADIUS] = magnitude[std::clamp(b + i, 0, bands - 1)];
            }
            std::nth_element(temporal.begin(), temporal.begin() + HISTORY / 2, temporal.end());
            std::nth_element(spectral.begin(), spectral.begin() + RADIUS, spectral.end());
            const Sample steady = temporal[HISTORY / 2];
            const Sample around = spectral[RADIUS];
            const Sample tonalness = steady / (steady + around + Sample(1e-30));
            const Sample sine = saturate(tonalness, SINE_LOW, SINE_HIGH);
            const Sample hit = saturate(Sample(1) - tonalness, TRANSIENT_LOW, TRANSIENT_HIGH);
            noise[b] = std::clamp(Sample(1) - sine - hit, Sample(0), Sample(1));
        }
    }

    // Slews the noise mask toward its target, removes texture from the main
    // spectrum, and hands it to the renderer.
    template<class Input>
    void replaceTexture(STFT &output, Input input, Sample strength, Sample minimumFrequency) {
        const Sample cutoff = minimumFrequency * CUTOFF_TIMES_MINIMUM;
        const Sample releaseStep = replacementStep * REPLACEMENT_RISE_SECONDS / REPLACEMENT_RELEASE_SECONDS;
        for (int b = 0; b < bands; ++b) {
            const Sample target = strength * noise[b] *
                std::clamp((output.binToFreq(b) - cutoff) / cutoff, Sample(0), Sample(1));
            grainMask[b] = std::clamp(target, grainMask[b] - releaseStep, grainMask[b] + replacementStep);
            const Sample keep = std::sqrt(Sample(1) - grainMask[b]);
            for (int c = 0; c < channels; ++c) {
                output.spectrum(c)[b] *= keep;
            }
        }
        grains.add(output, input, grainMask);
    }
};

}

#endif
