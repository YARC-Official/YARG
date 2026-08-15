#pragma once

#include "BassCoreBindings.h"
#include "BassMixBindings.h"
#include "RenderAheadMixer.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>
#include <memory>

namespace yarg::audio {

class ReadAheadStream final {
public:
    static std::unique_ptr<ReadAheadStream> create(
        BassCoreBindings& bass, BassMixBindings& bassMix,
        const yarg_read_ahead_config& config, int* bassError) noexcept;

    ~ReadAheadStream();
    ReadAheadStream(const ReadAheadStream&) = delete;
    ReadAheadStream& operator=(const ReadAheadStream&) = delete;

    std::uint32_t streamHandle() const noexcept { return streamHandle_; }
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
        const yarg_read_ahead_config& config) noexcept;
    static std::uint32_t YARG_BASS_CALLBACK streamCallback(
        std::uint32_t stream, void* buffer, std::uint32_t length,
        void* user) noexcept;
    std::uint32_t read(void* buffer, std::uint32_t length) noexcept;
    bool createRenderer(std::uint32_t bufferMilliseconds) noexcept;
    void closeConsumer() noexcept;
    void resetConsumerClock() noexcept;
    void recordCallbackTiming(std::uint32_t frames, std::int64_t timestamp,
        std::int64_t previousTimestamp) noexcept;
    std::uint32_t remainingPlaybackDelayFrames(std::uint32_t endpointDelayFrames,
        std::int64_t timestamp) noexcept;
    bool playbackClockExpired(std::uint64_t submittedFrames,
        std::uint32_t callbackFrames, std::int64_t timestamp) const noexcept;
    void updateMinimum(std::uint32_t queued) noexcept;

    BassCoreBindings& bass_;
    BassMixBindings& bassMix_;
    const yarg_read_ahead_config config_;
    std::unique_ptr<RenderAheadMixer> renderer_;
    std::uint32_t streamHandle_ = 0;
    std::atomic<std::uint32_t> state_{YARG_READ_AHEAD_CREATED};
    std::atomic<int> lastError_{0};
    std::atomic<bool> consumerOpen_{false};
    std::atomic<std::uint32_t> activeConsumers_{0};
    std::atomic<std::uint64_t> consumerSequence_{0};
    std::atomic<std::int64_t> consumerTimestamp_{0};
    std::atomic<std::int64_t> playbackClockTimestamp_{0};
    std::atomic<std::uint64_t> playbackClockFrames_{0};
    std::atomic<std::uint64_t> generationConsumedFrames_{0};
    std::atomic<std::uint64_t> lastHeardFrame_{0};
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
