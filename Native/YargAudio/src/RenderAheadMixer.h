#pragma once

#include "AudioRingBuffer.h"

#include <array>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

namespace yarg::audio {

class IAudioSource {
public:
    virtual ~IAudioSource() = default;
    virtual bool prepareThread() noexcept = 0;
    virtual int read(float* samples, std::size_t frames) noexcept = 0;
    virtual int lastError() const noexcept = 0;
    virtual std::int64_t position(std::uint32_t sourceHandle) noexcept = 0;
    virtual bool setAttribute(std::uint32_t, std::uint32_t, float) noexcept {
        return false;
    }
};

struct SourcePositionSnapshot {
    std::int64_t heardPosition;
    std::int64_t decodePosition;
    std::uint32_t totalDelayFrames;
};

class RenderAheadMixer {
public:
    static constexpr std::size_t RenderChunkFrames = 128;
    static constexpr std::size_t MaximumQueuedAttributes = 8;
    static std::size_t bufferFrames(std::uint32_t sampleRate,
        std::uint32_t callbackFrames, std::uint32_t milliseconds);

    RenderAheadMixer(std::unique_ptr<IAudioSource> source, std::uint32_t sampleRate,
        std::uint32_t channels, std::uint32_t callbackFrames,
        std::uint32_t bufferMilliseconds, std::uint32_t positionSource);
    ~RenderAheadMixer();
    RenderAheadMixer(const RenderAheadMixer&) = delete;
    RenderAheadMixer& operator=(const RenderAheadMixer&) = delete;

    bool start();
    bool prefill(std::chrono::milliseconds timeout);
    void stop() noexcept;
    bool clear();
    bool setTargetFrames(std::size_t frames) noexcept;
    bool queueSourceAttribute(std::uint32_t channel, std::uint32_t attribute,
        float value) noexcept;
    std::size_t consume(float* samples, std::size_t frames) noexcept;
    bool sourcePositionSnapshot(std::uint32_t endpointDelayFrames,
        SourcePositionSnapshot& snapshot) noexcept;

    void holdNextRender() noexcept {
        nextRenderHolds_.fetch_add(1, std::memory_order_acq_rel);
    }
    void releaseNextRender() noexcept {
        nextRenderHolds_.fetch_sub(1, std::memory_order_acq_rel);
    }

    std::size_t queuedFrames() const noexcept { return ring_.available(); }
    std::size_t targetFrames() const noexcept { return targetFrames_.load(std::memory_order_acquire); }
    bool failed() const noexcept { return failed_.load(std::memory_order_acquire); }
    int lastError() const noexcept { return lastError_.load(std::memory_order_relaxed); }
    int positionError() const noexcept { return positionError_.load(std::memory_order_acquire); }
    std::uint64_t producedFrames() const noexcept { return producedFrames_.load(); }
    std::uint64_t maximumRenderNanoseconds() const noexcept { return maximumRenderNs_.load(); }
    std::uint64_t positionOutputFrame() const noexcept { return positionOutputFrame_.load(); }

private:
    struct PositionRecordSlot {
        std::atomic<std::uint64_t> sequence{0};
        std::atomic<std::uint64_t> frame{0};
        std::atomic<std::int64_t> position{0};
    };

    struct PositionRecord {
        std::uint64_t frame;
        std::int64_t position;
    };

    struct AttributeWrite {
        std::uint32_t channel;
        std::uint32_t attribute;
        float value;
    };

    void yieldWhileHeld() const noexcept;
    void recordSourcePosition(std::uint64_t frame) noexcept;
    void applyQueuedAttributes() noexcept;
    bool readPositionRecord(std::uint64_t index, PositionRecord& record) const noexcept;
    void run() noexcept;
    void updateMaximum(std::uint64_t nanoseconds) noexcept;
    std::size_t lowWatermarkFrames() const noexcept;

    std::unique_ptr<IAudioSource> source_;
    const std::uint32_t channels_;
    const std::uint32_t positionSource_;
    std::atomic<std::size_t> targetFrames_;
    AudioRingBuffer ring_;
    std::vector<float> scratch_;
    std::mutex mutex_;
    std::condition_variable wake_;
    std::condition_variable prefilled_;
    std::thread worker_;
    bool running_ = false;
    std::atomic<bool> stopping_{false};
    std::atomic<bool> failed_{false};
    std::atomic<int> lastError_{0};
    std::atomic<std::uint64_t> producedFrames_{0};
    std::atomic<std::uint64_t> generationProducedFrames_{0};
    std::atomic<std::uint64_t> generationConsumedFrames_{0};
    const std::size_t positionHistorySize_;
    std::unique_ptr<PositionRecordSlot[]> positionHistory_;
    std::atomic<std::uint64_t> positionHistoryStart_{0};
    std::atomic<std::uint64_t> positionHistoryCount_{0};
    std::atomic<int> positionError_{0};
    std::mutex attributeMutex_;
    std::array<AttributeWrite, MaximumQueuedAttributes> queuedAttributes_{};
    std::size_t queuedAttributeCount_ = 0;
    std::atomic<bool> attributesQueued_{false};
    std::atomic<std::uint64_t> maximumRenderNs_{0};
    std::atomic<std::uint64_t> positionOutputFrame_{0};
    std::atomic<std::uint32_t> nextRenderHolds_{0};
};

} // namespace yarg::audio
