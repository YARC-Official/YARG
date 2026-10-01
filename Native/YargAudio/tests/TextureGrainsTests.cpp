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
constexpr int IMPULSE_FRAME = 10000;
constexpr double PI = 3.14159265358979323846;
using STFT = signalsmith::linear::DynamicSTFT<float, false, true>;

std::vector<float> renderGrains(bool impulse, float amount, float rightGain) {
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
            chunk[i] = impulse ? (frame == IMPULSE_FRAME ? 1.0f : 0.0f) :
                float(0.5 * std::sin(2 * PI * 3000 * frame / RATE));
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

}

void runTextureGrainsTests() {
    const auto sine = renderGrains(false, 1, -1);
    double power = 0;
    for (int i = BLOCK * 2; i < int(sine.size()); ++i) {
        power += sine[i] * sine[i];
        REQUIRE(std::abs(sine[i]) < 0.52f);
        REQUIRE(std::abs(sine[i] - sine[i - 1]) < 0.24f);
    }
    const double rms = std::sqrt(power / (sine.size() - BLOCK * 2));
    REQUIRE(std::abs(rms - std::sqrt(0.125)) < 0.01);
    const auto muted = renderGrains(false, 0, 0);
    REQUIRE(std::all_of(muted.begin(), muted.end(), [](float value) { return value == 0; }));
    const auto impulse = renderGrains(true, 1, 1);
    double energy = 0;
    for (float value : impulse) {
        energy += value * value;
        REQUIRE(std::abs(value) <= 1.001f);
    }
    REQUIRE(std::abs(energy - 1) < 0.001);
    REQUIRE(std::abs(impulse[IMPULSE_FRAME + BLOCK] - 1) < 0.001f);
    REQUIRE(renderGrains(false, 1, -1) == sine);
    const auto quiet = renderGrains(false, 0.25f, 0);
    for (int i = 0; i < int(sine.size()); ++i) {
        REQUIRE(std::abs(quiet[i] * 2 - sine[i]) < 1e-5);
    }
    std::cout << "Texture grains: gain, continuity, impulse alignment, stereo, mask and reset passed\n";
}
