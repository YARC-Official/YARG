#ifndef YARG_AUDIO_STRETCH_ENVELOPE_EQ_H
#define YARG_AUDIO_STRETCH_ENVELOPE_EQ_H

#include "signalsmith-linear/stft.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <vector>

namespace yarg::audio {

template<typename Sample>
class EnvelopeEq {
    using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
    using Power = std::array<double, 3>;
    struct Reference {
        Power power{};
        bool active = false;
    };
    static constexpr double PI = 3.14159265358979323846;
    static constexpr double POWER_SECONDS = 0.250;
    static constexpr double GAIN_SECONDS = 0.150;
    static constexpr double MIN_GAIN = 0.7943282347242815;
    static constexpr double MAX_GAIN = 1.1220184543019633;
    static constexpr double POWER_FLOOR = 1e-12;
    static constexpr double LOW_CROSSOVER_HZ = 2000;
    static constexpr double HIGH_CROSSOVER_HZ = 6000;
    static constexpr double WEAK_BAND_POWER_RATIO = 0.001;
    std::vector<Power> weights;
    std::vector<std::array<Sample, 2>> filters;
    std::vector<Reference> references;
    std::array<Sample, 2> poles{};
    std::array<Sample, 3> gains{Sample(1), Sample(1), Sample(1)};
    std::array<Sample, 3> targets{Sample(1), Sample(1), Sample(1)};
    Power sourcePower{}, outputPower{}, samplePower{};
    double powerRate = 0;
    Sample gainRate = 0;
    int position = 0;
    int delay = 0;
    int hop = 0;
    int warmup = 0;
    int activeSamples = 0;
    int updateSamples = 0;

public:
    void configure(const STFT &analysis, int channels, Sample sampleRate) {
        hop = int(analysis.defaultInterval());
        delay = int(analysis.synthesisLatency());
        warmup = int(sampleRate * POWER_SECONDS);
        powerRate = -std::expm1(-1 / (sampleRate * POWER_SECONDS));
        gainRate = Sample(-std::expm1(-1 / (sampleRate * GAIN_SECONDS)));
        poles[0] = Sample(std::exp(-2 * PI * std::min(double(sampleRate) * 0.2, LOW_CROSSOVER_HZ) / sampleRate));
        poles[1] = Sample(std::exp(-2 * PI * std::min(double(sampleRate) * 0.4, HIGH_CROSSOVER_HZ) / sampleRate));
        filters.resize(channels);
        references.resize(delay + hop + 1);
        weights.resize(analysis.bands());
        double windowPower = 0;
        for (int i = 0; i < int(analysis.blockSamples()); ++i) {
            const double window = analysis.analysisWindow()[i];
            windowPower += window * window;
        }
        const double scale = 2 / (analysis.fftSamples() * windowPower);
        for (int b = 0; b < int(weights.size()); ++b) {
            const auto rotation = std::polar(1.0, -2 * PI * analysis.binToFreq(Sample(b)));
            const auto low = (1 - double(poles[0])) / (1.0 - double(poles[0]) * rotation);
            const auto upper = (1 - double(poles[1])) / (1.0 - double(poles[1]) * rotation);
            weights[b] = {std::norm(low) * scale, std::norm(upper - low) * scale,
                std::norm(1.0 - upper) * scale};
        }
        reset();
    }

    void reset() {
        std::fill(filters.begin(), filters.end(), std::array<Sample, 2>{});
        std::fill(references.begin(), references.end(), Reference{});
        sourcePower = outputPower = samplePower = {};
        gains.fill(Sample(1));
        targets.fill(Sample(1));
        updateSamples = hop;
        position = activeSamples = 0;
    }

    template<class Input>
    void setReference(Input input, bool active) {
        Reference reference;
        reference.active = active;
        if (active) {
            for (int c = 0; c < int(filters.size()); ++c) {
                for (int b = 0; b < int(weights.size()); ++b) {
                    const double power = std::norm(input(c, b));
                    for (int band = 0; band < 3; ++band) {
                        reference.power[band] += power * weights[b][band];
                    }
                }
            }
        }
        for (int i = 0; i < hop; ++i) {
            references[(position + delay + i) % references.size()] = reference;
        }
    }

    Sample filter(int channel, Sample value) {
        auto &state = filters[channel];
        state[0] += (Sample(1) - poles[0]) * (value - state[0]);
        state[1] += (Sample(1) - poles[1]) * (value - state[1]);
        const std::array<Sample, 3> bands{state[0], state[1] - state[0], value - state[1]};
        Sample result = value;
        for (int band = 0; band < 3; ++band) {
            samplePower[band] += double(bands[band]) * bands[band];
            result += (gains[band] - Sample(1)) * bands[band];
        }
        return result;
    }

    void advance(bool adapt = true) {
        const auto &reference = references[position];
        if (reference.active) {
            activeSamples = std::min(warmup, activeSamples + 1);
        } else {
            activeSamples = 0;
        }
        const bool update = --updateSamples == 0;
        if (update) {
            updateSamples = hop;
        }
        const double regularization = (sourcePower[0] + sourcePower[1] + sourcePower[2]) *
            WEAK_BAND_POWER_RATIO + POWER_FLOOR;
        for (int band = 0; band < 3; ++band) {
            sourcePower[band] += powerRate * (reference.power[band] - sourcePower[band]);
            outputPower[band] += powerRate * (samplePower[band] - outputPower[band]);
            if (adapt) {
                if (!reference.active || activeSamples < warmup) {
                    targets[band] = Sample(1);
                } else if (update) {
                    targets[band] = Sample(std::clamp(std::sqrt((sourcePower[band] + regularization) /
                        (outputPower[band] + regularization)), MIN_GAIN, MAX_GAIN));
                }
                gains[band] += gainRate * (targets[band] - gains[band]);
            }
        }
        samplePower = {};
        references[position] = {};
        position = (position + 1) % references.size();
    }
};

}

#endif
