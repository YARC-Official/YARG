#pragma once

#include "BassCoreBindings.h"
#include "BassMixBindings.h"
#include "RenderAheadMixer.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <memory>

namespace yarg::audio {

class ReadAheadStream final {
public:
    using TimestampProvider = std::int64_t (*)() noexcept;

    static std::unique_ptr<ReadAheadStream> create(
        BassCoreBindings& bass, BassMixBindings& bassMix,
        const yarg_read_ahead_config& config, int* bassError,
        TimestampProvider timestampProvider = nullptr) noexcept;

    ~ReadAheadStream();
    ReadAheadStream(const ReadAheadStream&) = delete;
    ReadAheadStream& operator=(const ReadAheadStream&) = delete;

    std::uint32_t streamHandle() const noexcept { return streamHandle_; }
    int setCallbackClockEnabled(bool enabled) noexcept;
    int prefill(std::uint32_t timeoutMilliseconds) noexcept;
    int flush() noexcept;
    int setBufferLength(std::uint32_t bufferMilliseconds) noexcept;
    std::int64_t getSourcePosition(std::uint32_t source,
        std::uint32_t endpointDelayFrames, int& error) noexcept;
    int getPositionSnapshot(std::uint32_t source,
        std::uint32_t endpointDelayFrames,
        yarg_read_ahead_position_snapshot& snapshot) noexcept;
    int getStats(yarg_read_ahead_stats& stats) noexcept;
    bool destroy(int* bassError) noexcept;

private:
    class BassAudioSource;

    ReadAheadStream(BassCoreBindings& bass, BassMixBindings& bassMix,
        const yarg_read_ahead_config& config,
        TimestampProvider timestampProvider) noexcept;
    static std::uint32_t YARG_BASS_CALLBACK streamCallback(
        std::uint32_t stream, void* buffer, std::uint32_t length,
        void* user) noexcept;
    std::uint32_t read(void* buffer, std::uint32_t length) noexcept;
    bool createRenderer(std::uint32_t bufferMilliseconds) noexcept;
    void closeConsumer() noexcept;
    void resetPlaybackState() noexcept;
    void updatePlaybackClock(std::size_t consumed,
        std::int64_t timestamp) noexcept;
    void recordConsumption(std::size_t consumed, std::size_t requested) noexcept;
    void recordCallbackTiming(std::uint32_t frames, std::int64_t timestamp,
        std::int64_t previousTimestamp) noexcept;
    void setPlaybackClockRate(std::int64_t ratePpm,
        std::int64_t timestamp) noexcept;
    std::uint64_t playbackClockElapsedFrames(std::int64_t timestamp,
        std::int64_t clockTimestamp) const noexcept;
    std::uint32_t remainingPlaybackDelayFrames(std::uint32_t endpointDelayFrames,
        std::int64_t timestamp) noexcept;
    bool playbackClockExpired(std::uint64_t submittedFrames,
        std::uint32_t callbackFrames, std::int64_t timestamp) const noexcept;
    void updateMinimum(std::uint32_t queued) noexcept;

    BassCoreBindings& bass_;
    BassMixBindings& bassMix_;
    const yarg_read_ahead_config config_;
    const TimestampProvider timestampProvider_;
    std::unique_ptr<RenderAheadMixer> renderer_;
    std::uint32_t streamHandle_ = 0;
    std::atomic<std::uint32_t> state_{YARG_READ_AHEAD_CREATED};
    std::atomic<int> lastError_{0};
    std::atomic<bool> consumerOpen_{false};
    std::atomic<std::uint32_t> activeConsumers_{0};
    std::atomic<std::uint64_t> consumerSequence_{0};
    std::atomic<std::int64_t> consumerTimestamp_{0};
    std::atomic<std::int64_t> playbackClockTimestamp_{0};
    std::atomic<std::uint64_t> playbackClockFixedFrames_{0};
    std::atomic<std::uint64_t> generationConsumedFrames_{0};
    std::atomic<std::uint64_t> lastHeardFrame_{0};
    std::atomic<bool> callbackClockEnabled_{true};
    std::atomic<std::int64_t> playbackClockRatePpm_{0};
    std::atomic<std::uint32_t> endpointDelayFrames_{UINT32_MAX};
    std::atomic<std::uint64_t> callbackTiming_{0};
    std::atomic<std::int64_t> callbackClockOriginTimestamp_{0};
    std::atomic<std::uint64_t> callbackClockOriginFrames_{0};
    std::atomic<std::int64_t> callbackClockOffsetFrames_{0};
    std::atomic<std::uint32_t> minimumQueuedFrames_{UINT32_MAX};
    std::atomic<std::uint64_t> consumedFrames_{0};
    std::atomic<std::uint64_t> requestedFrames_{0};
    std::atomic<std::uint64_t> underrunFrames_{0};
    std::atomic<std::uint64_t> underrunEvents_{0};
};

}
