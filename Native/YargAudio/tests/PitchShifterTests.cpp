#include "dsp/PitchShiftDsp.h"
#include "dsp/PitchShifter.h"
#include "Test.h"

#include <chrono>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <random>
#include <string>
#include <vector>

using namespace yarg::audio;

namespace {

constexpr double PI = 3.14159265358979323846;

struct MockBass {
    std::uint32_t channelFlags = 0x100;
    std::uint32_t floatDspConfig = 0;
    std::uint32_t dspResult = 23;
    int error = 5;
    std::uint32_t frequency = 44100;
    std::uint32_t channels = 2;
    BassDspProc callback = nullptr;
    void* callbackUser = nullptr;
    std::vector<std::string> events;
};

MockBass* mock = nullptr;

int YARG_BASS_CALL mockSetDevice(std::uint32_t) { return 1; }
std::uint32_t YARG_BASS_CALL mockGetData(std::uint32_t, void*, std::uint32_t length) { return length; }
int YARG_BASS_CALL mockError() { return mock->error; }
std::uint32_t YARG_BASS_CALL mockSetDsp(std::uint32_t, BassDspProc callback, void* user, int) {
    mock->callback = callback;
    mock->callbackUser = user;
    return mock->dspResult;
}
int YARG_BASS_CALL mockRemoveDsp(std::uint32_t, std::uint32_t) {
    mock->events.emplace_back("remove");
    return 1;
}
int YARG_BASS_CALL mockChannelLock(std::uint32_t, int lock) {
    mock->events.emplace_back(lock ? "lock" : "unlock");
    return 1;
}
int YARG_BASS_CALL mockGetInfo(std::uint32_t, BassChannelInfo* info) {
    info->frequency = mock->frequency;
    info->channels = mock->channels;
    info->flags = mock->channelFlags;
    return 1;
}
std::uint32_t YARG_BASS_CALL mockGetConfig(std::uint32_t) { return mock->floatDspConfig; }

BassCoreFunctions completeFunctions() {
    return {&mockSetDevice, &mockGetData, &mockError, &mockSetDsp,
        &mockRemoveDsp, &mockChannelLock, &mockGetInfo, &mockGetConfig};
}

std::vector<float> noise(std::size_t samples, unsigned seed) {
    std::mt19937 rng(seed);
    std::uniform_real_distribution<float> dist(-0.5f, 0.5f);
    std::vector<float> data(samples);
    for (auto& v : data) v = dist(rng);
    return data;
}

std::vector<float> harmonicTone(std::size_t frames, double frequency, double sampleRate) {
    std::vector<float> data(frames);
    for (std::size_t i = 0; i < frames; ++i) {
        double value = 0.0;
        for (int h = 1; h <= 8; ++h) {
            value += std::sin(2.0 * PI * frequency * h * i / sampleRate + h * 0.7) / h;
        }
        data[i] = static_cast<float>(0.3 * value);
    }
    return data;
}

void processInBlocks(PitchShifter& shifter, std::vector<float>& data, std::uint32_t channels,
    std::size_t block) {
    const std::size_t frames = data.size() / channels;
    for (std::size_t offset = 0; offset < frames; offset += block) {
        const std::size_t count = std::min(block, frames - offset);
        shifter.process(data.data() + offset * channels, count);
    }
}

double goertzelPower(const float* data, std::size_t count, double frequency, double sampleRate) {
    const double w = 2.0 * PI * frequency / sampleRate;
    const double coefficient = 2.0 * std::cos(w);
    double s1 = 0.0;
    double s2 = 0.0;
    for (std::size_t i = 0; i < count; ++i) {
        const double window = 0.5 - 0.5 * std::cos(2.0 * PI * i / (count - 1));
        const double s0 = data[i] * window + coefficient * s1 - s2;
        s2 = s1;
        s1 = s0;
    }
    return s1 * s1 + s2 * s2 - coefficient * s1 * s2;
}

double dominantFrequency(const float* data, std::size_t count, double low, double high, double sampleRate) {
    double best = low;
    double bestPower = -1.0;
    for (double f = low; f <= high; f += 0.25) {
        const double power = goertzelPower(data, count, f, sampleRate);
        if (power > bestPower) {
            bestPower = power;
            best = f;
        }
    }
    return best;
}

void testLatency() {
    REQUIRE(PitchShifter::latencyFrames(44100) == 1529);
    REQUIRE(PitchShifter::latencyFrames(48000) == 1664);
    REQUIRE(PitchShifter::latencyFrames(0) == 0);
    REQUIRE(PitchShifter::latencyFrames(1000) == 0);
    PitchShifter shifter;
    REQUIRE(!shifter.configure(0, 44100));
    REQUIRE(!shifter.configure(2, 0));
    REQUIRE(!shifter.configure(PitchShifter::MAX_CHANNELS + 1, 44100));
    REQUIRE(shifter.configure(2, 48000));
    REQUIRE(shifter.latency() == 1664);
}

void testIdleIsExactDelay() {
    const std::uint32_t channels = 2;
    PitchShifter shifter;
    REQUIRE(shifter.configure(channels, 44100));
    const auto input = noise(44100 * channels, 1);
    auto output = input;
    const std::size_t blocks[] = {1, 7, 128, 4096, 20000, 300};
    std::size_t offset = 0;
    std::size_t index = 0;
    const std::size_t frames = input.size() / channels;
    while (offset < frames) {
        const std::size_t count = std::min(blocks[index++ % 6], frames - offset);
        shifter.process(output.data() + offset * channels, count);
        offset += count;
    }
    const std::size_t latency = shifter.latency();
    for (std::size_t i = 0; i < frames; ++i) {
        for (std::uint32_t c = 0; c < channels; ++c) {
            const float expected = i < latency ? 0.0f : input[(i - latency) * channels + c];
            REQUIRE(output[i * channels + c] == expected);
        }
    }
    REQUIRE(shifter.settled());
    REQUIRE(shifter.spliceCount() == 0);
}

void testPitchAccuracy() {
    const double sampleRate = 44100.0;
    const double ratios[] = {std::pow(2.0, -1.0 / 12.0), std::pow(2.0, -5.0 / 12.0), 0.5, 1.5, 2.0};
    for (double ratio : ratios) {
        PitchShifter shifter;
        REQUIRE(shifter.configure(1, 44100));
        shifter.setRatio(static_cast<float>(ratio));
        const double frequency = 220.0;
        auto data = harmonicTone(static_cast<std::size_t>(sampleRate * 3), frequency, sampleRate);
        processInBlocks(shifter, data, 1, 512);

        const std::size_t start = static_cast<std::size_t>(sampleRate);
        const std::size_t count = static_cast<std::size_t>(sampleRate * 2);
        const double measured = dominantFrequency(data.data() + start, count,
            frequency * ratio * 0.95, frequency * ratio * 1.05, sampleRate);
        REQUIRE(std::abs(measured / (frequency * ratio) - 1.0) < 0.003);
        REQUIRE(shifter.spliceCount() > 0);

        const double shifted = goertzelPower(data.data() + start, count, frequency * ratio, sampleRate);
        const double original = goertzelPower(data.data() + start, count, frequency, sampleRate);
        if (ratio > 0.6) REQUIRE(shifted > original * 100.0);
    }
}

void testSplicesAreClickFree() {
    const double sampleRate = 44100.0;
    const double frequency = 330.0;
    const double ratio = std::pow(2.0, -5.0 / 12.0);
    PitchShifter shifter;
    REQUIRE(shifter.configure(1, 44100));
    shifter.setRatio(static_cast<float>(ratio));
    std::vector<float> data(static_cast<std::size_t>(sampleRate * 2));
    for (std::size_t i = 0; i < data.size(); ++i) {
        data[i] = static_cast<float>(0.5 * std::sin(2.0 * PI * frequency * i / sampleRate));
    }
    processInBlocks(shifter, data, 1, 256);

    const double maxSlope = 0.5 * 2.0 * PI * frequency / sampleRate;
    float largestStep = 0.0f;
    float smallestPeak = 1.0f;
    const std::size_t start = static_cast<std::size_t>(sampleRate * 0.25);
    for (std::size_t i = start; i < data.size(); ++i) {
        largestStep = std::max(largestStep, std::abs(data[i] - data[i - 1]));
    }
    const std::size_t period = static_cast<std::size_t>(sampleRate / (frequency * ratio)) + 1;
    for (std::size_t i = start; i + period < data.size(); i += period) {
        float peak = 0.0f;
        for (std::size_t k = 0; k < period; ++k) peak = std::max(peak, std::abs(data[i + k]));
        smallestPeak = std::min(smallestPeak, peak);
    }
    REQUIRE(shifter.spliceCount() > 5);
    REQUIRE(largestStep < maxSlope * 1.1);
    REQUIRE(smallestPeak > 0.45f);
}

void testReturnsToExactAlignment() {
    const std::uint32_t channels = 2;
    const std::size_t sampleRate = 44100;
    PitchShifter shifter;
    REQUIRE(shifter.configure(channels, sampleRate));
    const auto input = noise(sampleRate * 4 * channels, 3);
    auto output = input;
    const std::size_t bend = sampleRate * channels;
    shifter.setRatio(0.8f);
    shifter.process(output.data(), sampleRate / 2);
    shifter.setRatio(1.0f);
    for (std::size_t offset = sampleRate / 2; offset < sampleRate * 4; offset += 512) {
        shifter.process(output.data() + offset * channels, std::min<std::size_t>(512, sampleRate * 4 - offset));
    }
    (void) bend;
    REQUIRE(shifter.settled());
    const std::size_t latency = shifter.latency();
    for (std::size_t i = sampleRate * 3; i < sampleRate * 4; ++i) {
        for (std::uint32_t c = 0; c < channels; ++c) {
            REQUIRE(output[i * channels + c] == input[(i - latency) * channels + c]);
        }
    }
}

void testResetClearsHistory() {
    PitchShifter shifter;
    REQUIRE(shifter.configure(1, 44100));
    auto first = noise(10000, 4);
    shifter.setRatio(0.7f);
    shifter.process(first.data(), first.size());
    shifter.setRatio(1.0f);
    shifter.reset();
    REQUIRE(shifter.settled());
    const auto input = noise(5000, 5);
    auto output = input;
    shifter.process(output.data(), output.size());
    const std::size_t latency = shifter.latency();
    for (std::size_t i = 0; i < output.size(); ++i) {
        REQUIRE(output[i] == (i < latency ? 0.0f : input[i - latency]));
    }
}

void testRandomRatioStress() {
    const std::uint32_t channels = 2;
    PitchShifter shifter;
    REQUIRE(shifter.configure(channels, 44100));
    std::mt19937 rng(9);
    std::uniform_real_distribution<float> ratioDist(0.3f, 2.5f);
    std::uniform_int_distribution<int> blockDist(1, 9000);
    std::uniform_int_distribution<int> choice(0, 3);
    const std::uint32_t maxDelay = shifter.latency() * 3;
    std::size_t total = 0;
    while (total < 44100u * 60) {
        const auto count = static_cast<std::size_t>(blockDist(rng));
        auto block = noise(count * channels, static_cast<unsigned>(total));
        const int pick = choice(rng);
        shifter.setRatio(pick == 0 ? 1.0f : pick == 1 ? std::nanf("") : ratioDist(rng));
        shifter.process(block.data(), count);
        for (float v : block) {
            REQUIRE(std::isfinite(v));
            REQUIRE(std::abs(v) <= 1.0f);
        }
        REQUIRE(shifter.currentDelay() >= 2.0);
        REQUIRE(shifter.currentDelay() <= maxDelay);
        total += count;
    }
}

void testBassAttachment() {
    MockBass state;
    mock = &state;
    BassCoreBindings bass(completeFunctions());

    yarg_pitch_shift_dsp* dsp = nullptr;
    int bassError = -1;
    REQUIRE(pitchShiftDspAttach(bass, 0, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    state.channelFlags = 0;
    REQUIRE(pitchShiftDspAttach(bass, 11, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_UNSUPPORTED);
    state.channelFlags = 0x100;
    state.channels = 0;
    REQUIRE(pitchShiftDspAttach(bass, 11, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_UNSUPPORTED);
    state.channels = 2;
    state.dspResult = 0;
    REQUIRE(pitchShiftDspAttach(bass, 11, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == 5);
    REQUIRE(dsp == nullptr);
    state.dspResult = 23;

    REQUIRE(pitchShiftDspAttach(bass, 11, 0, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr && state.callbackUser == dsp);
    std::uint32_t latency = 0;
    REQUIRE(pitchShiftDspGetLatency(dsp, &latency) == YARG_AUDIO_OK);
    REQUIRE(latency == PitchShifter::latencyFrames(44100));
    REQUIRE(pitchShiftDspSetRatio(dsp, std::nanf("")) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(pitchShiftDspSetRatio(dsp, 0.0f) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(pitchShiftDspSetRatio(dsp, 0.9f) == YARG_AUDIO_OK);

    auto input = noise(4096 * 2, 6);
    auto buffer = input;
    state.callback(23, 11, buffer.data(), 3 * sizeof(float), state.callbackUser);
    REQUIRE(buffer == input);
    state.callback(23, 11, buffer.data(), static_cast<std::uint32_t>(buffer.size() * sizeof(float)), state.callbackUser);
    REQUIRE(!dsp->shifter.settled());

    REQUIRE(pitchShiftDspSetRatio(dsp, 1.0f) == YARG_AUDIO_OK);
    REQUIRE(pitchShiftDspRequestReset(dsp) == YARG_AUDIO_OK);
    buffer = input;
    state.callback(23, 11, buffer.data(), static_cast<std::uint32_t>(buffer.size() * sizeof(float)), state.callbackUser);
    REQUIRE(dsp->shifter.settled());
    REQUIRE(buffer[0] == 0.0f && buffer[2 * latency] == input[0]);

    REQUIRE(pitchShiftDspRequestReset(nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(pitchShiftDspGetLatency(nullptr, &latency) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(pitchShiftDspDestroy(dsp));
    REQUIRE(state.events.size() == 3 && state.events[1] == "remove");
}

double framesPerSecond(PitchShifter& shifter, std::vector<float>& data, std::uint32_t channels) {
    const auto begin = std::chrono::steady_clock::now();
    for (int pass = 0; pass < 4; ++pass) processInBlocks(shifter, data, channels, 6400);
    const double seconds = std::chrono::duration<double>(std::chrono::steady_clock::now() - begin).count();
    return 4.0 * static_cast<double>(data.size() / channels) / seconds;
}

void reportThroughput() {
    const std::uint32_t channels = 2;
    auto data = noise(44100 * 10 * channels, 7);
    PitchShifter idle;
    REQUIRE(idle.configure(channels, 44100));
    const double idleRate = framesPerSecond(idle, data, channels);
    PitchShifter bending;
    REQUIRE(bending.configure(channels, 44100));
    bending.setRatio(0.75f);
    const double bendRate = framesPerSecond(bending, data, channels);
    std::cout << "PitchShifter stereo throughput: idle " << idleRate / 44100.0
              << "x realtime, bending " << bendRate / 44100.0 << "x realtime\n";
    REQUIRE(bendRate > 44100.0 * 300.0);
}

} // namespace

void runPitchShifterTests() {
    testLatency();
    testIdleIsExactDelay();
    testPitchAccuracy();
    testSplicesAreClickFree();
    testReturnsToExactAlignment();
    testResetClearsHistory();
    testRandomRatioStress();
    testBassAttachment();
    reportThroughput();
}
