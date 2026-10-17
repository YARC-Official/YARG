#include "ReadAheadStream.h"
#include "Test.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <iostream>

using namespace yarg::audio;

namespace {

constexpr std::uint32_t SAMPLE_RATE = 48000;
constexpr std::uint32_t BLOCK_FRAMES = 480;
constexpr std::uint32_t TEST_SECONDS = 300;
constexpr std::uint64_t TRACK_FRAMES = SAMPLE_RATE * (TEST_SECONDS + 2);
constexpr double TOLERANCE_MS = 5.0;
constexpr double NANOSECONDS_PER_SECOND = 1'000'000'000.0;
constexpr std::uint32_t OUTPUT_STREAM = 29;

struct Device {
    std::atomic<std::uint64_t> decodedFrames{0};
    BassStreamProc callback = nullptr;
    void* callbackUser = nullptr;
    std::int64_t timestamp = 1'000'000'000;
    std::uint32_t queuedFrames = 0;
    bool outputLocked = false;
    std::uint32_t locks = 0;
    std::uint32_t unlocks = 0;
};

Device* device = nullptr;

std::int64_t timestamp() noexcept {
    return device->timestamp;
}

int YARG_BASS_CALL setDevice(std::uint32_t) { return 1; }
int YARG_BASS_CALL error() { return 0; }
int YARG_BASS_CALL freeStream(std::uint32_t) { return 1; }

int YARG_BASS_CALL lockChannel(std::uint32_t channel, int lock) {
    REQUIRE(channel == OUTPUT_STREAM);
    REQUIRE(device->outputLocked != (lock != 0));
    device->outputLocked = lock != 0;
    if (lock != 0) {
        ++device->locks;
    } else {
        ++device->unlocks;
    }
    return 1;
}

std::uint32_t YARG_BASS_CALL createStream(std::uint32_t, std::uint32_t,
    std::uint32_t, BassStreamProc callback, void* user) {
    device->callback = callback;
    device->callbackUser = user;
    return 19;
}

std::uint32_t YARG_BASS_CALL decode(std::uint32_t channel, void* buffer,
    std::uint32_t bytes) {
    if (channel == OUTPUT_STREAM) {
        REQUIRE(device->outputLocked);
        REQUIRE(buffer == nullptr);
        REQUIRE(bytes == 0);
        return device->queuedFrames * sizeof(float);
    }
    const auto start = device->decodedFrames.load();
    const auto frames = std::min<std::uint64_t>(bytes / sizeof(float), TRACK_FRAMES - start);
    auto* samples = static_cast<float*>(buffer);
    for (std::uint64_t frame = 0; frame < frames; ++frame) {
        const auto trackFrame = start + frame;
        samples[frame] = trackFrame > 0 && trackFrame % SAMPLE_RATE == 0 ? 1.0f : 0.0f;
    }
    device->decodedFrames.store(start + frames);
    return static_cast<std::uint32_t>(frames * sizeof(float));
}

std::uint64_t YARG_BASS_CALL position(std::uint32_t, std::uint32_t,
    std::uint32_t delayBytes) {
    const auto decodedBytes = device->decodedFrames.load() * sizeof(float);
    return decodedBytes > delayBytes ? decodedBytes - delayBytes : 0;
}

bool run(const char* name, double deviceRate, bool independentClock,
    bool variableQueue = false) {
    Device state;
    device = &state;
    BassCoreFunctions functions{};
    functions.setDevice = &setDevice;
    functions.channelGetData = &decode;
    functions.errorGetCode = &error;
    functions.streamCreate = &createStream;
    functions.streamFree = &freeStream;
    functions.channelLock = &lockChannel;
    BassCoreBindings core(functions);
    BassMixBindings mix(BassMixFunctions{&position, nullptr, nullptr});
    const yarg_read_ahead_config config{sizeof(yarg_read_ahead_config), 7, 11,
        SAMPLE_RATE, 1, BLOCK_FRAMES, (TEST_SECONDS + 1) * 1000,
        independentClock ? 0 : OUTPUT_STREAM};
    auto stream = ReadAheadStream::create(core, mix, config, nullptr, &timestamp);
    REQUIRE(stream);
    REQUIRE(stream->setCallbackClockEnabled(independentClock) == YARG_AUDIO_OK);
    REQUIRE(stream->prefill(10000) == YARG_AUDIO_OK);

    std::array<float, BLOCK_FRAMES> output{};
    const auto origin = state.timestamp;
    const auto blocks = TEST_SECONDS * SAMPLE_RATE / BLOCK_FRAMES;
    double firstError = 0;
    double maximumDrift = 0;
    double finalDrift = 0;
    double audibleDrift = 0;
    std::uint32_t clicks = 0;
    for (std::uint32_t block = 0; block <= blocks; ++block) {
        state.timestamp = origin + static_cast<std::int64_t>(std::llround(
            block * BLOCK_FRAMES * NANOSECONDS_PER_SECOND / (SAMPLE_RATE * deviceRate)));
        if (variableQueue) {
            state.timestamp += block % 2 == 0 ? 200'000 : -200'000;
        }
        REQUIRE(state.callback(19, output.data(), sizeof(output), state.callbackUser) == sizeof(output));
        yarg_read_ahead_position_snapshot snapshot{sizeof(yarg_read_ahead_position_snapshot)};
        const auto queuedFrames = variableQueue ? BLOCK_FRAMES * 2 + (block % 8) * 48 : 0;
        state.queuedFrames = queuedFrames;
        REQUIRE(stream->getPositionSnapshot(23, queuedFrames, snapshot) == YARG_AUDIO_OK);

        for (std::uint32_t frame = 0; frame < BLOCK_FRAMES; ++frame) {
            if (output[frame] == 0) {
                continue;
            }
            ++clicks;
            const auto clickFrame = static_cast<std::uint64_t>(clicks) * SAMPLE_RATE;
            REQUIRE(clickFrame == static_cast<std::uint64_t>(block) * BLOCK_FRAMES + frame);
            REQUIRE(output[frame] == 1.0f);
            const auto trailingFrames = BLOCK_FRAMES - frame;
            const auto estimatedClickFrame = snapshot.heard_position / static_cast<double>(sizeof(float)) + queuedFrames - trailingFrames;
            const auto errorMs = (estimatedClickFrame - clickFrame) * 1000.0 / SAMPLE_RATE;
            if (clicks == 1) {
                firstError = errorMs;
            }
            finalDrift = errorMs - firstError;
            maximumDrift = std::max(maximumDrift, std::abs(finalDrift));
            const auto actualClickSeconds = (clickFrame - SAMPLE_RATE) / (SAMPLE_RATE * deviceRate);
            const auto gameplayClickSeconds = (clickFrame - SAMPLE_RATE) / static_cast<double>(SAMPLE_RATE);
            audibleDrift = (actualClickSeconds - gameplayClickSeconds) * 1000.0;
        }
    }

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(clicks == TEST_SECONDS);
    REQUIRE(stats.underrun_frames == 0);
    REQUIRE(stats.underrun_events == 0);
    REQUIRE(!state.outputLocked);
    REQUIRE(state.locks == state.unlocks);
    REQUIRE(state.locks == (independentClock ? 0 : blocks + 1));
    REQUIRE(stream->destroy(nullptr));
    const auto passed = maximumDrift <= TOLERANCE_MS;
    std::cout << name << ": " << (passed ? "PASS" : "FAIL")
        << ", clicks=" << clicks
        << ", audible_vs_gameplay_drift_ms=" << audibleDrift
        << ", estimate_vs_consumed_click_drift_ms=" << finalDrift
        << ", maximum_estimate_drift_ms=" << maximumDrift
        << ", tolerance_ms=" << TOLERANCE_MS << ", underruns=0\n";
    return passed;
}

}

int main() {
    const auto nominal = run("shared nominal", 1.0, false);
    const auto independentFast = run("independent +1000 ppm", 1.001, true);
    const auto independentSlow = run("independent -1000 ppm", 0.999, true);
    const auto sharedFast = run("shared +1000 ppm", 1.001, false);
    const auto sharedSlow = run("shared -1000 ppm", 0.999, false);
    const auto variableQueue = run("shared +1000 ppm with queue variation and jitter", 1.001, false, true);
    return nominal && independentFast && independentSlow && sharedFast && sharedSlow && variableQueue ? 0 : 1;
}
