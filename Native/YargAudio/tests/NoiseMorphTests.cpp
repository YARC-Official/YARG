#include "stretch/NoiseMorph.h"
#include "Test.h"

#include <random>

namespace {

using STFT = signalsmith::linear::DynamicSTFT<float, false, true>;
constexpr double PI = 3.14159265358979323846;
constexpr int FRAMES = 100;
constexpr int WARMUP_FRAMES = 30;

enum class Signal { TONE, VIBRATO, NOISE };

double replacedPower(Signal signal, int channels, int sampleRate, float strength, float stretchFactor) {
    const int block = int(std::ceil(sampleRate * 0.060 / 512)) * 512;
    const int hop = block / 8;
    STFT analysis;
    analysis.configure(channels, channels, block);
    analysis.setInterval(hop, analysis.kaiser);
    for (int i = 0; i < block; ++i) {
        const double position = (i - int(analysis.analysisOffset())) / (block / 8.0);
        analysis.analysisWindow()[i] = float(std::exp(-0.5 * position * position));
    }
    yarg::audio::NoiseMorph<float> noise;
    noise.configure(analysis, channels, float(sampleRate), true);
    std::vector<float> waveform(analysis.fftSamples());
    std::vector<std::complex<float>> source(analysis.bands());
    std::minstd_rand random(77);
    std::normal_distribution<float> normal;
    double phase = 0;
    double sourcePower = 0;
    double outputPower = 0;
    auto input = [&](int channel, int band) {
        return source[band] * (channel == 0 ? 1.0f : -0.5f);
    };
    for (int frame = 0; frame < FRAMES; ++frame) {
        const double frequency = signal == Signal::VIBRATO ?
            5000 + 120 * std::sin(frame * 0.21) : 5000;
        for (int i = 0; i < block; ++i) {
            phase += 2 * PI * frequency / sampleRate;
            const float value = signal == Signal::NOISE ? normal(random) : float(std::sin(phase));
            waveform[i] = value * analysis.analysisWindow()[i];
        }
        std::fill(waveform.begin() + block, waveform.end(), 0.0f);
        analysis.fft.fft(waveform.data(), source.data());
        for (int channel = 0; channel < channels; ++channel) {
            for (int band = 0; band < int(source.size()); ++band) {
                analysis.spectrum(channel)[band] = input(channel, band);
            }
        }
        noise.apply(analysis, input, strength, 1500.0f / sampleRate, stretchFactor);
        for (int channel = 0; channel < channels; ++channel) {
            for (int band = 0; band < int(source.size()); ++band) {
                const auto original = input(channel, band);
                const auto kept = analysis.spectrum(channel)[band];
                REQUIRE(std::norm(kept) >= std::norm(original) * 0.749999f);
                if (analysis.binToFreq(float(band)) * sampleRate <= 4500) {
                    REQUIRE(kept == original);
                }
            }
        }
        if (frame >= WARMUP_FRAMES) {
            for (int channel = 0; channel < channels; ++channel) {
                for (int band = 0; band < int(source.size()); ++band) {
                    sourcePower += std::norm(input(channel, band));
                    outputPower += std::norm(analysis.spectrum(channel)[band]);
                }
            }
        }
        for (int i = 0; i < hop; ++i) {
            const float left = noise.readGrain(0);
            REQUIRE(std::isfinite(left));
            if (channels == 2) {
                REQUIRE(std::abs(noise.readGrain(1) + 0.5f * left) < 1e-6f);
            }
            noise.advanceGrain();
        }
    }
    noise.reset();
    for (int i = 0; i < block; ++i) {
        for (int channel = 0; channel < channels; ++channel) {
            REQUIRE(noise.readGrain(channel) == 0);
        }
        noise.advanceGrain();
    }
    return 1 - outputPower / sourcePower;
}

void maskRiseKeepsNoiseBroadband(int channels, int sampleRate, double smoothingSeconds, double envelopeRadius) {
    const int block = int(std::ceil(sampleRate * 0.060 / 512)) * 512;
    const int hop = block / 8;
    STFT analysis;
    analysis.configure(channels, channels, block);
    analysis.setInterval(hop, analysis.kaiser);
    yarg::audio::TextureGrains<float> grains;
    grains.configure(analysis, channels, float(sampleRate));
    std::vector<float> mask(analysis.bands(), 1.0f);
    std::vector<float> samples(FRAMES * hop);
    const int tone = int(analysis.freqToBin(6000.0f / sampleRate));
    for (int frame = 0; frame < FRAMES; ++frame) {
        mask[tone] = frame < WARMUP_FRAMES ? 1e-8f : 0.01f;
        grains.add(analysis, [](int channel, int) {
            return std::complex<float>(channel == 0 ? 1.0f : -0.5f, 0);
        }, mask, smoothingSeconds, envelopeRadius);
        for (int i = 0; i < hop; ++i) {
            const float left = grains.read(0);
            REQUIRE(std::isfinite(left));
            samples[frame * hop + i] = left;
            if (channels == 2) {
                REQUIRE(std::abs(grains.read(1) + 0.5f * left) < 1e-6f);
            }
            grains.advance();
        }
    }
    std::vector<float> windowed(analysis.fftSamples(), 0);
    std::vector<std::complex<float>> spectrum(analysis.bands());
    for (int frame : {31, 33, 40, 50}) {
        for (int i = 0; i < block; ++i) {
            const double window = 0.5 - 0.5 * std::cos(2 * PI * i / (block - 1));
            windowed[i] = float(samples[frame * hop + i] * window);
        }
        analysis.fft.fft(windowed.data(), spectrum.data());
        double total = 0;
        double narrow = 0;
        for (int band = 0; band < int(spectrum.size()); ++band) {
            const double power = std::norm(spectrum[band]);
            total += power;
            const float frequency = analysis.binToFreq(float(band)) * sampleRate;
            if (std::abs(frequency - 6000) < 200) {
                narrow += power;
            }
        }
        REQUIRE(narrow / total < 0.10);
    }
    std::fill(mask.begin(), mask.end(), 0.0f);
    for (int frame = 0; frame < 24; ++frame) {
        grains.add(analysis, [](int, int) { return std::complex<float>(1, 0); }, mask, smoothingSeconds, envelopeRadius);
        for (int i = 0; i < hop; ++i) {
            if (frame >= 16) {
                for (int channel = 0; channel < channels; ++channel) {
                    REQUIRE(grains.read(channel) == 0);
                }
            }
            grains.advance();
        }
    }
}

struct TextureMeasurements {
    double ripple;
    double power;
};

TextureMeasurements measureTexture(double envelopeRadius) {
    constexpr int BLOCK = 3072;
    constexpr int HOP = BLOCK / 8;
    constexpr int RENDER_FRAMES = 400;
    constexpr double RIPPLE_BINS = 16;
    STFT analysis;
    analysis.configure(2, 2, BLOCK);
    analysis.setInterval(HOP, analysis.kaiser);
    yarg::audio::TextureGrains<float> grains;
    grains.configure(analysis, 2, 48000.0f);
    std::vector<float> mask(analysis.bands(), 1.0f);
    std::vector<float> samples(RENDER_FRAMES * HOP);
    auto input = [](int channel, int band) {
        const double power = 1 + 0.8 * std::cos(2 * PI * band / RIPPLE_BINS);
        return std::complex<float>(float(std::sqrt(power)) * (channel == 0 ? 1.0f : -0.5f), 0);
    };
    for (int frame = 0; frame < RENDER_FRAMES; ++frame) {
        grains.add(analysis, input, mask, 0.017142857, envelopeRadius);
        for (int i = 0; i < HOP; ++i) {
            const float left = grains.read(0);
            REQUIRE(std::abs(grains.read(1) + 0.5f * left) < 1e-6f);
            samples[frame * HOP + i] = left;
            grains.advance();
        }
    }
    std::vector<float> windowed(analysis.fftSamples(), 0);
    std::vector<std::complex<float>> spectrum(analysis.bands());
    double total = 0;
    double ripple = 0;
    double power = 0;
    for (int frame = 32; frame < RENDER_FRAMES - 8; frame += 8) {
        for (int i = 0; i < BLOCK; ++i) {
            const double window = 0.5 - 0.5 * std::cos(2 * PI * i / (BLOCK - 1));
            windowed[i] = float(samples[frame * HOP + i] * window);
            power += windowed[i] * windowed[i];
        }
        analysis.fft.fft(windowed.data(), spectrum.data());
        for (int band = 256; band < 768; ++band) {
            const double bandPower = std::norm(spectrum[band]);
            total += bandPower;
            ripple += bandPower * std::cos(2 * PI * band / RIPPLE_BINS);
        }
    }
    return {2 * ripple / total, power};
}

}

void runNoiseMorphTests() {
    for (int sampleRate : {24000, 44100, 48000, 96000}) {
        for (int channels : {1, 2}) {
            for (float stretchFactor : {2.0f, 1.0f / 0.35f, 4.0f}) {
                REQUIRE(replacedPower(Signal::TONE, channels, sampleRate, 1.0f, stretchFactor) < 0.01);
                REQUIRE(replacedPower(Signal::VIBRATO, channels, sampleRate, 1.0f, stretchFactor) < 0.01);
                const auto replacement = replacedPower(Signal::NOISE, channels, sampleRate, 1.0f, stretchFactor);
                REQUIRE(replacement > 0.025);
                REQUIRE(replacement <= 0.25);
                REQUIRE(std::abs(replacedPower(Signal::NOISE, channels, sampleRate, 0.0f, stretchFactor)) < 1e-6);
            }
            for (double smoothingSeconds : {0.0, 0.017142857, 0.040}) {
                for (double envelopeRadius : {2.0, 2.5, 3.0, 3.5, 4.0}) {
                    maskRiseKeepsNoiseBroadband(channels, sampleRate, smoothingSeconds, envelopeRadius);
                }
            }
        }
    }
    const auto narrow = measureTexture(2);
    const auto middle = measureTexture(3);
    const auto wide = measureTexture(4);
    REQUIRE(wide.ripple < narrow.ripple * 0.85);
    REQUIRE(middle.ripple < narrow.ripple);
    REQUIRE(middle.ripple > wide.ripple);
    REQUIRE(std::abs(wide.power / narrow.power - 1) < 0.01);
    REQUIRE(std::abs(middle.power / narrow.power - 1) < 0.01);
}
