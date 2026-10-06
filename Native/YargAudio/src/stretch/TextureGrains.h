#ifndef YARG_AUDIO_STRETCH_TEXTURE_GRAINS_H
#define YARG_AUDIO_STRETCH_TEXTURE_GRAINS_H

#include <cstring>
#include "signalsmith-linear/stft.h"
#include <algorithm>
#include <cmath>
#include <complex>
#include <random>
#include <vector>

// TextureGrains - resynthesizes the noise portion of the spectrum via noise
// morphing (Moliner, Fierro, Wright & Valimaki 2024):
//
//   - envelope from input magnitude shaped by the texture mask
//   - excitation: shared random rotation per band times each channel's own
//     input phase, so blocks stay decorrelated over time while interchannel
//     relationships (and the stereo image) are preserved
//   - one inverse FFT, equal-power crossfade overlap-add between blocks
//
// Only texture passes through here; tones and transients stay on the main path.
// Blocks are independent by construction: no alignment search, nothing tonal
// to re-correlate.
namespace yarg::audio {

template<typename Sample>
class TextureGrains {
    using Complex = std::complex<Sample>;
    using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
    static constexpr unsigned DEFAULT_SEED = 1;
    static constexpr double PI = 3.14159265358979323846;
    signalsmith::linear::RealFFT<Sample, false, true> fft;
    std::vector<Complex> spectrum;
    std::vector<Sample> excitation;
    std::vector<Sample> waveform, previous, fade, output;
    std::minstd_rand randomEngine{DEFAULT_SEED};
    Sample normalisation = 1;
    int channels = 0;
    int hop = 0;
    int block = 0;
    int outputPosition = 0;

public:
    void configure(const STFT &analysis, int count) {
        channels = count;
        hop = int(analysis.defaultInterval());
        block = int(analysis.blockSamples());
        fft.resize(analysis.fftSamples());
        spectrum.resize(analysis.bands());
        excitation.resize(analysis.bands());
        waveform.resize(channels * analysis.fftSamples());
        previous.assign(channels * hop, Sample(0));
        output.assign(channels * analysis.blockSamples(), Sample(0));
        outputPosition = 0;
        fade.resize(hop);
        for (int i = 0; i < hop; ++i) {
            fade[i] = std::sin((i + Sample(0.5)) * Sample(PI / 2) / hop);
        }
        double windowPower = 0;
        for (int i = 0; i < int(analysis.blockSamples()); ++i) {
            const double window = analysis.analysisWindow()[i];
            windowPower += window * window;
        }
        normalisation = Sample(1 / std::sqrt(double(analysis.fftSamples()) * windowPower));
        randomEngine.seed(DEFAULT_SEED);
    }

    void reset() {
        std::fill(previous.begin(), previous.end(), Sample(0));
        std::fill(output.begin(), output.end(), Sample(0));
        outputPosition = 0;
        randomEngine.seed(DEFAULT_SEED);
    }

    // Renders texture to waveform, crossfades one block, blends it in.
    template<class Input>
    void add(const STFT &analysis, Input input, const std::vector<Sample> &mask) {
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
        renderWaveform(analysis, input, mask);
        blendBlocks(analysis);
    }

    Sample read(int channel) const {
        return output[channel * block + outputPosition];
    }

    void advance() {
        for (int c = 0; c < channels; ++c) {
            output[c * block + outputPosition] = 0;
        }
        outputPosition = (outputPosition + 1) % block;
    }

private:
    // Envelope from input magnitude; shared random phase keeps stereo scaling exact.
    template<class Input>
    void renderWaveform(const STFT &analysis, Input input, const std::vector<Sample> &mask) {
        const int size = int(analysis.fftSamples());
        std::uniform_real_distribution<Sample> phaseDistribution(Sample(-PI), Sample(PI));
        for (int b = 0; b < int(spectrum.size()); ++b) {
            excitation[b] = phaseDistribution(randomEngine);
        }
        for (int c = 0; c < channels; ++c) {
            for (int b = 0; b < int(spectrum.size()); ++b) {
                const Complex source = input(c, b);
                const Sample power = std::norm(source);
                const Complex unit = power > Sample(1e-30) ?
                    source / std::sqrt(power) : Complex{Sample(1), Sample(0)};
                spectrum[b] = std::polar(std::sqrt(power * mask[b]) * normalisation,
                    excitation[b]) * unit;
            }
            fft.ifft(spectrum.data(), waveform.data() + c * size);
        }
    }

    // Equal-power overlap-add of the new block over the previous tail.
    void blendBlocks(const STFT &analysis) {
        const int size = int(analysis.fftSamples());
        const int block = int(analysis.blockSamples());
        const int start = outputPosition + int(analysis.synthesisOffset()) - hop;
        for (int c = 0; c < channels; ++c) {
            const auto *wave = waveform.data() + c * size;
            auto *tail = previous.data() + c * hop;
            auto *buffer = output.data() + c * block;
            for (int i = 0; i < hop; ++i) {
                const Sample next = -wave[(size - hop + i) % size];
                buffer[(start + i) % block] += tail[i] * fade[hop - 1 - i] + next * fade[i];
                tail[i] = wave[i];
            }
        }
    }
};

}

#endif
