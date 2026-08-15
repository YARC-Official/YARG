#include "ReadAheadStream.h"
#include "Test.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <memory>
#include <thread>
#include <vector>

using namespace yarg::audio;

namespace {

struct MockBass {
    BassStreamProc callback = nullptr;
    void* callbackUser = nullptr;
    std::atomic<std::size_t> availableFrames{0};
    std::atomic<std::size_t> nextFrame{0};
    std::atomic<std::size_t> decodedFrames{0};
    std::atomic<std::uint32_t> lastPositionDelay{0};
    std::atomic<std::uint32_t> positionCalls{0};
    std::atomic<bool> blockPosition{false};
    std::atomic<bool> positionBlocked{false};
    bool fail = false;
    bool freed = false;
    int errorCode = 73;
    int decodePositionError = 0;
    int delayedPositionError = 0;
    bool positionFromDecodedFrames = false;
    std::uint32_t expectedFrequency = 1000;
};

MockBass* mock = nullptr;

struct FakeClock {
    std::int64_t timestamp = 1'000'000'000;
};

FakeClock* fakeClock = nullptr;

std::int64_t fakeTimestamp() noexcept {
    return fakeClock->timestamp;
}

int YARG_BASS_CALL setDevice(std::uint32_t device) { return device == 7; }

std::uint32_t YARG_BASS_CALL getData(std::uint32_t, void* buffer,
    std::uint32_t bytes) {
    if (mock->fail) return UINT32_MAX;
    const auto requested = bytes / sizeof(float);
    const auto available = mock->availableFrames.load();
    const auto frames = std::min<std::size_t>(requested, available);
    auto* samples = static_cast<float*>(buffer);
    for (std::size_t frame = 0; frame < frames; ++frame) {
        samples[frame] = static_cast<float>(mock->nextFrame.fetch_add(1));
    }
    mock->decodedFrames.fetch_add(frames);
    mock->availableFrames.fetch_sub(frames);
    return static_cast<std::uint32_t>(frames * sizeof(float));
}

int YARG_BASS_CALL error() { return mock->errorCode; }
std::uint32_t YARG_BASS_CALL setDsp(std::uint32_t, BassDspProc, void*, int) { return 1; }
int YARG_BASS_CALL removeDsp(std::uint32_t, std::uint32_t) { return 1; }
int YARG_BASS_CALL lockChannel(std::uint32_t, int) { return 1; }
int YARG_BASS_CALL getInfo(std::uint32_t, BassChannelInfo*) { return 1; }
std::uint32_t YARG_BASS_CALL getConfig(std::uint32_t) { return 0; }

std::uint32_t YARG_BASS_CALL createStream(std::uint32_t frequency,
    std::uint32_t channels, std::uint32_t flags, BassStreamProc callback,
    void* user) {
    REQUIRE(frequency == mock->expectedFrequency);
    REQUIRE(channels == 1);
    REQUIRE(flags == (0x100u | 0x200000u));
    mock->callback = callback;
    mock->callbackUser = user;
    return 19;
}

int YARG_BASS_CALL freeStream(std::uint32_t stream) {
    REQUIRE(stream == 19);
    mock->freed = true;
    return 1;
}

std::uint64_t YARG_BASS_CALL getPosition(
    std::uint32_t, std::uint32_t, std::uint32_t delay) {
    mock->positionCalls.fetch_add(1);
    mock->lastPositionDelay.store(delay);
    if (mock->blockPosition.load()) {
        mock->positionBlocked.store(true);
        while (mock->blockPosition.load()) {
            std::this_thread::yield();
        }
    }
    if (delay == 0 && mock->decodePositionError != 0) {
        mock->errorCode = mock->decodePositionError;
        return UINT64_MAX;
    }
    if (delay > 0 && mock->delayedPositionError != 0) {
        mock->errorCode = mock->delayedPositionError;
        return UINT64_MAX;
    }
    if (mock->positionFromDecodedFrames) {
        const auto delayFrames = delay / sizeof(float);
        const auto decodedFrames = mock->decodedFrames.load();
        return decodedFrames >= delayFrames ? decodedFrames - delayFrames : 0;
    }
    return 100 + delay;
}

BassCoreBindings makeCore(MockBass& state) {
    mock = &state;
    return BassCoreBindings(BassCoreFunctions{
        &setDevice, &getData, &error, &setDsp, &removeDsp, &lockChannel,
        &getInfo, &getConfig, &createStream, &freeStream});
}

BassMixBindings makeMix() {
    return BassMixBindings(BassMixFunctions{&getPosition, nullptr, nullptr});
}

yarg_read_ahead_config config(std::uint32_t milliseconds) {
    return yarg_read_ahead_config{sizeof(yarg_read_ahead_config), 7, 11,
        1000, 1, 4, milliseconds};
}

yarg_read_ahead_config configAtRate(std::uint32_t milliseconds,
    std::uint32_t sampleRate) {
    return yarg_read_ahead_config{sizeof(yarg_read_ahead_config), 7, 11,
        sampleRate, 1, 4, milliseconds};
}

void testPrefillConsumptionPositionAndResize() {
    MockBass state;
    state.availableFrames.store(8);
    auto core = makeCore(state);
    auto mix = makeMix();
    int bassError = -1;
    auto stream = ReadAheadStream::create(core, mix, config(4), &bassError);
    REQUIRE(stream);
    REQUIRE(bassError == 0);
    REQUIRE(stream->streamHandle() == 19);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> output(3);
    REQUIRE(state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser) == output.size() * sizeof(float));
    REQUIRE(output == std::vector<float>({0, 1, 2}));

    int positionError = -1;
    REQUIRE(stream->getSourcePosition(23, 0, positionError) == 120);
    REQUIRE(state.lastPositionDelay.load() == 20);
    REQUIRE(positionError == YARG_AUDIO_OK);

    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    REQUIRE(stream->getPositionSnapshot(23, 0, snapshot) == YARG_AUDIO_OK);
    REQUIRE(snapshot.total_delay_frames == 5);
    REQUIRE(snapshot.heard_position == 120);
    REQUIRE(snapshot.decode_position == 100);

    REQUIRE(stream->flush() == YARG_AUDIO_OK);
    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(stats.queued_frames == 0);
    REQUIRE(stream->setBufferLength(20) == YARG_AUDIO_OK);
    state.availableFrames.store(20);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(stats.target_frames == 20);
    REQUIRE(stream->destroy(&bassError));
    REQUIRE(state.freed);
}

void testUnderrunReturnsSilenceWithoutEndingStream() {
    MockBass state;
    state.availableFrames.store(8);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> output(12, -1);
    REQUIRE(state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser) == output.size() * sizeof(float));
    REQUIRE(output[0] == 0);
    REQUIRE(output[7] == 7);
    REQUIRE(output[8] == 0);
    REQUIRE(output[11] == 0);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(stats.underrun_events == 1);
    REQUIRE(stats.underrun_frames == 4);
    REQUIRE(stream->destroy(nullptr));
}

void testPositionBeforeGenerationStartReturnsGenerationOrigin() {
    MockBass state;
    state.availableFrames.store(8);
    state.delayedPositionError = 37;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    int positionError = -1;
    REQUIRE(stream->getSourcePosition(23, 2, positionError) == 0);
    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(state.lastPositionDelay.load() == 40);

    REQUIRE(stream->flush() == YARG_AUDIO_OK);
    state.availableFrames.store(8);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);
    REQUIRE(stream->getSourcePosition(23, 2, positionError) == 0);
    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(state.lastPositionDelay.load() == 40);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(stats.produced_frames == 16);
    REQUIRE(stream->destroy(nullptr));
}

void testPositionBeforeGenerationStartUsesAvailableHistory() {
    MockBass state;
    state.availableFrames.store(8);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    int positionError = -1;
    REQUIRE(stream->getSourcePosition(23, 2, positionError) == 140);
    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(state.lastPositionDelay.load() == 40);

    REQUIRE(stream->flush() == YARG_AUDIO_OK);
    state.availableFrames.store(8);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);
    REQUIRE(stream->getSourcePosition(23, 2, positionError) == 140);
    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(state.lastPositionDelay.load() == 40);
    REQUIRE(stream->destroy(nullptr));
}

void testPositionLookupFailureIsReported() {
    MockBass state;
    state.availableFrames.store(8);
    state.delayedPositionError = 20;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> output(3);
    REQUIRE(state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser) == output.size() * sizeof(float));

    int positionError = -1;
    REQUIRE(stream->getSourcePosition(23, 0, positionError) == -1);
    REQUIRE(positionError == YARG_AUDIO_ERROR_BASS);
    REQUIRE(state.lastPositionDelay.load() == 20);
    REQUIRE(stream->destroy(nullptr));
}

void testDecodePositionLookupFailureIsReported() {
    MockBass state;
    state.availableFrames.store(8);
    state.decodePositionError = 20;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    REQUIRE(stream->getPositionSnapshot(23, 0, snapshot) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(stream->destroy(nullptr));
}

void testPositionSnapshotRetriesAfterConsumption() {
    MockBass state;
    state.availableFrames.store(8);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    state.blockPosition.store(true);
    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    int result = YARG_AUDIO_ERROR_INTERNAL;
    std::thread positionThread([&] {
        result = stream->getPositionSnapshot(23, 0, snapshot);
    });

    const auto deadline = std::chrono::steady_clock::now() +
        std::chrono::seconds(2);
    while (!state.positionBlocked.load() &&
        std::chrono::steady_clock::now() < deadline) {
        std::this_thread::yield();
    }
    REQUIRE(state.positionBlocked.load());

    float output = -1;
    state.callback(19, &output, sizeof(output), state.callbackUser);
    state.blockPosition.store(false);
    positionThread.join();

    REQUIRE(result == YARG_AUDIO_OK);
    REQUIRE(state.positionCalls.load() >= 4);
    REQUIRE(snapshot.total_delay_frames == 7);
    REQUIRE(stream->destroy(nullptr));
}

void testPositionAdvancesBetweenOutputPulls() {
    MockBass state;
    state.availableFrames.store(256);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(128), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> output(110);
    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);

    int positionError = -1;
    stream->getSourcePosition(23, 100, positionError);
    const auto firstDelay = state.lastPositionDelay.load();
    std::this_thread::sleep_for(std::chrono::milliseconds(25));
    stream->getSourcePosition(23, 100, positionError);
    const auto secondDelay = state.lastPositionDelay.load();

    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(secondDelay < firstDelay);
    REQUIRE(firstDelay - secondDelay >= sizeof(float));
    REQUIRE(stream->destroy(nullptr));
}

void testCallbackTimingDoesNotMovePositionBackward() {
    MockBass state;
    state.availableFrames.store(512);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(128), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> firstOutput(110);
    state.callback(19, firstOutput.data(),
        static_cast<std::uint32_t>(firstOutput.size() * sizeof(float)),
        state.callbackUser);

    int positionError = -1;
    stream->getSourcePosition(23, 100, positionError);
    std::this_thread::sleep_for(std::chrono::milliseconds(25));
    stream->getSourcePosition(23, 100, positionError);
    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    const auto beforeCallback = stats.position_output_frame;

    std::vector<float> secondOutput(10);
    state.callback(19, secondOutput.data(),
        static_cast<std::uint32_t>(secondOutput.size() * sizeof(float)),
        state.callbackUser);
    stream->getSourcePosition(23, 100, positionError);
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);

    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(stats.position_output_frame >= beforeCallback);
    REQUIRE(stream->destroy(nullptr));
}

void testLateCallbackDoesNotJumpPosition() {
    MockBass state;
    state.availableFrames.store(512);
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(128), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_OK);

    std::vector<float> output(128);
    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    const auto refillDeadline = std::chrono::steady_clock::now() +
        std::chrono::seconds(2);
    do {
        REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
        if (stats.queued_frames >= output.size()) break;
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    } while (std::chrono::steady_clock::now() < refillDeadline);
    REQUIRE(stats.queued_frames >= output.size());

    int positionError = -1;
    stream->getSourcePosition(23, 128, positionError);
    std::this_thread::sleep_for(std::chrono::milliseconds(300));
    stream->getSourcePosition(23, 128, positionError);
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    const auto beforeCallback = stats.position_output_frame;

    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);
    stream->getSourcePosition(23, 128, positionError);
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);

    REQUIRE(positionError == YARG_AUDIO_OK);
    REQUIRE(stats.position_output_frame == beforeCallback);
    REQUIRE(stream->destroy(nullptr));
}

void testSyntheticUiSamplingUnderCallbackRateMismatch() {
    FakeClock clock;
    fakeClock = &clock;

    constexpr std::uint32_t SAMPLE_RATE = 44100;
    constexpr double CLOCK_ERROR_PPM = 40.0;
    constexpr double CALLBACK_INTERVAL_NS = 32.0 * 1'000'000'000.0 /
        SAMPLE_RATE * (1.0 + CLOCK_ERROR_PPM / 1'000'000.0);
    constexpr std::int64_t SAMPLE_INTERVAL_NS = 7'500'000;
    constexpr int SAMPLE_COUNT = 14000;

    MockBass state;
    state.availableFrames.store(10000000);
    state.positionFromDecodedFrames = true;
    state.expectedFrequency = SAMPLE_RATE;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix,
        configAtRate(120000, SAMPLE_RATE), nullptr, &fakeTimestamp);
    REQUIRE(stream);
    REQUIRE(stream->prefill(10000) == YARG_AUDIO_OK);

    std::vector<float> output(32);
    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);

    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    REQUIRE(stream->getPositionSnapshot(23, 128, snapshot) == YARG_AUDIO_OK);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    const auto initialOutputFrame = stats.position_output_frame;
    auto previousOutputFrame = initialOutputFrame;
    const auto initialTimestamp = clock.timestamp;
    auto callbackTimestamp = static_cast<double>(clock.timestamp);
    auto previousDriftMs = 0.0;
    auto largestDriftStepMs = 0.0;
    int driftStepCount = 0;
    int firstDriftStepSample = -1;
    std::uint32_t firstCallbackElapsedFrames = 0;
    std::int64_t firstCallbackCorrectionFrames = 0;
    std::int64_t firstCallbackClockOffsetFrames = 0;

    for (int sample = 0; sample < SAMPLE_COUNT; sample++) {
        const auto sampleTimestamp = initialTimestamp +
            static_cast<std::int64_t>(sample + 1) * SAMPLE_INTERVAL_NS;
        while (callbackTimestamp + CALLBACK_INTERVAL_NS <= sampleTimestamp) {
            callbackTimestamp += CALLBACK_INTERVAL_NS;
            clock.timestamp = static_cast<std::int64_t>(callbackTimestamp + 0.5);
            state.callback(19, output.data(),
                static_cast<std::uint32_t>(output.size() * sizeof(float)),
                state.callbackUser);
        }
        clock.timestamp = sampleTimestamp;
        REQUIRE(stream->getPositionSnapshot(23, 128, snapshot) == YARG_AUDIO_OK);
        REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
        REQUIRE(stats.position_output_frame >= previousOutputFrame);
        previousOutputFrame = stats.position_output_frame;

        const auto hostFrames = static_cast<double>(sample + 1) *
            SAMPLE_INTERVAL_NS * SAMPLE_RATE / 1'000'000'000.0;
        const auto outputFrames = static_cast<double>(
            stats.position_output_frame - initialOutputFrame);
        const auto driftMs = (hostFrames - outputFrames) * 1000.0 / SAMPLE_RATE;
        if (sample == 0) {
            previousDriftMs = driftMs;
            continue;
        }
        const auto driftStepMs = driftMs - previousDriftMs;
        largestDriftStepMs = std::max(largestDriftStepMs, driftStepMs);
        if (driftStepMs > 1.0) {
            driftStepCount++;
            if (firstDriftStepSample < 0) {
                firstDriftStepSample = sample;
                firstCallbackElapsedFrames = stats.callback_elapsed_frames;
                firstCallbackCorrectionFrames = stats.callback_correction_frames;
                firstCallbackClockOffsetFrames = stats.callback_clock_offset_frames;
            }
        }
        previousDriftMs = driftMs;
    }

    std::cout << "synthetic " << CLOCK_ERROR_PPM
        << " ppm drift_steps=" << driftStepCount
        << " largest_drift_step_ms=" << largestDriftStepMs
        << " final_drift_ms=" << previousDriftMs
        << " first_step_sample=" << firstDriftStepSample
        << " callback_elapsed=" << firstCallbackElapsedFrames
        << " callback_correction=" << firstCallbackCorrectionFrames
        << " callback_offset=" << firstCallbackClockOffsetFrames << '\n';
    REQUIRE(driftStepCount == 0);
    REQUIRE(largestDriftStepMs < 1.0);
    REQUIRE(previousDriftMs > 1.0);
    REQUIRE(stream->destroy(nullptr));
    fakeClock = nullptr;
}

void testCallbackRateUpdateDoesNotStepPosition() {
    FakeClock clock;
    fakeClock = &clock;

    constexpr std::uint32_t SAMPLE_RATE = 44100;
    constexpr double CLOCK_ERROR_PPM = 2000.0;
    constexpr double CALLBACK_INTERVAL_NS = 32.0 * 1'000'000'000.0 /
        SAMPLE_RATE * (1.0 + CLOCK_ERROR_PPM / 1'000'000.0);
    constexpr std::int64_t SAMPLE_INTERVAL_NS = 7'500'000;
    constexpr int SAMPLE_COUNT = 600;

    MockBass state;
    state.availableFrames.store(10000000);
    state.positionFromDecodedFrames = true;
    state.expectedFrequency = SAMPLE_RATE;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix,
        configAtRate(120000, SAMPLE_RATE), nullptr, &fakeTimestamp);
    REQUIRE(stream);
    REQUIRE(stream->prefill(10000) == YARG_AUDIO_OK);

    std::vector<float> primeOutput(12000);
    state.callback(19, primeOutput.data(),
        static_cast<std::uint32_t>(primeOutput.size() * sizeof(float)),
        state.callbackUser);

    std::vector<float> output(32);
    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);

    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    REQUIRE(stream->getPositionSnapshot(23, 10000, snapshot) == YARG_AUDIO_OK);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    const auto initialOutputFrame = stats.position_output_frame;
    auto previousOutputFrame = initialOutputFrame;
    const auto initialTimestamp = clock.timestamp;
    auto callbackTimestamp = static_cast<double>(clock.timestamp);
    auto previousDriftMs = 0.0;
    auto largestDriftStepMs = 0.0;

    for (int sample = 0; sample < SAMPLE_COUNT; sample++) {
        const auto sampleTimestamp = initialTimestamp +
            static_cast<std::int64_t>(sample + 1) * SAMPLE_INTERVAL_NS;
        while (callbackTimestamp + CALLBACK_INTERVAL_NS <= sampleTimestamp) {
            callbackTimestamp += CALLBACK_INTERVAL_NS;
            clock.timestamp = static_cast<std::int64_t>(callbackTimestamp + 0.5);
            state.callback(19, output.data(),
                static_cast<std::uint32_t>(output.size() * sizeof(float)),
                state.callbackUser);
        }
        clock.timestamp = sampleTimestamp;
        REQUIRE(stream->getPositionSnapshot(23, 10000, snapshot) == YARG_AUDIO_OK);
        REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
        REQUIRE(stats.position_output_frame >= previousOutputFrame);
        previousOutputFrame = stats.position_output_frame;

        const auto hostFrames = static_cast<double>(sample + 1) *
            SAMPLE_INTERVAL_NS * SAMPLE_RATE / 1'000'000'000.0;
        const auto outputFrames = static_cast<double>(
            stats.position_output_frame - initialOutputFrame);
        const auto driftMs = (hostFrames - outputFrames) * 1000.0 / SAMPLE_RATE;
        if (sample > 0) {
            largestDriftStepMs = std::max(largestDriftStepMs,
                driftMs - previousDriftMs);
        }
        previousDriftMs = driftMs;
    }

    REQUIRE(largestDriftStepMs < 1.0);
    REQUIRE(previousDriftMs > 1.0);
    REQUIRE(stream->destroy(nullptr));
    fakeClock = nullptr;
}

void testCallbackClockRetainsFractionalFrames() {
    FakeClock clock;
    fakeClock = &clock;

    constexpr std::uint32_t SAMPLE_RATE = 48000;
    constexpr double CALLBACK_INTERVAL_NS = 32.0 * 1'000'000'000.0 / SAMPLE_RATE;
    constexpr double CALLBACK_JITTER_NS = 0.5 * 1'000'000'000.0 / SAMPLE_RATE;
    constexpr std::int64_t SAMPLE_INTERVAL_NS = 7'500'000;
    constexpr int SAMPLE_COUNT = 8000;

    MockBass state;
    state.availableFrames.store(10000000);
    state.positionFromDecodedFrames = true;
    state.expectedFrequency = SAMPLE_RATE;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix,
        configAtRate(120000, SAMPLE_RATE), nullptr, &fakeTimestamp);
    REQUIRE(stream);
    REQUIRE(stream->prefill(10000) == YARG_AUDIO_OK);

    std::vector<float> output(32);
    state.callback(19, output.data(),
        static_cast<std::uint32_t>(output.size() * sizeof(float)),
        state.callbackUser);

    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    REQUIRE(stream->getPositionSnapshot(23, 128, snapshot) == YARG_AUDIO_OK);

    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    const auto initialOutputFrame = stats.position_output_frame;
    const auto initialTimestamp = clock.timestamp;
    auto callbackTimestamp = static_cast<double>(clock.timestamp);
    auto previousDriftMs = 0.0;
    bool shortCallback = true;

    for (int sample = 0; sample < SAMPLE_COUNT; sample++) {
        const auto sampleTimestamp = initialTimestamp +
            static_cast<std::int64_t>(sample + 1) * SAMPLE_INTERVAL_NS;
        while (true) {
            const auto callbackInterval = CALLBACK_INTERVAL_NS +
                (shortCallback ? -CALLBACK_JITTER_NS : CALLBACK_JITTER_NS);
            if (callbackTimestamp + callbackInterval > sampleTimestamp) break;
            callbackTimestamp += callbackInterval;
            shortCallback = !shortCallback;
            clock.timestamp = static_cast<std::int64_t>(callbackTimestamp + 0.5);
            state.callback(19, output.data(),
                static_cast<std::uint32_t>(output.size() * sizeof(float)),
                state.callbackUser);
        }
        clock.timestamp = sampleTimestamp;
        REQUIRE(stream->getPositionSnapshot(23, 128, snapshot) == YARG_AUDIO_OK);
        REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);

        const auto hostFrames = static_cast<double>(sample + 1) *
            SAMPLE_INTERVAL_NS * SAMPLE_RATE / 1'000'000'000.0;
        const auto outputFrames = static_cast<double>(
            stats.position_output_frame - initialOutputFrame);
        const auto driftMs = (hostFrames - outputFrames) * 1000.0 / SAMPLE_RATE;
        previousDriftMs = driftMs;
    }

    REQUIRE(previousDriftMs > -100.0);
    REQUIRE(previousDriftMs < 100.0);
    REQUIRE(stream->destroy(nullptr));
    fakeClock = nullptr;
}

void testSourceFailureIsReported() {
    MockBass state;
    state.fail = true;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = ReadAheadStream::create(core, mix, config(4), nullptr);
    REQUIRE(stream);
    REQUIRE(stream->prefill(2000) == YARG_AUDIO_ERROR_SOURCE);
    yarg_read_ahead_stats stats{sizeof(yarg_read_ahead_stats)};
    REQUIRE(stream->getStats(stats) == YARG_AUDIO_OK);
    REQUIRE(stats.state == YARG_READ_AHEAD_SOURCE_FAILED);
    REQUIRE(stats.last_error == 73);
    REQUIRE(stream->destroy(nullptr));
}

}

void runReadAheadStreamTests() {
    testPrefillConsumptionPositionAndResize();
    testUnderrunReturnsSilenceWithoutEndingStream();
    testPositionBeforeGenerationStartReturnsGenerationOrigin();
    testPositionBeforeGenerationStartUsesAvailableHistory();
    testPositionLookupFailureIsReported();
    testDecodePositionLookupFailureIsReported();
    testPositionSnapshotRetriesAfterConsumption();
    testPositionAdvancesBetweenOutputPulls();
    testCallbackTimingDoesNotMovePositionBackward();
    testLateCallbackDoesNotJumpPosition();
    testSyntheticUiSamplingUnderCallbackRateMismatch();
    testCallbackRateUpdateDoesNotStepPosition();
    testCallbackClockRetainsFractionalFrames();
    testSourceFailureIsReported();
}
