#include "Test.h"
#include "stretch/TextureGrains.h"

#include <algorithm>
#include <cmath>
#include <vector>

namespace {

constexpr int BLOCK = 3584;
constexpr int HOP = 448;
constexpr int RATE = 44100;
constexpr int STEPS = 80;
constexpr double PI = 3.14159265358979323846;
using STFT = signalsmith::linear::DynamicSTFT<float, false, true>;

std::vector<float> renderNoise(float amount, float rightGain, bool secondHalfSilent, float frequency = 3000) {
    STFT analysis;
    analysis.configure(2, 2, BLOCK, HOP + 1);
    analysis.setInterval(HOP, analysis.kaiser);
    analysis.reset();
    yarg::audio::TextureGrains<float> grains;
    grains.configure(analysis, 2);
    std::vector<float> mask(analysis.bands(), amount);
    std::vector<float> chunk(BLOCK);
    std::vector<float> result;
    int frame = -BLOCK;
    for (int step = 0; step < STEPS; ++step) {
        const int count = step == 0 ? BLOCK : HOP;
        for (int i = 0; i < count; ++i, ++frame) {
            const bool silent = secondHalfSilent && frame >= STEPS * HOP / 2;
            chunk[i] = silent ? 0.0f : float(0.5 * std::sin(2 * PI * frequency * frame / RATE));
        }
        analysis.writeInput(0, count, chunk.data());
        for (int i = 0; i < count; ++i) {
            chunk[i] *= rightGain;
        }
        analysis.writeInput(1, count, chunk.data());
        analysis.moveInput(count);
        analysis.analyse();
        grains.add(analysis, [&](int c, int b) { return analysis.spectrum(c)[b]; }, mask);
        for (int i = 0; i < HOP; ++i) {
            const float left = grains.read(0);
            REQUIRE(std::isfinite(left));
            REQUIRE(std::abs(grains.read(1) - left * rightGain) < 1e-6);
            result.push_back(left);
            grains.advance();
        }
    }
    REQUIRE(result.size() == STEPS * HOP);
    grains.reset();
    for (int i = 0; i < BLOCK; ++i) {
        REQUIRE(grains.read(0) == 0);
        REQUIRE(grains.read(1) == 0);
        grains.advance();
    }
    return result;
}

double rms(const std::vector<float> &signal, int first, int last) {
    double power = 0;
    for (int i = first; i < last; ++i) {
        power += double(signal[i]) * signal[i];
    }
    return std::sqrt(power / (last - first));
}

double correlation(const std::vector<float> &output) {
    double cross = 0;
    double outputPower = 0;
    double sinePower = 0;
    for (int i = BLOCK * 2; i < int(output.size()); ++i) {
        const double sine = 0.5 * std::sin(2 * PI * 3000 * (i - BLOCK) / RATE);
        cross += output[i] * sine;
        outputPower += output[i] * output[i];
        sinePower += sine * sine;
    }
    return cross / std::sqrt(outputPower * sinePower);
}

}

void runTextureGrainsTests() {
    const auto lowTone = renderNoise(1, -1, false, 200);
    double largestJump = 0;
    for (int i = BLOCK * 2 + 1; i < int(lowTone.size()); ++i) {
        largestJump = std::max(largestJump, std::abs(double(lowTone[i]) - lowTone[i - 1]));
    }
    const double relativeJump = largestJump / rms(lowTone, BLOCK * 2, int(lowTone.size()));
    std::cout << "Texture grains: low-tone maximum jump/rms=" << relativeJump << '\n';
    REQUIRE(relativeJump < 0.2);
    const auto full = renderNoise(1, -1, false);
    const double fullRms = rms(full, BLOCK * 2, int(full.size()));
    std::cout << "Texture grains: full-mask rms=" << fullRms
        << " sine-correlation=" << correlation(full) << '\n';
    REQUIRE(fullRms > 0.05);
    REQUIRE(fullRms < 1.0);
    REQUIRE(std::abs(correlation(full)) < 0.3);
    const auto muted = renderNoise(0, 0, false);
    REQUIRE(std::all_of(muted.begin(), muted.end(), [](float value) { return value == 0; }));
    REQUIRE(renderNoise(1, -1, false) == full);
    const auto quiet = renderNoise(0.25f, 0, false);
    REQUIRE(std::abs(rms(quiet, BLOCK * 2, int(quiet.size())) / fullRms - 0.5) < 0.05);
    const auto gated = renderNoise(1, 1, true);
    const int half = int(gated.size()) / 2;
    REQUIRE(rms(gated, BLOCK * 2, half) > 5 * rms(gated, half + BLOCK * 2, int(gated.size())));
    std::cout << "Texture grains: morphing, scaling, envelope and reset passed\n";
}
