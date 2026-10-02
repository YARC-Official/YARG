#ifndef YARG_AUDIO_STRETCH_NOISE_MORPH_H
#define YARG_AUDIO_STRETCH_NOISE_MORPH_H

#include "stretch/TextureGrains.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <random>
#include <vector>

// NoiseMorph - splits each band into keep and replace parts so attacks stay sharp
// and sustained noise (crowd, cymbals, distortion) does not turn tonal under
// time stretching:
//
//   - Tones and transients are detected with temporal and spectral medians: a band
//     steady over time but loud against its neighbours is a tone; a simultaneous
//     burst across many neighbours is a transient. Both stay on the main path.
//   - Everything else is texture. It is removed from the vocoder output and handed
//     to TextureGrains for resynthesis from scratch.
//
// Bypassed (strength 0) until history is full, and during pitch shifts, formant
// work, and transients, where the main path already handles the spectrum.
namespace yarg::audio {

template<typename Sample>
class NoiseMorph {
    using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
    using Complex = std::complex<Sample>;
    static constexpr Sample PI{Sample(3.14159265358979323846)};
    static constexpr unsigned DEFAULT_SEED = 1;
    static constexpr int HISTORY = 17;
    static constexpr int RADIUS = 16;
    static constexpr Sample REPLACEMENT_RISE_SECONDS{Sample(0.030)};
    static constexpr Sample REPLACEMENT_RELEASE_SECONDS{Sample(0.015)};
    static constexpr Sample NOISE_CONFIDENCE_START{Sample(0.85)};
    static constexpr Sample NOISE_CONFIDENCE_RANGE{Sample(0.15)};
    static constexpr Sample TREBLE_DECORRELATION_RATE{Sample(2.5)};
    // Detection cutoffs: tone/attack ratio boundaries, peak-to-background ratio,
    // and the low-frequency fade-in for resynthesis.
    static constexpr Sample TONE_LOW{Sample(0.65)};
    static constexpr Sample TONE_RANGE{Sample(0.15)};
    static constexpr Sample ATTACK_HIGH{Sample(0.35)};
    // A slice much louder than the average around it counts as a peak.
    static constexpr double PEAK_TIMES_BACKGROUND = 4.0;
    // Minimum-frequency fade-in for resynthesis, as a multiple of the cutoff.
    static constexpr Sample CUTOFF_TIMES_MINIMUM{Sample(1.5)};
    TextureGrains<Sample> grains;
    bool useGrains = false;
    std::vector<Sample> grainMask;
    std::vector<Complex> grainPhase;
    std::minstd_rand randomEngine{DEFAULT_SEED};
    Sample replacementStep = 0;
    std::vector<double> sumPrefix;
    std::vector<Sample> history, magnitude, mask, tonal, protection;
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
        mask.assign(bands, Sample(0));
        tonal.assign(bands, Sample(0));
        if (useGrains) {
            grains.configure(analysis, channels, true);
            grainMask.assign(bands, Sample(0));
            grainPhase.assign(bands, Complex{Sample(1), Sample(0)});
            protection.assign(bands, Sample(0));
            sumPrefix.assign(bands + 1, 0);
        }
        clearHistory();
    }

    void clearHistory() {
        historyIndex = historyCount = 0;
        randomEngine.seed(DEFAULT_SEED);
        std::fill(history.begin(), history.end(), Sample(0));
        std::fill(grainMask.begin(), grainMask.end(), Sample(0));
        if (useGrains) {
            grains.reset();
        }
    }

    void reset(long seed) {
        clearHistory();
        randomEngine.seed(static_cast<std::minstd_rand::result_type>(seed));
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

    // Per-block split: measure bands, detect tones/transients, shelter music,
    // move texture to the grains.
    template<class Input>
    void apply(STFT &output, Input input, Sample strength, Sample minimumFrequency, Sample decorrelation) {
        pushMagnitudes(input);
        if (historyCount < HISTORY || strength == 0) {
            std::fill(grainMask.begin(), grainMask.end(), Sample(0));
            grains.add(output, input, grainMask);
            return;
        }
        detectTonesAndAttacks();
        markProtectedPeaks();
        shelterAndApply(output, input, strength, minimumFrequency, decorrelation);
    }

private:
    // Records per-band magnitudes into the history ring.
    template<class Input>
    void pushMagnitudes(Input input) {
        for (int b = 0; b < bands; ++b) {
            Sample power = 0;
            for (int c = 0; c < channels; ++c) {
                power += std::norm(input(c, b));
            }
            magnitude[b] = std::sqrt(power);
            history[historyIndex * bands + b] = magnitude[b];
        }
        historyIndex = (historyIndex + 1) % HISTORY;
        historyCount = std::min(historyCount + 1, HISTORY);
    }

    // Temporal median finds steady tones; spectral median finds broadband transients.
    void detectTonesAndAttacks() {
        for (int b = 0; b < bands; ++b) {
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
            Sample steady = temporal[HISTORY / 2];
            Sample around = spectral[RADIUS];
            Sample ratio = steady / (steady + around + Sample(1e-30));
            Sample tone = std::clamp((ratio - TONE_LOW) / TONE_RANGE, Sample(0), Sample(1));
            Sample attack = std::clamp((ATTACK_HIGH - ratio) / TONE_RANGE, Sample(0), Sample(1));
            tonal[b] = std::pow(std::sin(tone * Sample(1.5707963267948966)), 2);
            mask[b] = 1 - std::pow(std::sin(attack * Sample(1.5707963267948966)), 2);
        }
    }

    // Flags bands above background plus their neighbourhood as protected.
    void markProtectedPeaks() {
        std::fill(protection.begin(), protection.end(), Sample(0));
        sumPrefix[0] = 0;
        for (int b = 0; b < bands; ++b) {
            sumPrefix[b + 1] = sumPrefix[b] + double(magnitude[b]);
        }
        for (int b = 0; b < bands; ++b) {
            if ((b > 0 && magnitude[b] < magnitude[b - 1]) ||
                (b + 1 < bands && magnitude[b] < magnitude[b + 1])) {
                continue;
            }
            int low = std::max(0, b - RADIUS);
            int high = std::min(bands - 1, b + RADIUS);
            double background = (sumPrefix[high + 1] - sumPrefix[low]) / double(high - low + 1);
            Sample shelter = double(magnitude[b]) > background * PEAK_TIMES_BACKGROUND ?
                Sample(1) : tonal[b];
            if (shelter == Sample(0)) {
                continue;
            }
            protection[b] = std::max(protection[b], shelter);
            for (int i = b - 1; i >= 0 && magnitude[i] < magnitude[i + 1]; --i) {
                protection[i] = std::max(protection[i], shelter);
            }
            for (int i = b + 1; i < bands && magnitude[i] < magnitude[i - 1]; ++i) {
                protection[i] = std::max(protection[i], shelter);
            }
        }
    }

    // Extends tone protection to neighbouring bands, removes texture from the main
    // spectrum, and hands it to the grains.
    template<class Input>
    void shelterAndApply(STFT &output, Input input, Sample strength, Sample minimumFrequency, Sample decorrelation) {
        const Sample cutoff = minimumFrequency * CUTOFF_TIMES_MINIMUM;
        const Sample releaseStep = replacementStep * REPLACEMENT_RISE_SECONDS / REPLACEMENT_RELEASE_SECONDS;
        const Sample trebleDecorrelation = std::min(Sample(1), decorrelation * TREBLE_DECORRELATION_RATE);
        std::uniform_real_distribution<Sample> phaseDistribution(-PI, PI);
        double coherentPower = 0;
        double totalPower = 0;
        for (int b = 0; b < bands; ++b) {
            const Sample shelter = std::max(tonal[b], protection[b]);
            const Sample texture = std::max(Sample(0), mask[b] - shelter);
            const Sample target = strength * texture *
                std::clamp((output.binToFreq(b) - cutoff) / cutoff, Sample(0), Sample(1));
            const Sample amount = std::clamp(target, grainMask[b] - releaseStep, grainMask[b] + replacementStep);
            grainMask[b] = amount;
            const Sample keep = std::sqrt(Sample(1) - amount);
            for (int c = 0; c < channels; ++c) {
                output.spectrum(c)[b] *= keep;
            }
            if (decorrelation > Sample(0)) {
                grainPhase[b] = Complex{Sample(1), Sample(0)};
                Sample meanRotation = Sample(1);
                const Sample noiseConfidence = std::clamp(
                    (texture - NOISE_CONFIDENCE_START) / NOISE_CONFIDENCE_RANGE, Sample(0), Sample(1));
                const Sample trebleWeight = std::clamp(
                    (output.binToFreq(b) - Sample(2) * cutoff) / cutoff, Sample(0), Sample(1));
                const Sample phaseDose = decorrelation + (Sample(1) - decorrelation) *
                    trebleDecorrelation * noiseConfidence * trebleWeight;
                const Sample phaseAmount = phaseDose * target;
                if (phaseAmount > Sample(0)) {
                    const Sample phaseRange = PI * phaseAmount;
                    grainPhase[b] = std::polar(Sample(1), phaseDistribution(randomEngine) * phaseAmount);
                    meanRotation = std::sin(phaseRange) / phaseRange;
                }
                for (int c = 0; c < channels; ++c) {
                    const double power = double(std::norm(input(c, b))) * amount;
                    totalPower += power;
                    coherentPower += power * meanRotation * meanRotation;
                }
            }
        }
        if (decorrelation > Sample(0)) {
            const Sample windowCoherence = std::clamp(
                Sample(coherentPower / (totalPower + 1e-30)), Sample(0), Sample(1));
            grains.add(output, [&](int c, int b) { return input(c, b) * grainPhase[b]; }, grainMask, windowCoherence);
        } else {
            grains.add(output, input, grainMask);
        }
    }
};

}

#endif
