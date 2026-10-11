#include "stretch/YargStretch.h"
#include "Test.h"

namespace {

constexpr double PI = 3.14159265358979323846;
constexpr int BLOCK_FRAMES = 256;
constexpr int TEST_FRAMES = 1800;
constexpr int WARMUP_FRAMES = 600;

struct Partial {
    double frequency;
    double amplitude;
};

struct RenderSettings {
    int sampleRate = 48000;
    int channels = 1;
    double speed = 0.35;
    float pitch = 1;
    bool split = false;
};

std::vector<float> render(const std::vector<Partial>& partials, const RenderSettings& settings) {
    const int window = int(std::ceil(settings.sampleRate * 0.060 / 512)) * 512;
    yarg::audio::YargStretch<float> stretch(1);
    stretch.configure(settings.channels, float(settings.sampleRate), window, window / 8, settings.split);
    stretch.setTransientFrequency(1500.0f / settings.sampleRate);
    stretch.setTransposeFactor(settings.pitch);
    std::vector<std::vector<float>> input(settings.channels, std::vector<float>(2 * BLOCK_FRAMES));
    std::vector<std::vector<float>> output(settings.channels, std::vector<float>(BLOCK_FRAMES));
    std::vector<const float*> inputChannels;
    std::vector<float*> outputChannels;
    for (int channel = 0; channel < settings.channels; ++channel) {
        inputChannels.push_back(input[channel].data());
        outputChannels.push_back(output[channel].data());
    }
    std::vector<float> samples;
    samples.reserve((TEST_FRAMES - WARMUP_FRAMES) * BLOCK_FRAMES);
    int sourcePosition = 0;
    double remainder = 0;
    for (int frame = 0; frame < TEST_FRAMES; ++frame) {
        remainder += BLOCK_FRAMES * settings.speed;
        const int count = int(remainder);
        remainder -= count;
        for (int i = 0; i < count; ++i) {
            double value = 0;
            for (const auto& partial : partials) {
                value += partial.amplitude * std::sin(2 * PI * partial.frequency *
                    (sourcePosition + i) / settings.sampleRate);
            }
            for (int channel = 0; channel < settings.channels; ++channel) {
                input[channel][i] = float(value) * (channel == 0 ? 1.0f : -0.5f);
            }
        }
        sourcePosition += count;
        stretch.process(inputChannels, count, outputChannels, BLOCK_FRAMES);
        for (int i = 0; i < BLOCK_FRAMES; ++i) {
            REQUIRE(std::isfinite(output[0][i]));
            REQUIRE(std::abs(output[0][i]) < 2);
            if (settings.channels == 2) {
                REQUIRE(std::abs(output[1][i] + 0.5f * output[0][i]) < 1e-5f);
            }
        }
        if (frame >= WARMUP_FRAMES) {
            samples.insert(samples.end(), output[0].begin(), output[0].end());
        }
    }
    return samples;
}

double coherentPower(const std::vector<float>& samples, double frequency, int sampleRate) {
    const auto rotation = std::polar(1.0, -2 * PI * frequency / sampleRate);
    std::complex<double> phase = 1;
    std::complex<double> sum = 0;
    for (float sample : samples) {
        sum += double(sample) * phase;
        phase *= rotation;
    }
    const double amplitude = 2 * std::abs(sum) / samples.size();
    return amplitude * amplitude / 2;
}

double totalPower(const std::vector<float>& samples) {
    double power = 0;
    for (float sample : samples) {
        power += double(sample) * sample;
    }
    return power / samples.size();
}

void overlappingHarmonicsKeepTheirFrequencies() {
    std::vector<Partial> partials;
    for (int harmonic = 1; harmonic <= 28; ++harmonic) {
        const double frequency = 113.7 * harmonic;
        const double formant = 0.15 + std::exp(-std::pow((frequency - 900) / 400, 2)) +
            0.7 * std::exp(-std::pow((frequency - 2200) / 500, 2));
        partials.push_back({frequency, 0.1 * formant / std::pow(harmonic, 0.65)});
    }
    for (int harmonic = 1; harmonic <= 24; ++harmonic) {
        partials.push_back({146.2 * harmonic, 0.1 / harmonic});
        partials.push_back({196.5 * harmonic, 0.07 / std::pow(harmonic, 1.3)});
    }
    for (int sampleRate : {44100, 48000}) {
        for (int channels : {1, 2}) {
            RenderSettings settings;
            settings.sampleRate = sampleRate;
            settings.channels = channels;
            const auto samples = render(partials, settings);
            double coherent = 0;
            for (const auto& partial : partials) {
                coherent += coherentPower(samples, partial.frequency, sampleRate);
            }
            REQUIRE(coherent / totalPower(samples) > 0.30);
        }
    }
}

void tonesKeepPitchAcrossPlaybackModes() {
    const std::vector<Partial> tone{{997.3, 0.3}};
    for (double speed : {0.1, 0.35, 0.5, 1.0, 2.0}) {
        RenderSettings settings;
        settings.speed = speed;
        const auto samples = render(tone, settings);
        REQUIRE(coherentPower(samples, tone[0].frequency, settings.sampleRate) / totalPower(samples) > 0.95);
    }
    for (float pitch : {0.5f, 2.0f}) {
        RenderSettings settings;
        settings.pitch = pitch;
        settings.channels = 2;
        const auto samples = render(tone, settings);
        REQUIRE(coherentPower(samples, tone[0].frequency * pitch, settings.sampleRate) / totalPower(samples) > 0.95);
    }
    RenderSettings settings;
    settings.split = true;
    const auto samples = render(tone, settings);
    REQUIRE(coherentPower(samples, tone[0].frequency, settings.sampleRate) / totalPower(samples) > 0.95);
}

void unitySpeedCopiesInputExactly() {
    constexpr int WINDOW = 3072;
    constexpr int BLOCK = 256;
    constexpr int PIPELINE_DELAY = WINDOW;
    constexpr int FRAMES = 256;
    yarg::audio::YargStretch<float> stretch(1);
    stretch.configure(1, 48000, WINDOW, WINDOW / 8);
    std::vector<float> input(BLOCK);
    std::vector<float> output(BLOCK);
    const float* inputs[] = {input.data()};
    float* outputs[] = {output.data()};
    std::vector<float> source;
    std::vector<float> rendered;
    source.reserve(FRAMES * BLOCK);
    rendered.reserve(FRAMES * BLOCK);
    for (int frame = 0; frame < FRAMES; ++frame) {
        for (int i = 0; i < BLOCK; ++i) {
            const int position = frame * BLOCK + i;
            input[i] = float(0.2 * std::sin(0.213 * position) +
                0.1 * std::sin(0.479 * position) + 0.03 * std::sin(0.791 * position));
            source.push_back(input[i]);
        }
        stretch.process(inputs, BLOCK, outputs, BLOCK);
        rendered.insert(rendered.end(), output.begin(), output.end());
    }
    for (int i = 2 * PIPELINE_DELAY; i < int(rendered.size()); ++i) {
        REQUIRE(std::abs(rendered[i] - source[i - PIPELINE_DELAY]) < 1e-5f);
    }
}

void attacksResetAndSilenceDrains() {
    constexpr int SAMPLE_RATE = 48000;
    constexpr int WINDOW = 3072;
    constexpr int INPUT_FRAMES = 90;
    yarg::audio::YargStretch<float> stretch(1);
    stretch.configure(2, float(SAMPLE_RATE), WINDOW, WINDOW / 8);
    stretch.setTransientFrequency(1500.0f / SAMPLE_RATE);
    std::vector<float> left(INPUT_FRAMES);
    std::vector<float> right(INPUT_FRAMES);
    std::vector<float> outputLeft(WINDOW);
    std::vector<float> outputRight(WINDOW);
    const float* inputs[] = {left.data(), right.data()};
    float* outputs[] = {outputLeft.data(), outputRight.data()};
    int position = 0;
    for (int frame = 0; frame < 900; ++frame) {
        for (int i = 0; i < INPUT_FRAMES; ++i) {
            const int beat = position % (SAMPLE_RATE / 2);
            const float burst = beat < SAMPLE_RATE / 10 ?
                float(0.3 * std::exp(-double(beat) / 1200) * std::sin(2 * PI * 3400 * position / SAMPLE_RATE)) : 0;
            const float value = burst + float(0.005 * std::sin(2 * PI * 733 * position / SAMPLE_RATE));
            left[i] = value;
            right[i] = -0.5f * value;
            ++position;
        }
        stretch.process(inputs, INPUT_FRAMES, outputs, BLOCK_FRAMES);
        for (int i = 0; i < BLOCK_FRAMES; ++i) {
            REQUIRE(std::isfinite(outputLeft[i]));
            REQUIRE(std::abs(outputLeft[i]) < 1);
            REQUIRE(std::abs(outputRight[i] + 0.5f * outputLeft[i]) < 1e-5f);
        }
    }
    REQUIRE(stretch.transientCount() > 0);
    stretch.flush(outputs, WINDOW, float(INPUT_FRAMES) / BLOCK_FRAMES);
    for (int i = 0; i < WINDOW; ++i) {
        REQUIRE(std::isfinite(outputLeft[i]));
        REQUIRE(std::abs(outputRight[i] + 0.5f * outputLeft[i]) < 1e-5f);
    }
    stretch.reset();
    std::fill(left.begin(), left.end(), 0);
    std::fill(right.begin(), right.end(), 0);
    for (int frame = 0; frame < 64; ++frame) {
        stretch.process(inputs, INPUT_FRAMES, outputs, BLOCK_FRAMES);
        for (int i = 0; i < BLOCK_FRAMES; ++i) {
            REQUIRE(std::isfinite(outputLeft[i]));
            REQUIRE(std::abs(outputLeft[i]) < 1e-6f);
            REQUIRE(std::abs(outputRight[i]) < 1e-6f);
        }
    }
}

}

void runYargStretchTests() {
    overlappingHarmonicsKeepTheirFrequencies();
    tonesKeepPitchAcrossPlaybackModes();
    unitySpeedCopiesInputExactly();
    attacksResetAndSilenceDrains();
}
