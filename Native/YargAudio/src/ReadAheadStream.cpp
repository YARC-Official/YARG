#include "ReadAheadStream.h"

#include <algorithm>
#include <chrono>
#include <cstring>
#include <thread>

namespace yarg::audio {
namespace {

// Buffers decoded audio for BASS and estimates which frame is being heard.
// The position is estimated; the speakers provide no direct position counter.

constexpr std::uint32_t BASS_SAMPLE_FLOAT = 0x100;
constexpr std::uint32_t BASS_STREAM_DECODE = 0x200000;
constexpr std::int64_t NANOSECONDS_PER_SECOND = 1'000'000'000;
// Measure clock speed over at least one second.
constexpr std::int64_t RATE_MEASUREMENT_MINIMUM_NANOSECONDS =
    NANOSECONDS_PER_SECOND;
// Reject rates more than 1% from normal; likely bad timing data.
constexpr std::int64_t MAXIMUM_CLOCK_RATE_ERROR_PPM = 10'000;
// Preserve fractional frames to prevent rounding drift.
constexpr unsigned PLAYBACK_CLOCK_FRACTION_BITS = 32;
constexpr std::uint64_t PLAYBACK_CLOCK_SCALE =
    std::uint64_t{1} << PLAYBACK_CLOCK_FRACTION_BITS;
constexpr std::int64_t PARTS_PER_MILLION = 1'000'000;
constexpr unsigned CALLBACK_TIMING_FRAME_BITS = 32;

// Read both callback stats atomically as one value.
std::uint64_t packCallbackTiming(std::uint32_t frames,
    std::uint32_t elapsedFrames) noexcept {
    return static_cast<std::uint64_t>(frames) << CALLBACK_TIMING_FRAME_BITS |
        elapsedFrames;
}

std::uint32_t callbackFrames(std::uint64_t timing) noexcept {
    return static_cast<std::uint32_t>(timing >> CALLBACK_TIMING_FRAME_BITS);
}

std::uint32_t callbackElapsedFrames(std::uint64_t timing) noexcept {
    return static_cast<std::uint32_t>(timing);
}

std::int64_t currentTimestamp() noexcept {
    // Use a monotonic clock. Tests can replace it with fake timestamps.
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

}

class ReadAheadStream::BassAudioSource final : public IAudioSource {
public:
    // Adapter between RenderAheadMixer and BASS.
    BassAudioSource(BassCoreBindings& bass, BassMixBindings& bassMix,
        int device, std::uint32_t handle, std::uint32_t channels)
        : bass_(bass), bassMix_(bassMix), device_(device), handle_(handle),
          channels_(channels) {}

    bool prepareThread() noexcept override { return bass_.setDevice(device_); }

    int read(float* samples, std::size_t frames) noexcept override {
        const auto bytesPerFrame = channels_ * sizeof(float);
        const auto bytesRequested = frames * bytesPerFrame;
        const auto bytesRead = bass_.getData(handle_, samples,
            static_cast<std::uint32_t>(bytesRequested));
        return bytesRead < 0 ? -1 :
            bytesRead / static_cast<int>(bytesPerFrame);
    }

    int lastError() const noexcept override { return bass_.error(); }

    std::int64_t position(std::uint32_t sourceHandle,
        std::uint32_t delayBytes) noexcept override {
        // Include output delay when asking BASS for the source position.
        return bassMix_.getPosition(sourceHandle, delayBytes);
    }

private:
    BassCoreBindings& bass_;
    BassMixBindings& bassMix_;
    int device_;
    std::uint32_t handle_;
    std::uint32_t channels_;
};

std::unique_ptr<ReadAheadStream> ReadAheadStream::create(
    BassCoreBindings& bass, BassMixBindings& bassMix,
    const yarg_read_ahead_config& config, int* bassError,
    TimestampProvider timestampProvider) noexcept {
    // Create the audio buffer and the BASS stream that reads from it.
    if (bassError) *bassError = 0;
    if (!bass.readAheadValid() || !bassMix.valid()) return nullptr;

    try {
        auto stream = std::unique_ptr<ReadAheadStream>(
            new ReadAheadStream(bass, bassMix, config,
                timestampProvider ? timestampProvider : &currentTimestamp));
        if (!stream->createRenderer(config.buffer_milliseconds)) return nullptr;
        stream->streamHandle_ = bass.createStream(config.sample_rate,
            config.channels, BASS_SAMPLE_FLOAT | BASS_STREAM_DECODE,
            &ReadAheadStream::streamCallback, stream.get());
        if (stream->streamHandle_ == 0) {
            if (bassError) *bassError = bass.error();
            return nullptr;
        }
        return stream;
    } catch (...) {
        return nullptr;
    }
}

ReadAheadStream::ReadAheadStream(BassCoreBindings& bass,
    BassMixBindings& bassMix, const yarg_read_ahead_config& config,
    TimestampProvider timestampProvider) noexcept
    : bass_(bass), bassMix_(bassMix), config_(config),
      timestampProvider_(timestampProvider) {}

ReadAheadStream::~ReadAheadStream() {
    if (!destroy(nullptr) && renderer_) renderer_->stop();
}

int ReadAheadStream::setCallbackClockEnabled(bool enabled) noexcept {
    // ASIO callbacks use the hardware clock. Shared callbacks use software
    // timing, so only ASIO timing is used for clock calibration.
    callbackClockEnabled_.store(enabled, std::memory_order_release);
    playbackClockRatePpm_.store(0, std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

int ReadAheadStream::prefill(std::uint32_t timeoutMilliseconds) noexcept {
    // Fill the buffer before playback starts.
    if (!renderer_ || streamHandle_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    closeConsumer();
    state_.store(YARG_READ_AHEAD_PREFILLING, std::memory_order_release);
    if (renderer_->prefill(std::chrono::milliseconds(timeoutMilliseconds))) {
        minimumQueuedFrames_.store(static_cast<std::uint32_t>(renderer_->queuedFrames()),
            std::memory_order_relaxed);
        consumerOpen_.store(true, std::memory_order_release);
        state_.store(YARG_READ_AHEAD_READY, std::memory_order_release);
        return YARG_AUDIO_OK;
    }
    if (renderer_->failed()) {
        lastError_.store(renderer_->lastError(), std::memory_order_relaxed);
        state_.store(YARG_READ_AHEAD_SOURCE_FAILED, std::memory_order_release);
        return YARG_AUDIO_ERROR_SOURCE;
    }
    state_.store(YARG_READ_AHEAD_EMPTY, std::memory_order_release);
    return YARG_AUDIO_ERROR_TIMEOUT;
}

int ReadAheadStream::flush() noexcept {
    // Stop reads, then clear audio and position state.
    if (!renderer_ || streamHandle_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    closeConsumer();
    renderer_->clear();
    state_.store(YARG_READ_AHEAD_EMPTY, std::memory_order_release);
    return YARG_AUDIO_OK;
}

int ReadAheadStream::setBufferLength(std::uint32_t bufferMilliseconds) noexcept {
    // Rebuild the buffer with a new size and restart position counting.
    if (!renderer_ || streamHandle_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    closeConsumer();
    renderer_->clear();
    if (!createRenderer(bufferMilliseconds)) return YARG_AUDIO_ERROR_INTERNAL;
    state_.store(YARG_READ_AHEAD_EMPTY, std::memory_order_release);
    return YARG_AUDIO_OK;
}

std::int64_t ReadAheadStream::getSourcePosition(std::uint32_t source,
    std::uint32_t endpointDelayFrames, int& error) noexcept {
    yarg_read_ahead_position_snapshot snapshot{
        sizeof(yarg_read_ahead_position_snapshot)};
    error = getPositionSnapshot(source, endpointDelayFrames, snapshot);
    return error == YARG_AUDIO_OK ? snapshot.heard_position : -1;
}

int ReadAheadStream::getPositionSnapshot(std::uint32_t source,
    std::uint32_t endpointDelayFrames,
    yarg_read_ahead_position_snapshot& snapshot) noexcept {
    if (!renderer_ || source == 0) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }

    // The callback may update this state while the game reads it. Odd means
    // busy; a changed value means retry.
    SourcePositionSnapshot position{};
    for (;;) {
        const auto sequence = consumerSequence_.load(std::memory_order_acquire);
        if ((sequence & 1u) != 0) {
            std::this_thread::yield();
            continue;
        }

        const auto succeeded = renderer_->sourcePositionSnapshotAfterLock(
            source, position, [this, endpointDelayFrames] {
                return remainingPlaybackDelayFrames(
                    endpointDelayFrames, timestampProvider_());
            });
        if (!succeeded) {
            return YARG_AUDIO_ERROR_BASS;
        }
        if (consumerSequence_.load(std::memory_order_acquire) == sequence) {
            break;
        }
    }

    snapshot.total_delay_frames = position.totalDelayFrames;
    snapshot.heard_position = position.heardPosition;
    snapshot.decode_position = position.decodePosition;
    return YARG_AUDIO_OK;
}

int ReadAheadStream::getStats(yarg_read_ahead_stats& stats) noexcept {
    // Debug data only.
    stats.state = state_.load(std::memory_order_acquire);
    stats.last_error = lastError_.load(std::memory_order_relaxed);
    const auto* renderer = renderer_.get();
    stats.target_frames = renderer
        ? static_cast<std::uint32_t>(renderer->targetFrames()) : 0;
    stats.queued_frames = renderer
        ? static_cast<std::uint32_t>(renderer->queuedFrames()) : 0;
    const auto minimum = minimumQueuedFrames_.load(std::memory_order_relaxed);
    stats.minimum_queued_frames = minimum == UINT32_MAX ? stats.queued_frames : minimum;
    stats.produced_frames = renderer ? renderer->producedFrames() : 0;
    stats.consumed_frames = consumedFrames_.load(std::memory_order_relaxed);
    stats.requested_frames = requestedFrames_.load(std::memory_order_relaxed);
    stats.underrun_frames = underrunFrames_.load(std::memory_order_relaxed);
    stats.underrun_events = underrunEvents_.load(std::memory_order_relaxed);
    stats.maximum_render_nanoseconds = renderer
        ? renderer->maximumRenderNanoseconds() : 0;
    stats.position_output_frame = renderer ? renderer->positionOutputFrame() : 0;
    const auto callbackTiming = callbackTiming_.load(std::memory_order_relaxed);
    const auto callbackFrameCount = callbackFrames(callbackTiming);
    const auto callbackElapsedFrameCount = callbackElapsedFrames(callbackTiming);
    stats.callback_frames = callbackFrameCount;
    stats.callback_elapsed_frames = callbackElapsedFrameCount;
    stats.callback_correction_frames =
        static_cast<std::int64_t>(callbackFrameCount) - callbackElapsedFrameCount;
    stats.callback_clock_offset_frames = callbackClockOffsetFrames_.load(
        std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool ReadAheadStream::destroy(int* bassError) noexcept {
    // Stop callbacks before destroying the renderer.
    if (bassError) *bassError = 0;
    if (streamHandle_ == 0) return true;
    state_.store(YARG_READ_AHEAD_STOPPING, std::memory_order_release);
    closeConsumer();
    renderer_->stop();
    if (!bass_.freeStream(streamHandle_)) {
        if (bassError) *bassError = bass_.error();
        return false;
    }
    streamHandle_ = 0;
    renderer_.reset();
    state_.store(YARG_READ_AHEAD_STOPPED, std::memory_order_release);
    return true;
}

std::uint32_t YARG_BASS_CALLBACK ReadAheadStream::streamCallback(
    std::uint32_t, void* buffer, std::uint32_t length, void* user) noexcept {
    if (!user || !buffer) return 0;
    auto* stream = static_cast<ReadAheadStream*>(user);
    return stream->read(buffer, length);
}

std::uint32_t ReadAheadStream::read(void* buffer, std::uint32_t length) noexcept {
    // BASS requests the next audio block here.
    activeConsumers_.fetch_add(1, std::memory_order_acq_rel);
    struct ConsumerExit {
        std::atomic<std::uint32_t>& count;
        ~ConsumerExit() { count.fetch_sub(1, std::memory_order_acq_rel); }
    } consumerExit{activeConsumers_};

    // Missing samples remain silent instead of exposing uninitialized memory.
    std::memset(buffer, 0, length);
    const auto bytesPerFrame = config_.channels * sizeof(float);
    if (bytesPerFrame == 0 || length % bytesPerFrame != 0) return length;
    const auto frames = length / bytesPerFrame;
    requestedFrames_.fetch_add(frames, std::memory_order_relaxed);
    if (!consumerOpen_.load(std::memory_order_acquire)) return length;

    // Mark shared position state as busy.
    consumerSequence_.fetch_add(1, std::memory_order_acq_rel);
    const auto consumed = renderer_->consume(static_cast<float*>(buffer), frames);
    updatePlaybackClock(consumed, timestampProvider_());
    consumerSequence_.fetch_add(1, std::memory_order_release);
    recordConsumption(consumed, frames);
    return length;
}

void ReadAheadStream::updatePlaybackClock(std::size_t consumed,
    std::int64_t timestamp) noexcept {
    const auto previousTimestamp = consumerTimestamp_.load(std::memory_order_relaxed);
    // Frames handed to BASS in this playback generation.
    const auto submittedFrames = generationConsumedFrames_.load(std::memory_order_relaxed);
    const auto clockHasExpired = playbackClockExpired(
        submittedFrames, static_cast<std::uint32_t>(consumed), timestamp);
    const auto clockHasNotStarted =
        playbackClockTimestamp_.load(std::memory_order_relaxed) == 0;
    if (clockHasNotStarted || clockHasExpired) {
        // Reset the estimate to this known frame count if it runs too far ahead.
        playbackClockFixedFrames_.store(
            (submittedFrames + consumed) * PLAYBACK_CLOCK_SCALE,
            std::memory_order_relaxed);
        playbackClockTimestamp_.store(timestamp, std::memory_order_relaxed);
    }
    generationConsumedFrames_.store(submittedFrames + consumed,
        std::memory_order_relaxed);
    consumerTimestamp_.store(timestamp, std::memory_order_relaxed);
    // Update timing before marking shared state ready.
    recordCallbackTiming(static_cast<std::uint32_t>(consumed), timestamp,
        previousTimestamp);
}

void ReadAheadStream::recordConsumption(std::size_t consumed,
    std::size_t requested) noexcept {
    consumedFrames_.fetch_add(consumed, std::memory_order_relaxed);
    const auto queued = static_cast<std::uint32_t>(renderer_->queuedFrames());
    updateMinimum(queued);
    if (renderer_->failed()) {
        lastError_.store(renderer_->lastError(), std::memory_order_relaxed);
        state_.store(YARG_READ_AHEAD_SOURCE_FAILED, std::memory_order_release);
        return;
    }
    if (consumed < requested) {
        underrunFrames_.fetch_add(requested - consumed, std::memory_order_relaxed);
        underrunEvents_.fetch_add(1, std::memory_order_relaxed);
        state_.store(YARG_READ_AHEAD_STARVED, std::memory_order_release);
        return;
    }
    state_.store(YARG_READ_AHEAD_RUNNING, std::memory_order_release);
}

bool ReadAheadStream::createRenderer(std::uint32_t bufferMilliseconds) noexcept {
    // RenderAheadMixer fills the buffer; this stream feeds BASS.
    try {
        auto source = std::make_unique<BassAudioSource>(bass_, bassMix_,
            config_.bass_device_id, config_.source_mixer, config_.channels);
        auto renderer = std::make_unique<RenderAheadMixer>(std::move(source),
            config_.sample_rate, config_.channels, config_.minimum_block_frames,
            bufferMilliseconds);
        renderer_ = std::move(renderer);
        minimumQueuedFrames_.store(
            static_cast<std::uint32_t>(renderer_->targetFrames()),
            std::memory_order_relaxed);
        return true;
    } catch (...) {
        return false;
    }
}

void ReadAheadStream::closeConsumer() noexcept {
    // Stop new reads, wait for active reads, then reset the generation.
    consumerOpen_.store(false, std::memory_order_release);
    while (activeConsumers_.load(std::memory_order_acquire) != 0) {
        std::this_thread::yield();
    }
    resetPlaybackState();
}

void ReadAheadStream::resetPlaybackState() noexcept {
    // New playback needs fresh timestamps, counters, and clock-rate data.
    consumerSequence_.fetch_add(1, std::memory_order_acq_rel);
    consumerTimestamp_.store(0, std::memory_order_relaxed);
    playbackClockTimestamp_.store(0, std::memory_order_relaxed);
    playbackClockFixedFrames_.store(0, std::memory_order_relaxed);
    generationConsumedFrames_.store(0, std::memory_order_relaxed);
    lastHeardFrame_.store(0, std::memory_order_relaxed);
    playbackClockRatePpm_.store(0, std::memory_order_relaxed);
    endpointDelayFrames_.store(UINT32_MAX, std::memory_order_relaxed);
    consumerSequence_.fetch_add(1, std::memory_order_release);
    callbackTiming_.store(0, std::memory_order_relaxed);
    callbackClockOriginTimestamp_.store(0, std::memory_order_relaxed);
    callbackClockOriginFrames_.store(0, std::memory_order_relaxed);
    callbackClockOffsetFrames_.store(0, std::memory_order_relaxed);
}

void ReadAheadStream::recordCallbackTiming(std::uint32_t frames,
    std::int64_t timestamp, std::int64_t previousTimestamp) noexcept {
    // Compare delivered frames with elapsed computer time. For ASIO, the
    // difference estimates whether the device clock is fast or slow.
    if (previousTimestamp <= 0 || config_.sample_rate == 0) return;
    const auto elapsedNanoseconds =
        std::max<std::int64_t>(0, timestamp - previousTimestamp);
    const auto elapsedFrames = std::min<std::uint64_t>(
        static_cast<std::uint64_t>(elapsedNanoseconds) * config_.sample_rate /
            NANOSECONDS_PER_SECOND,
        UINT32_MAX);
    // This debug value is rounded. Calibration below uses the full timestamp.
    const auto timing = packCallbackTiming(frames,
        static_cast<std::uint32_t>(elapsedFrames));
    callbackTiming_.store(timing, std::memory_order_relaxed);

    const auto originTimestamp = callbackClockOriginTimestamp_.load(
        std::memory_order_relaxed);
    if (originTimestamp == 0) {
        // Start measuring. One callback may be late because Windows was busy.
        callbackClockOriginFrames_.store(
            consumedFrames_.load(std::memory_order_relaxed) + frames,
            std::memory_order_relaxed);
        callbackClockOriginTimestamp_.store(timestamp, std::memory_order_relaxed);
        return;
    }

    const auto originFrames = callbackClockOriginFrames_.load(
        std::memory_order_relaxed);
    const auto totalConsumedFrames =
        consumedFrames_.load(std::memory_order_relaxed) + frames;
    const auto originElapsedNanoseconds =
        std::max<std::int64_t>(0, timestamp - originTimestamp);
    const auto clockElapsedFrames =
        static_cast<std::uint64_t>(originElapsedNanoseconds) * config_.sample_rate /
        NANOSECONDS_PER_SECOND;
    const auto actualElapsedFrames = totalConsumedFrames - originFrames;
    // Positive means more frames arrived than computer time predicted.
    const auto clockOffset = actualElapsedFrames >= clockElapsedFrames
        ? static_cast<std::int64_t>(actualElapsedFrames - clockElapsedFrames)
        : -static_cast<std::int64_t>(clockElapsedFrames - actualElapsedFrames);
    callbackClockOffsetFrames_.store(clockOffset, std::memory_order_relaxed);

    const auto nominalElapsedFrames = static_cast<double>(originElapsedNanoseconds) *
        config_.sample_rate / NANOSECONDS_PER_SECOND;
    // Shared callback timing reflects software scheduling, not device speed.
    if (!callbackClockEnabled_.load(std::memory_order_acquire)) return;
    if (originElapsedNanoseconds < RATE_MEASUREMENT_MINIMUM_NANOSECONDS ||
        actualElapsedFrames == 0 || nominalElapsedFrames <= 0) return;

    // Example: +40 ppm means 40 extra frames per million frames.
    const auto measuredPpm = (static_cast<double>(actualElapsedFrames) /
        nominalElapsedFrames - 1.0) * PARTS_PER_MILLION;
    if (measuredPpm < -MAXIMUM_CLOCK_RATE_ERROR_PPM ||
        measuredPpm > MAXIMUM_CLOCK_RATE_ERROR_PPM) {
        // Reject implausible rates and restart the measurement.
        callbackClockOriginFrames_.store(totalConsumedFrames,
            std::memory_order_relaxed);
        callbackClockOriginTimestamp_.store(timestamp, std::memory_order_relaxed);
        setPlaybackClockRate(0, timestamp);
        return;
    }

    setPlaybackClockRate(static_cast<std::int64_t>(measuredPpm), timestamp);
}

void ReadAheadStream::setPlaybackClockRate(std::int64_t ratePpm,
    std::int64_t timestamp) noexcept {
    // Advance the old estimate before changing speed. This prevents a position
    // jump when the measured rate changes.
    const auto clockTimestamp = playbackClockTimestamp_.load(
        std::memory_order_relaxed);
    if (clockTimestamp != 0 && timestamp >= clockTimestamp) {
        const auto clockFrames = playbackClockFixedFrames_.load(
            std::memory_order_relaxed);
        const auto elapsedFrames = playbackClockElapsedFrames(timestamp,
            clockTimestamp);
        playbackClockFixedFrames_.store(clockFrames + elapsedFrames,
            std::memory_order_relaxed);
        playbackClockTimestamp_.store(timestamp, std::memory_order_relaxed);
    }
    playbackClockRatePpm_.store(ratePpm, std::memory_order_relaxed);
}

std::uint32_t ReadAheadStream::remainingPlaybackDelayFrames(
    std::uint32_t endpointDelayFrames,
    std::int64_t timestamp) noexcept {
    // Return frames still between BASS and the listener, including device delay.
    endpointDelayFrames_.store(endpointDelayFrames, std::memory_order_relaxed);
    const auto submittedFrames = generationConsumedFrames_.load(std::memory_order_relaxed);
    const auto clockTimestamp = playbackClockTimestamp_.load(std::memory_order_relaxed);
    if (clockTimestamp == 0 || config_.sample_rate == 0) return endpointDelayFrames;

    // Estimate frames played since the last clock anchor.
    const auto elapsedFrames = playbackClockElapsedFrames(timestamp, clockTimestamp);
    const auto clockFrames = playbackClockFixedFrames_.load(
        std::memory_order_relaxed);
    const auto advancedFrames = clockFrames + elapsedFrames;
    const auto submittedFixedFrames = submittedFrames * PLAYBACK_CLOCK_SCALE;
    const auto delayedSubmittedFrames = submittedFixedFrames +
        static_cast<std::uint64_t>(endpointDelayFrames) * PLAYBACK_CLOCK_SCALE;
    // Submitted frames minus played frames equals frames still waiting.
    auto remainingFixedFrames = std::uint64_t{0};
    if (delayedSubmittedFrames > advancedFrames) {
        remainingFixedFrames = delayedSubmittedFrames - advancedFrames;
    }
    auto heardFixedFrames = std::uint64_t{0};
    if (remainingFixedFrames < submittedFixedFrames) {
        heardFixedFrames = submittedFixedFrames - remainingFixedFrames;
    }
    const auto estimatedHeardFrames = heardFixedFrames / PLAYBACK_CLOCK_SCALE;

    // Never report an earlier position than a previous read.
    auto previousHeardFrames = lastHeardFrame_.load(std::memory_order_relaxed);
    while (previousHeardFrames < estimatedHeardFrames &&
        !lastHeardFrame_.compare_exchange_weak(
            previousHeardFrames, estimatedHeardFrames,
            std::memory_order_relaxed)) {
    }
    const auto heardFrames = std::min(
        std::max(previousHeardFrames, estimatedHeardFrames), submittedFrames);
    if (heardFrames == 0 && remainingFixedFrames > submittedFixedFrames) {
        const auto remainingFrames =
            (remainingFixedFrames + PLAYBACK_CLOCK_SCALE - 1) /
            PLAYBACK_CLOCK_SCALE;
        return static_cast<std::uint32_t>(std::min<std::uint64_t>(
            remainingFrames, UINT32_MAX));
    }
    return static_cast<std::uint32_t>(std::min<std::uint64_t>(
        submittedFrames - heardFrames, UINT32_MAX));
}

std::uint64_t ReadAheadStream::playbackClockElapsedFrames(
    std::int64_t timestamp, std::int64_t clockTimestamp) const noexcept {
    // Convert time to frames using the measured rate. Preserve fractions.
    const auto elapsedNanoseconds =
        std::max<std::int64_t>(0, timestamp - clockTimestamp);
    const auto ratePpm = playbackClockRatePpm_.load(std::memory_order_relaxed);
    const auto rateScale = PARTS_PER_MILLION + ratePpm;
    const auto nominalElapsedFrames = static_cast<double>(elapsedNanoseconds) *
        config_.sample_rate / NANOSECONDS_PER_SECOND;
    return static_cast<std::uint64_t>(nominalElapsedFrames * rateScale *
        PLAYBACK_CLOCK_SCALE / PARTS_PER_MILLION);
}

bool ReadAheadStream::playbackClockExpired(std::uint64_t submittedFrames,
    std::uint32_t callbackFrames, std::int64_t timestamp) const noexcept {
    // Reset if the estimate is ahead of frames BASS has received.
    const auto endpointDelay = endpointDelayFrames_.load(std::memory_order_relaxed);
    const auto clockTimestamp = playbackClockTimestamp_.load(std::memory_order_relaxed);
    if (endpointDelay == UINT32_MAX || clockTimestamp == 0 || config_.sample_rate == 0) {
        return false;
    }

    const auto elapsedFrames = playbackClockElapsedFrames(timestamp, clockTimestamp);
    const auto clockFrames = playbackClockFixedFrames_.load(
        std::memory_order_relaxed);
    const auto advancedFrames = clockFrames + elapsedFrames;
    const auto endpointFixedFrames = static_cast<std::uint64_t>(endpointDelay) *
        PLAYBACK_CLOCK_SCALE;
    if (advancedFrames <= endpointFixedFrames) return false;
    const auto playedFrames = advancedFrames - endpointFixedFrames;
    const auto submittedLimit = (submittedFrames + callbackFrames) *
        PLAYBACK_CLOCK_SCALE;
    return playedFrames > submittedLimit;
}

void ReadAheadStream::updateMinimum(std::uint32_t queued) noexcept {
    // Track the lowest queue depth for underrun diagnostics.
    auto previous = minimumQueuedFrames_.load(std::memory_order_relaxed);
    while (queued < previous && !minimumQueuedFrames_.compare_exchange_weak(
        previous, queued, std::memory_order_relaxed)) {
    }
}

}
