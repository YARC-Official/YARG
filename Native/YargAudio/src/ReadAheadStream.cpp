#include "ReadAheadStream.h"

#include <algorithm>
#include <chrono>
#include <cstring>
#include <limits>
#include <thread>

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassStreamDecode = 0x200000;
constexpr std::int64_t NanosecondsPerSecond = 1'000'000'000;

std::uint64_t packCallbackTiming(std::uint32_t frames,
    std::uint32_t elapsedFrames) noexcept {
    return static_cast<std::uint64_t>(frames) << 32 | elapsedFrames;
}

std::uint32_t callbackFrames(std::uint64_t timing) noexcept {
    return static_cast<std::uint32_t>(timing >> 32);
}

std::uint32_t callbackElapsedFrames(std::uint64_t timing) noexcept {
    return static_cast<std::uint32_t>(timing);
}

std::int64_t currentTimestamp() noexcept {
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

}

class ReadAheadStream::BassAudioSource final : public IAudioSource {
public:
    BassAudioSource(BassCoreBindings& bass, BassMixBindings& bassMix,
        int device, std::uint32_t handle, std::uint32_t channels)
        : bass_(bass), bassMix_(bassMix), device_(device), handle_(handle),
          channels_(channels) {}

    bool prepareThread() noexcept override { return bass_.setDevice(device_); }

    int read(float* samples, std::size_t frames) noexcept override {
        const auto bytes = frames * channels_ * sizeof(float);
        const auto result = bass_.getData(handle_, samples,
            static_cast<std::uint32_t>(bytes));
        return result < 0 ? -1 : result / static_cast<int>(channels_ * sizeof(float));
    }

    int lastError() const noexcept override { return bass_.error(); }

    std::int64_t position(std::uint32_t sourceHandle,
        std::uint32_t delayBytes) noexcept override {
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
    const yarg_read_ahead_config& config, int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!bass.readAheadValid() || !bassMix.valid()) return nullptr;

    try {
        auto stream = std::unique_ptr<ReadAheadStream>(
            new ReadAheadStream(bass, bassMix, config));
        if (!stream->createRenderer(config.buffer_milliseconds)) return nullptr;
        stream->streamHandle_ = bass.createStream(config.sample_rate,
            config.channels, BassSampleFloat | BassStreamDecode,
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
    BassMixBindings& bassMix, const yarg_read_ahead_config& config) noexcept
    : bass_(bass), bassMix_(bassMix), config_(config) {}

ReadAheadStream::~ReadAheadStream() {
    int error = 0;
    if (!destroy(&error) && renderer_) renderer_->stop();
}

int ReadAheadStream::prefill(std::uint32_t timeoutMilliseconds) noexcept {
    if (!renderer_ || streamHandle_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    closeConsumer();
    state_.store(YARG_READ_AHEAD_PREFILLING, std::memory_order_release);
    if (!renderer_->prefill(std::chrono::milliseconds(timeoutMilliseconds))) {
        if (renderer_->failed()) {
            lastError_.store(renderer_->lastError(), std::memory_order_relaxed);
            state_.store(YARG_READ_AHEAD_SOURCE_FAILED, std::memory_order_release);
            return YARG_AUDIO_ERROR_SOURCE;
        }
        state_.store(YARG_READ_AHEAD_EMPTY, std::memory_order_release);
        return YARG_AUDIO_ERROR_TIMEOUT;
    }
    minimumQueuedFrames_.store(static_cast<std::uint32_t>(renderer_->queuedFrames()),
        std::memory_order_relaxed);
    consumerOpen_.store(true, std::memory_order_release);
    state_.store(YARG_READ_AHEAD_READY, std::memory_order_release);
    return YARG_AUDIO_OK;
}

int ReadAheadStream::flush() noexcept {
    if (!renderer_ || streamHandle_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    closeConsumer();
    renderer_->clear();
    state_.store(YARG_READ_AHEAD_EMPTY, std::memory_order_release);
    return YARG_AUDIO_OK;
}

int ReadAheadStream::setBufferLength(std::uint32_t bufferMilliseconds) noexcept {
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
                    endpointDelayFrames, currentTimestamp());
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
    stats.state = state_.load(std::memory_order_acquire);
    stats.last_error = lastError_.load(std::memory_order_relaxed);
    stats.target_frames = renderer_
        ? static_cast<std::uint32_t>(renderer_->targetFrames()) : 0;
    stats.queued_frames = renderer_
        ? static_cast<std::uint32_t>(renderer_->queuedFrames()) : 0;
    const auto minimum = minimumQueuedFrames_.load(std::memory_order_relaxed);
    stats.minimum_queued_frames = minimum == UINT32_MAX ? stats.queued_frames : minimum;
    stats.produced_frames = renderer_ ? renderer_->producedFrames() : 0;
    stats.consumed_frames = consumedFrames_.load(std::memory_order_relaxed);
    stats.requested_frames = requestedFrames_.load(std::memory_order_relaxed);
    stats.underrun_frames = underrunFrames_.load(std::memory_order_relaxed);
    stats.underrun_events = underrunEvents_.load(std::memory_order_relaxed);
    stats.maximum_render_nanoseconds = renderer_
        ? renderer_->maximumRenderNanoseconds() : 0;
    stats.position_output_frame = renderer_ ? renderer_->positionOutputFrame() : 0;
    const auto callbackTiming = callbackTiming_.load(std::memory_order_relaxed);
    stats.callback_frames = callbackFrames(callbackTiming);
    stats.callback_elapsed_frames = callbackElapsedFrames(callbackTiming);
    stats.callback_correction_frames = static_cast<std::int64_t>(stats.callback_frames) -
        stats.callback_elapsed_frames;
    stats.callback_clock_offset_frames = callbackClockOffsetFrames_.load(
        std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool ReadAheadStream::destroy(int* bassError) noexcept {
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
    return static_cast<ReadAheadStream*>(user)->read(buffer, length);
}

std::uint32_t ReadAheadStream::read(void* buffer, std::uint32_t length) noexcept {
    activeConsumers_.fetch_add(1, std::memory_order_acq_rel);
    struct ConsumerExit {
        std::atomic<std::uint32_t>& count;
        ~ConsumerExit() { count.fetch_sub(1, std::memory_order_acq_rel); }
    } consumerExit{activeConsumers_};

    std::memset(buffer, 0, length);
    const auto bytesPerFrame = config_.channels * sizeof(float);
    if (bytesPerFrame == 0 || length % bytesPerFrame != 0) return length;
    const auto frames = length / bytesPerFrame;
    requestedFrames_.fetch_add(frames, std::memory_order_relaxed);
    if (!consumerOpen_.load(std::memory_order_acquire)) return length;

    consumerSequence_.fetch_add(1, std::memory_order_acq_rel);
    const auto consumed = renderer_->consume(static_cast<float*>(buffer), frames);
    const auto timestamp = currentTimestamp();
    const auto previousTimestamp = consumerTimestamp_.load(std::memory_order_relaxed);
    const auto submittedFrames = generationConsumedFrames_.load(std::memory_order_relaxed);
    const auto expired = playbackClockExpired(
        submittedFrames, static_cast<std::uint32_t>(consumed), timestamp);
    if (playbackClockTimestamp_.load(std::memory_order_relaxed) == 0 || expired) {
        if (expired) lastHeardFrame_.store(submittedFrames, std::memory_order_relaxed);
        playbackClockFrames_.store(submittedFrames + consumed, std::memory_order_relaxed);
        playbackClockTimestamp_.store(timestamp, std::memory_order_relaxed);
    }
    generationConsumedFrames_.store(submittedFrames + consumed, std::memory_order_relaxed);
    consumerTimestamp_.store(timestamp, std::memory_order_relaxed);
    consumerSequence_.fetch_add(1, std::memory_order_release);
    recordCallbackTiming(static_cast<std::uint32_t>(consumed), timestamp,
        previousTimestamp);
    consumedFrames_.fetch_add(consumed, std::memory_order_relaxed);
    const auto queued = static_cast<std::uint32_t>(renderer_->queuedFrames());
    updateMinimum(queued);
    if (renderer_->failed()) {
        lastError_.store(renderer_->lastError(), std::memory_order_relaxed);
        state_.store(YARG_READ_AHEAD_SOURCE_FAILED, std::memory_order_release);
    } else if (consumed < frames) {
        underrunFrames_.fetch_add(frames - consumed, std::memory_order_relaxed);
        underrunEvents_.fetch_add(1, std::memory_order_relaxed);
        state_.store(YARG_READ_AHEAD_STARVED, std::memory_order_release);
    } else {
        state_.store(YARG_READ_AHEAD_RUNNING, std::memory_order_release);
    }
    return length;
}

bool ReadAheadStream::createRenderer(std::uint32_t bufferMilliseconds) noexcept {
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
    consumerOpen_.store(false, std::memory_order_release);
    while (activeConsumers_.load(std::memory_order_acquire) != 0) {
        std::this_thread::yield();
    }
    resetConsumerClock();
}

void ReadAheadStream::resetConsumerClock() noexcept {
    consumerSequence_.fetch_add(1, std::memory_order_acq_rel);
    consumerTimestamp_.store(0, std::memory_order_relaxed);
    playbackClockTimestamp_.store(0, std::memory_order_relaxed);
    playbackClockFrames_.store(0, std::memory_order_relaxed);
    generationConsumedFrames_.store(0, std::memory_order_relaxed);
    lastHeardFrame_.store(0, std::memory_order_relaxed);
    endpointDelayFrames_.store(UINT32_MAX, std::memory_order_relaxed);
    consumerSequence_.fetch_add(1, std::memory_order_release);
    callbackTiming_.store(0, std::memory_order_relaxed);
    callbackClockOriginTimestamp_.store(0, std::memory_order_relaxed);
    callbackClockOriginFrames_.store(0, std::memory_order_relaxed);
    callbackClockOffsetFrames_.store(0, std::memory_order_relaxed);
}

void ReadAheadStream::recordCallbackTiming(std::uint32_t frames,
    std::int64_t timestamp, std::int64_t previousTimestamp) noexcept {
    if (previousTimestamp <= 0 || config_.sample_rate == 0) return;
    const auto elapsed = std::max<std::int64_t>(0, timestamp - previousTimestamp);
    const auto elapsedFrames = std::min<std::uint64_t>(
        static_cast<std::uint64_t>(elapsed) * config_.sample_rate / NanosecondsPerSecond,
        UINT32_MAX);
    const auto timing = packCallbackTiming(frames,
        static_cast<std::uint32_t>(elapsedFrames));
    callbackTiming_.store(timing, std::memory_order_relaxed);

    auto originTimestamp = callbackClockOriginTimestamp_.load(std::memory_order_relaxed);
    if (originTimestamp == 0) {
        callbackClockOriginFrames_.store(
            consumedFrames_.load(std::memory_order_relaxed) + frames,
            std::memory_order_relaxed);
        callbackClockOriginTimestamp_.store(timestamp, std::memory_order_relaxed);
        return;
    }

    const auto originFrames = callbackClockOriginFrames_.load(std::memory_order_relaxed);
    const auto consumedFrames = consumedFrames_.load(std::memory_order_relaxed) + frames;
    const auto clockElapsed = static_cast<std::uint64_t>(timestamp - originTimestamp) *
        config_.sample_rate / NanosecondsPerSecond;
    const auto actualElapsed = consumedFrames - originFrames;
    const auto clockOffset = actualElapsed >= clockElapsed
        ? static_cast<std::int64_t>(actualElapsed - clockElapsed)
        : -static_cast<std::int64_t>(clockElapsed - actualElapsed);
    callbackClockOffsetFrames_.store(clockOffset, std::memory_order_relaxed);
}

std::uint32_t ReadAheadStream::remainingPlaybackDelayFrames(
    std::uint32_t endpointDelayFrames,
    std::int64_t timestamp) noexcept {
    endpointDelayFrames_.store(endpointDelayFrames, std::memory_order_relaxed);
    const auto submittedFrames = generationConsumedFrames_.load(std::memory_order_relaxed);
    const auto clockTimestamp = playbackClockTimestamp_.load(std::memory_order_relaxed);
    if (clockTimestamp == 0 || config_.sample_rate == 0) return endpointDelayFrames;

    const auto elapsed = std::max<std::int64_t>(0, timestamp - clockTimestamp);
    const auto elapsedFrames = static_cast<std::uint64_t>(elapsed) * config_.sample_rate /
        NanosecondsPerSecond;
    const auto clockFrames = playbackClockFrames_.load(std::memory_order_relaxed);
    const auto advancedFrames = clockFrames + elapsedFrames;
    const auto delayedSubmittedFrames = submittedFrames + endpointDelayFrames;
    const auto remaining = delayedSubmittedFrames > advancedFrames
        ? delayedSubmittedFrames - advancedFrames : 0;
    const auto candidate = remaining < submittedFrames
        ? submittedFrames - remaining : 0;
    const auto bounded = std::min(candidate, submittedFrames);

    auto previous = lastHeardFrame_.load(std::memory_order_relaxed);
    while (previous < bounded && !lastHeardFrame_.compare_exchange_weak(
        previous, bounded, std::memory_order_relaxed)) {
    }
    const auto heard = std::min(std::max(previous, bounded), submittedFrames);
    if (heard > 0 || remaining <= submittedFrames) {
        return static_cast<std::uint32_t>(std::min<std::uint64_t>(
            submittedFrames - heard, UINT32_MAX));
    }
    return static_cast<std::uint32_t>(std::min<std::uint64_t>(remaining, UINT32_MAX));
}

bool ReadAheadStream::playbackClockExpired(std::uint64_t submittedFrames,
    std::uint32_t callbackFrames, std::int64_t timestamp) const noexcept {
    const auto endpointDelay = endpointDelayFrames_.load(std::memory_order_relaxed);
    const auto clockTimestamp = playbackClockTimestamp_.load(std::memory_order_relaxed);
    if (endpointDelay == UINT32_MAX || clockTimestamp == 0 || config_.sample_rate == 0) {
        return false;
    }

    const auto elapsed = std::max<std::int64_t>(0, timestamp - clockTimestamp);
    const auto elapsedFrames = static_cast<std::uint64_t>(elapsed) * config_.sample_rate /
        NanosecondsPerSecond;
    const auto clockFrames = playbackClockFrames_.load(std::memory_order_relaxed);
    const auto advancedFrames = clockFrames + elapsedFrames;
    const auto candidate = advancedFrames > endpointDelay
        ? advancedFrames - endpointDelay : 0;
    return candidate > submittedFrames + callbackFrames;
}

void ReadAheadStream::updateMinimum(std::uint32_t queued) noexcept {
    auto previous = minimumQueuedFrames_.load(std::memory_order_relaxed);
    while (queued < previous && !minimumQueuedFrames_.compare_exchange_weak(
        previous, queued, std::memory_order_relaxed)) {
    }
}

}
