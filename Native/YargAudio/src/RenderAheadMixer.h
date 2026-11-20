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
    virtual std::int64_t position(std::uint32_t sourceHandle,
        std::uint32_t delayBytes) noexcept = 0;
    virtual bool setAttribute(std::uint32_t, std::uint32_t, float) noexcept {
        return false;
    }
};

struct SourcePositionSnapshot {
    std::int64_t heardPosition;
    std::int64_t decodePosition;
    std::uint32_t totalDelayFrames;
};

enum class PositionRead {
    Failed,
    Succeeded,
    Busy,
};

class RenderAheadMixer {
public:
    static constexpr std::size_t RenderChunkFrames = 128;
    static constexpr std::size_t PositionHistorySize = 8192;
    static constexpr std::size_t MaximumQueuedAttributes = 8;
    static std::size_t bufferFrames(std::uint32_t sampleRate,
        std::uint32_t callbackFrames, std::uint32_t milliseconds);

    RenderAheadMixer(std::unique_ptr<IAudioSource> source, std::uint32_t sampleRate,
        std::uint32_t channels, std::uint32_t callbackFrames,
        std::uint32_t bufferMilliseconds);
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
    std::int64_t bufferedSourcePosition(std::uint32_t sourceHandle,
        std::uint32_t endpointDelayFrames) noexcept;
    template <typename DelayProvider>
    std::int64_t bufferedSourcePositionAfterLock(std::uint32_t sourceHandle,
        DelayProvider delayProvider) noexcept {
        NextRenderHold access{*this};
        std::lock_guard lock(sourceMutex_);
        return bufferedSourcePositionLocked(sourceHandle, delayProvider());
    }
    template <typename DelayProvider>
    bool sourcePositionSnapshotAfterLock(std::uint32_t sourceHandle,
        SourcePositionSnapshot& snapshot, DelayProvider delayProvider) noexcept {
        NextRenderHold access{*this};
        std::lock_guard lock(sourceMutex_);
        std::uint32_t delayFrames;
        if (!delayProvider(delayFrames)) {
            return false;
        }
        return sourcePositionSnapshotLocked(sourceHandle, delayFrames, snapshot);
    }
    template <typename DelayProvider>
    PositionRead trySourcePositionSnapshot(std::uint32_t sourceHandle,
        SourcePositionSnapshot& snapshot, DelayProvider delayProvider) noexcept {
        trackedSource_.store(sourceHandle, std::memory_order_release);
        std::unique_lock lock(sourceMutex_, std::try_to_lock);
        std::uint32_t delayFrames;
        if (!delayProvider(delayFrames)) {
            return PositionRead::Failed;
        }
        if (lock.owns_lock()) {
            return sourcePositionSnapshotLocked(sourceHandle, delayFrames, snapshot)
                ? PositionRead::Succeeded : PositionRead::Failed;
        }
        return sourcePositionSnapshotFromHistory(sourceHandle, delayFrames, snapshot)
            ? PositionRead::Succeeded : PositionRead::Busy;
    }

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
    std::uint64_t producedFrames() const noexcept { return producedFrames_.load(); }
    std::uint64_t maximumRenderNanoseconds() const noexcept { return maximumRenderNs_.load(); }
    std::uint64_t positionOutputFrame() const noexcept { return positionOutputFrame_.load(); }

private:
    struct NextRenderHold {
        RenderAheadMixer& mixer;
        explicit NextRenderHold(RenderAheadMixer& owner) noexcept : mixer(owner) {
            mixer.holdNextRender();
        }
        ~NextRenderHold() { mixer.releaseNextRender(); }
        NextRenderHold(const NextRenderHold&) = delete;
        NextRenderHold& operator=(const NextRenderHold&) = delete;
    };

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
    void recordSourcePosition() noexcept;
    void applyQueuedAttributes() noexcept;
    void resetPositionHistory() noexcept;
    bool readPositionRecord(std::uint64_t index, PositionRecord& record) const noexcept;
    bool sourcePositionSnapshotFromHistory(std::uint32_t sourceHandle,
        std::uint32_t endpointDelayFrames, SourcePositionSnapshot& snapshot) noexcept;
    std::int64_t bufferedSourcePositionLocked(std::uint32_t sourceHandle,
        std::uint32_t endpointDelayFrames) noexcept;
    bool sourcePositionSnapshotLocked(std::uint32_t sourceHandle,
        std::uint32_t endpointDelayFrames, SourcePositionSnapshot& snapshot) noexcept;
    void run() noexcept;
    void updateMaximum(std::uint64_t nanoseconds) noexcept;
    std::size_t lowWatermarkFrames() const noexcept;

    std::unique_ptr<IAudioSource> source_;
    const std::uint32_t channels_;
    std::atomic<std::size_t> targetFrames_;
    AudioRingBuffer ring_;
    std::vector<float> scratch_;
    std::mutex mutex_;
    std::mutex sourceMutex_;
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
    std::unique_ptr<PositionRecordSlot[]> positionHistory_;
    std::atomic<std::uint64_t> positionHistoryStart_{0};
    std::atomic<std::uint64_t> positionHistoryCount_{0};
    std::atomic<std::uint32_t> positionHistorySource_{0};
    std::atomic<std::uint32_t> trackedSource_{0};
    std::mutex attributeMutex_;
    std::array<AttributeWrite, MaximumQueuedAttributes> queuedAttributes_{};
    std::size_t queuedAttributeCount_ = 0;
    std::atomic<bool> attributesQueued_{false};
    std::atomic<std::uint64_t> maximumRenderNs_{0};
    std::atomic<std::uint64_t> positionOutputFrame_{0};
    std::atomic<std::uint32_t> nextRenderHolds_{0};
};

} // namespace yarg::audio
