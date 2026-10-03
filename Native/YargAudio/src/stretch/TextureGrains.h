#ifndef YARG_AUDIO_STRETCH_TEXTURE_GRAINS_H
#define YARG_AUDIO_STRETCH_TEXTURE_GRAINS_H

#include <cstring>
#include "signalsmith-linear/stft.h"
#include <algorithm>
#include <cmath>
#include <complex>
#include <vector>

// TextureGrains - resynthesizes the noise portion of the spectrum from short grains.
//
// NoiseMorph selects the texture-only bands; this converts them back to audio with
// one inverse FFT, then overlaps grain slices onto the output:
//
//   - Each grain is shifted a few samples to maximise normalised cross-correlation
//     with the previous grain tail, avoiding clicks at the seam.
//   - The seam is an equal-power crossfade, scaled down at low correlation so poor
//     matches fade instead of ringing.
//
// Only texture passes through here; tones and transients stay on the main path.
namespace yarg::audio {

template<typename Sample>
class TextureGrains {
    using Complex = std::complex<Sample>;
    using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
    // Search range for grain alignment, as a share of the hop.
    static constexpr int SEARCH_DIVISOR = 4;
    static constexpr double SHIFT_PENALTY = 0.05;
    static constexpr double MIN_ALIGNMENT_CORRELATION = 0.35;
    static constexpr double MIN_HALF_CORRELATION = 0.20;
    signalsmith::linear::RealFFT<Sample, false, true> fft;
    std::vector<Complex> spectrum;
    std::vector<Sample> waveform, previous, fade, inverseWindow, output;
    Sample meanWindowPower = 0;
    int channels = 0;
    int hop = 0;
    int outputPosition = 0;
    bool fullSearch = false;

public:
    void configure(const STFT &analysis, int count, bool fullSearchGrains = false) {
        channels = count;
        fullSearch = fullSearchGrains;
        hop = int(analysis.defaultInterval());
        fft.resize(analysis.fftSamples());
        spectrum.resize(analysis.bands());
        waveform.resize(channels * analysis.fftSamples());
        previous.assign(channels * hop, Sample(0));
        output.assign(channels * analysis.blockSamples(), Sample(0));
        outputPosition = 0;
        fade.resize(hop);
        inverseWindow.resize(analysis.blockSamples());
        for (int i = 0; i < hop; ++i) {
            fade[i] = std::sin((i + Sample(0.5)) * Sample(1.5707963267948966) / hop);
        }
        double windowPower = 0;
        for (int i = 0; i < int(analysis.blockSamples()); ++i) {
            const Sample window = analysis.analysisWindow()[i];
            inverseWindow[i] = Sample(1) / (analysis.fftSamples() * window);
            windowPower += double(window) * window;
        }
        meanWindowPower = Sample(windowPower / analysis.fftSamples());
    }

    void reset() {
        std::fill(previous.begin(), previous.end(), Sample(0));
        std::fill(output.begin(), output.end(), Sample(0));
        outputPosition = 0;
    }

    // Renders texture to waveform, aligns one grain, blends it in.
    template<class Input>
    void add(const STFT &analysis, Input input, const std::vector<Sample> &mask, Sample windowCoherence = Sample(1)) {
        if (std::all_of(mask.begin(), mask.end(), [](Sample amount) { return amount == Sample(0); })) {
            const int block = int(analysis.blockSamples());
            const int start = outputPosition + int(analysis.synthesisOffset()) - hop;
            for (int c = 0; c < channels; ++c) {
                auto *tail = previous.data() + c * hop;
                auto *buffer = output.data() + c * block;
                for (int i = 0; i < hop; ++i) {
                    buffer[(start + i) % block] += tail[i] * fade[hop - 1 - i];
                    tail[i] = Sample(0);
                }
            }
            return;
        }
        renderWaveform(analysis, input, mask, windowCoherence);
        Sample correlation = 0;
        int bestOffset = findBestOffset(analysis, correlation);
        blendGrains(analysis, bestOffset, correlation);
    }

    Sample read(int channel) const {
        return output[channel * inverseWindow.size() + outputPosition];
    }

    void advance() {
        for (int c = 0; c < channels; ++c) {
            output[c * inverseWindow.size() + outputPosition] = 0;
        }
        outputPosition = (outputPosition + 1) % inverseWindow.size();
    }

private:
    // Inverse FFT of the masked spectrum per channel; compensates the analysis window.
    template<class Input>
    void renderWaveform(const STFT &analysis, Input input, const std::vector<Sample> &mask, Sample windowCoherence) {
        const int size = int(analysis.fftSamples());
        const int center = int(analysis.analysisOffset());
        const int range = hop + hop / SEARCH_DIVISOR;
        for (int c = 0; c < channels; ++c) {
            for (int b = 0; b < int(spectrum.size()); ++b) {
                spectrum[b] = input(c, b) * std::sqrt(mask[b]);
            }
            auto *wave = waveform.data() + c * size;
            fft.ifft(spectrum.data(), wave);
            for (int i = -range; i < range; ++i) {
                const int index = i < 0 ? size + i : i;
                if (windowCoherence == Sample(1)) {
                    wave[index] *= (i < 0 ? -1 : 1) * inverseWindow[center + i];
                } else {
                    const Sample window = analysis.analysisWindow()[center + i];
                    const Sample power = windowCoherence * window * window + (Sample(1) - windowCoherence) * meanWindowPower;
                    wave[index] *= (i < 0 ? -1 : 1) / (size * std::sqrt(power));
                }
            }
        }
    }

    bool hasAlignmentSupport(int size, int offset) const {
        const int middle = hop / 2;
        for (int half = 0; half < 2; ++half) {
            const int start = half == 0 ? 0 : middle;
            const int end = half == 0 ? middle : hop;
            double cross = 0;
            double oldPower = 0;
            double newPower = 0;
            for (int c = 0; c < channels; ++c) {
                const auto *wave = waveform.data() + c * size;
                const auto *tail = previous.data() + c * hop;
                for (int i = start; i < end; ++i) {
                    const double old = tail[i];
                    const double next = wave[(size - hop + offset + i) % size];
                    cross += old * next;
                    oldPower += old * old;
                    newPower += next * next;
                }
            }
            const double correlation = cross / std::sqrt(oldPower * newPower + 1e-60);
            if (correlation < MIN_HALF_CORRELATION) {
                return false;
            }
        }
        return true;
    }

    // Finds the offset with max normalised correlation against the previous tail;
    // returns the offset and writes the score.
    int findBestOffset(const STFT &analysis, Sample &correlation) {
        const int size = int(analysis.fftSamples());
        int bestOffset = 0;
        double bestScore = 0;
        double bestCorrelation = 0;
        double oldPower = 0;
        for (Sample value : previous) {
            oldPower += double(value) * value;
        }
        const int searchStep = fullSearch ? 1 : std::max(1, hop / 16);
        const int searchRange = hop / SEARCH_DIVISOR;
        const int searchCount = 2 * searchRange / searchStep + 1;
        const double shiftPenalty = SHIFT_PENALTY * SEARCH_DIVISOR / double(hop);
        for (int candidate = -1; candidate < searchCount; ++candidate) {
            const int offset = candidate < 0 ? 0 : -searchRange + candidate * searchStep;
            double cross = 0;
            double newPower = 0;
            for (int c = 0; c < channels; ++c) {
                const auto *wave = waveform.data() + c * size;
                const auto *tail = previous.data() + c * hop;
                for (int i = 0; i < hop; ++i) {
                    const int index = (size - hop + offset + i) % size;
                    const double next = wave[index];
                    cross += tail[i] * next;
                    newPower += next * next;
                }
            }
            const double match = cross / std::sqrt(oldPower * newPower + 1e-60);
            const double score = match - shiftPenalty * std::abs(offset);
            if (candidate < 0 || score > bestScore || (score == bestScore && std::abs(offset) < std::abs(bestOffset))) {
                if (offset != 0 && (match < MIN_ALIGNMENT_CORRELATION || !hasAlignmentSupport(size, offset))) {
                    continue;
                }
                bestScore = score;
                bestCorrelation = match;
                bestOffset = offset;
            }
        }
        correlation = Sample(std::clamp(bestCorrelation, 0.0, 1.0));
        return bestOffset;
    }

    // Equal-power overlap-add of the aligned grain; poor matches blend quietly.
    void blendGrains(const STFT &analysis, int bestOffset, Sample correlation) {
        const int size = int(analysis.fftSamples());
        const int block = int(analysis.blockSamples());
        const int start = outputPosition + int(analysis.synthesisOffset()) - hop;
        for (int c = 0; c < channels; ++c) {
            const auto *wave = waveform.data() + c * size;
            auto *tail = previous.data() + c * hop;
            auto *buffer = output.data() + c * block;
            for (int i = 0; i < hop; ++i) {
                const Sample rise = fade[i];
                const Sample fall = fade[hop - 1 - i];
                const Sample next = wave[(size - hop + bestOffset + i) % size];
                buffer[(start + i) % block] += (tail[i] * fall + next * rise) /
                    std::sqrt(Sample(1) + 2 * correlation * rise * fall);
                tail[i] = wave[(size + bestOffset + i) % size];
            }
        }
    }
};

}

#endif
