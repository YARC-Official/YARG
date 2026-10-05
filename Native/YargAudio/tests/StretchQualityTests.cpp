#include "stretch/YargStretch.h"
#include "Test.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <random>
#include <vector>

namespace {

constexpr int RATE = 44100;
constexpr int WINDOW = 3072;
constexpr int HOP = 384;
constexpr int OUTPUT_SAMPLES = RATE * 2;
constexpr double PI = 3.14159265358979323846;
using Stretch = yarg::audio::YargStretch<float>;
using Stereo = std::array<std::vector<float>, 2>;

Stereo render(Stretch &stretch, const std::vector<float> &source, double speed,
    float pitch, float rightGain) {
    stretch.reset(0);
    stretch.setTransposeFactor(pitch);
    std::vector<float> right(source.size());
    for (int i = 0; i < int(source.size()); ++i) {
        right[i] = source[i] * rightGain;
    }
    const float *inputs[] = {source.data(), right.data()};
    int position = stretch.inputLatency();
    stretch.seek(inputs, position, speed);
    Stereo output{std::vector<float>(OUTPUT_SAMPLES), std::vector<float>(OUTPUT_SAMPLES)};
    std::array<float, HOP> leftBlock{}, rightBlock{};
    float *outputs[] = {leftBlock.data(), rightBlock.data()};
    double remainder = 0;
    for (int frame = 0; frame < OUTPUT_SAMPLES; frame += HOP) {
        const double exact = speed * HOP + remainder;
        const int count = int(exact);
        remainder = exact - count;
        REQUIRE(position + count <= int(source.size()));
        inputs[0] = source.data() + position;
        inputs[1] = right.data() + position;
        stretch.process(inputs, count, outputs, HOP);
        position += count;
        const int copied = std::min(HOP, OUTPUT_SAMPLES - frame);
        std::copy_n(leftBlock.begin(), copied, output[0].begin() + frame);
        std::copy_n(rightBlock.begin(), copied, output[1].begin() + frame);
    }
    return output;
}

double power(const std::vector<float> &signal) {
    double result = 0;
    for (int i = RATE; i < OUTPUT_SAMPLES; ++i) {
        REQUIRE(std::isfinite(signal[i]));
        result += double(signal[i]) * signal[i];
    }
    return result / RATE;
}

double tonePower(const std::vector<float> &signal, double frequency) {
    const auto rotation = std::polar(1.0, -2 * PI * frequency / RATE);
    std::complex<double> phase{1, 0};
    std::complex<double> sum{};
    for (int i = RATE; i < OUTPUT_SAMPLES; ++i) {
        sum += double(signal[i]) * phase;
        phase *= rotation;
    }
    return 2 * std::norm(sum / double(RATE));
}

void requireStereo(const Stereo &output, float rightGain) {
    double error = 0;
    double energy = 0;
    for (int i = 0; i < OUTPUT_SAMPLES; ++i) {
        REQUIRE(std::isfinite(output[0][i]));
        REQUIRE(std::isfinite(output[1][i]));
        const double difference = output[1][i] - double(rightGain) * output[0][i];
        error += difference * difference;
        energy += double(output[0][i]) * output[0][i];
    }
    REQUIRE(error < energy * 1e-10 + 1e-20);
}

}

void runStretchQualityTests() {
    Stretch stretch(0);
    stretch.configure(2, RATE, WINDOW, HOP);
    stretch.setTransientFrequency(1500.0f / RATE);
    std::vector<float> harmonics(OUTPUT_SAMPLES * 2 + WINDOW + HOP);
    std::vector<float> vibrato(harmonics.size());
    std::vector<float> sine(harmonics.size());
    std::vector<float> noise(harmonics.size());
    std::minstd_rand random(1);
    std::normal_distribution<float> distribution(0, 0.1f);
    double phase = 0;
    for (int i = 0; i < int(harmonics.size()); ++i) {
        for (int harmonic = 1; harmonic <= 4; ++harmonic) {
            harmonics[i] += float(0.1 * std::sin(2 * PI * 220 * harmonic * i / RATE + 0.3 * harmonic * harmonic));
        }
        phase += 2 * PI * (3000 + 25 * std::sin(2 * PI * 5 * i / RATE)) / RATE;
        vibrato[i] = float(0.2 * std::sin(phase));
        sine[i] = float(0.2 * std::sin(2 * PI * 440 * i / RATE));
        noise[i] = distribution(random);
    }
    for (double speed : {0.1, 0.25, 0.5, 0.75, 1.0, 2.0}) {
        auto output = render(stretch, harmonics, speed, 1, 0.1f);
        double harmonicPower = 0;
        for (int harmonic = 1; harmonic <= 4; ++harmonic) {
            harmonicPower += tonePower(output[0], 220 * harmonic);
        }
        REQUIRE(harmonicPower / power(output[0]) > 0.995);
        requireStereo(output, 0.1f);
        output = render(stretch, vibrato, speed, 1, 0.1f);
        REQUIRE(std::abs(power(output[0]) / 0.02 - 1) < 0.04);
        requireStereo(output, 0.1f);
    }
    for (float gain : {0.0f, 0.1f, 1.0f, -1.0f, 10.0f}) {
        const auto output = render(stretch, noise, 0.1, 1, gain);
        requireStereo(output, gain);
        REQUIRE(power(output[0]) > 0.003);
        REQUIRE(power(output[0]) < 0.02);
    }
    for (float pitch : {0.75f, 1.25f}) {
        const auto output = render(stretch, sine, 0.5, pitch, 0.1f);
        REQUIRE(tonePower(output[0], 440 * pitch) / power(output[0]) > 0.98);
        requireStereo(output, 0.1f);
    }
    const auto first = render(stretch, noise, 0.5, 1, 0.1f);
    render(stretch, vibrato, 0.25, 1, -1);
    REQUIRE(render(stretch, noise, 0.5, 1, 0.1f) == first);
    std::cout << "Stretch quality: harmonics, vibrato, stereo, pitch and reset passed\n";
}
