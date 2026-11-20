#include "RenderAheadMixer.h"

#include <algorithm>
#include <cmath>

#if defined(_WIN32)
#include <windows.h>
#endif

namespace yarg::audio {
namespace {

constexpr int BassErrorParameter = 20;
constexpr int BassErrorNotAvailable = 37;
constexpr std::uint32_t MAXIMUM_BUFFER_MILLISECONDS = 5000;

} // namespace

std::size_t RenderAheadMixer::bufferFrames(std::uint32_t sampleRate, std::uint32_t callbackFrames,
    std::uint32_t milliseconds) {
    const auto configured = static_cast<std::size_t>(
        std::ceil(static_cast<double>(sampleRate) * milliseconds / 1000.0));
    return std::max(configured, static_cast<std::size_t>(callbackFrames) * 2);
}

RenderAheadMixer::RenderAheadMixer(std::unique_ptr<IAudioSource> source,
    std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t callbackFrames,
    std::uint32_t bufferMilliseconds)
    : source_(std::move(source)), channels_(channels),
      targetFrames_(bufferFrames(sampleRate, callbackFrames, bufferMilliseconds)),
      ring_(bufferFrames(sampleRate, callbackFrames,
          std::max(bufferMilliseconds, MAXIMUM_BUFFER_MILLISECONDS)) + RenderChunkFrames * 2, channels),
      scratch_(RenderChunkFrames * channels),
      positionHistory_(std::make_unique<PositionRecordSlot[]>(PositionHistorySize)) {
}

RenderAheadMixer::~RenderAheadMixer() { stop(); }

bool RenderAheadMixer::start() {
    std::lock_guard lock(mutex_);
    if (running_) return true;
    stopping_.store(false, std::memory_order_release);
    failed_.store(false, std::memory_order_release);
    running_ = true;
    try {
        worker_ = std::thread(&RenderAheadMixer::run, this);
    } catch (...) {
        running_ = false;
        lastError_.store(-1);
        return false;
    }
    return true;
}

bool RenderAheadMixer::prefill(std::chrono::milliseconds timeout) {
    if (!start()) return false;
    std::unique_lock lock(mutex_);
    wake_.notify_one();
    return prefilled_.wait_for(lock, timeout, [this] {
        return ring_.available() >= targetFrames() || failed_.load() ||
            stopping_.load();
    }) && ring_.available() >= targetFrames();
}

void RenderAheadMixer::stop() noexcept {
    {
        std::lock_guard lock(mutex_);
        if (!running_) return;
        stopping_.store(true, std::memory_order_release);
    }
    wake_.notify_all();
    prefilled_.notify_all();
    if (worker_.joinable()) worker_.join();
    std::lock_guard lock(mutex_);
    running_ = false;
}

bool RenderAheadMixer::clear() {
    // Stop serializes with any in-flight source pull before indices move.
    stop();
    ring_.clear();
    generationProducedFrames_.store(0, std::memory_order_relaxed);
    generationConsumedFrames_.store(0, std::memory_order_relaxed);
    resetPositionHistory();
    positionOutputFrame_.store(0, std::memory_order_relaxed);
    return true;
}

bool RenderAheadMixer::setTargetFrames(std::size_t frames) noexcept {
    if (frames > ring_.capacity()) {
        return false;
    }

    {
        std::lock_guard lock(mutex_);
        targetFrames_.store(frames, std::memory_order_release);
    }
    wake_.notify_one();
    prefilled_.notify_all();
    return true;
}

std::size_t RenderAheadMixer::lowWatermarkFrames() const noexcept {
    const auto target = targetFrames();
    return target > RenderChunkFrames ? target - RenderChunkFrames : 0;
}

std::int64_t RenderAheadMixer::bufferedSourcePosition(
    std::uint32_t sourceHandle, std::uint32_t endpointDelayFrames) noexcept {
    NextRenderHold access{*this};
    std::lock_guard lock(sourceMutex_);
    return bufferedSourcePositionLocked(sourceHandle, endpointDelayFrames);
}

std::int64_t RenderAheadMixer::bufferedSourcePositionLocked(
    std::uint32_t sourceHandle, std::uint32_t endpointDelayFrames) noexcept {
    SourcePositionSnapshot snapshot{};
    return sourcePositionSnapshotLocked(sourceHandle, endpointDelayFrames, snapshot)
        ? snapshot.heardPosition : -1;
}

bool RenderAheadMixer::sourcePositionSnapshotLocked(
    std::uint32_t sourceHandle, std::uint32_t endpointDelayFrames,
    SourcePositionSnapshot& snapshot) noexcept {
    const auto totalDelayFrames = static_cast<std::uint64_t>(ring_.available()) +
        endpointDelayFrames;
    const auto maximumFrames = UINT32_MAX / (channels_ * sizeof(float));
    const auto delayFrames = std::min<std::uint64_t>(totalDelayFrames, maximumFrames);
    snapshot.totalDelayFrames = static_cast<std::uint32_t>(delayFrames);

    const auto produced = generationProducedFrames_.load(std::memory_order_relaxed);
    if (produced == 0) {
        snapshot.heardPosition = 0;
        snapshot.decodePosition = 0;
        return true;
    }

    const auto outputFrame = totalDelayFrames < produced ? produced - totalDelayFrames : 0;
    positionOutputFrame_.store(outputFrame, std::memory_order_relaxed);
    const auto delayBytes = static_cast<std::uint32_t>(
        delayFrames * channels_ * sizeof(float));

    snapshot.decodePosition = source_->position(sourceHandle, 0);
    if (snapshot.decodePosition < 0) {
        return false;
    }

    snapshot.heardPosition = source_->position(sourceHandle, delayBytes);
    if (snapshot.heardPosition >= 0) {
        return true;
    }

    const auto error = source_->lastError();
    if (totalDelayFrames >= produced &&
        (error == BassErrorParameter || error == BassErrorNotAvailable)) {
        snapshot.heardPosition = 0;
        return true;
    }

    return false;
}

std::size_t RenderAheadMixer::consume(float* samples, std::size_t frames) noexcept {
    const auto before = ring_.available();
    const auto consumed = ring_.read(samples, frames);
    generationConsumedFrames_.fetch_add(consumed, std::memory_order_release);
    const auto lowWatermark = lowWatermarkFrames();
    if (before >= lowWatermark && ring_.available() <= lowWatermark) {
        wake_.notify_one();
    }
    return consumed;
}

void RenderAheadMixer::run() noexcept {
#if defined(_WIN32)
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);
#endif
    if (!source_->prepareThread()) {
        lastError_.store(source_->lastError());
        failed_.store(true, std::memory_order_release);
        prefilled_.notify_all();
        return;
    }

    while (!stopping_.load(std::memory_order_acquire)) {
        if (ring_.available() >= targetFrames()) {
            prefilled_.notify_all();
            std::unique_lock lock(mutex_);
            wake_.wait(lock, [this] {
                return stopping_.load() || ring_.available() <= lowWatermarkFrames();
            });
            continue;
        }

        const auto freeFrames = ring_.freeSpace();
        const auto requested = std::min(RenderChunkFrames, freeFrames);
        const auto started = std::chrono::steady_clock::now();
        int rendered;
        {
            std::lock_guard sourceLock(sourceMutex_);
            rendered = source_->read(scratch_.data(), requested);
            if (rendered > 0) {
                const auto frames = std::min<std::size_t>(rendered, requested);
                const auto written = ring_.write(scratch_.data(), frames);
                producedFrames_.fetch_add(written, std::memory_order_relaxed);
                generationProducedFrames_.fetch_add(written, std::memory_order_relaxed);
                recordSourcePosition();
            }
        }
        const auto elapsed = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - started).count();
        updateMaximum(static_cast<std::uint64_t>(elapsed));
        yieldWhileHeld();

        if (rendered < 0) {
            lastError_.store(source_->lastError());
            failed_.store(true, std::memory_order_release);
            prefilled_.notify_all();
            return;
        }
        if (rendered == 0) {
            std::unique_lock lock(mutex_);
            wake_.wait_for(lock, std::chrono::milliseconds(1));
            continue;
        }

        if (ring_.available() >= targetFrames()) prefilled_.notify_all();
    }
}

void RenderAheadMixer::recordSourcePosition() noexcept {
    const auto source = trackedSource_.load(std::memory_order_acquire);
    if (source == 0) return;

    const auto count = positionHistoryCount_.load(std::memory_order_relaxed);
    if (source != positionHistorySource_.load(std::memory_order_relaxed)) {
        positionHistoryStart_.store(count, std::memory_order_release);
        positionHistorySource_.store(source, std::memory_order_release);
    }

    const auto position = source_->position(source, 0);
    if (position < 0) {
        positionHistoryStart_.store(count, std::memory_order_release);
        return;
    }

    auto& slot = positionHistory_[count % PositionHistorySize];
    slot.sequence.store(0, std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_release);
    slot.frame.store(generationProducedFrames_.load(std::memory_order_relaxed),
        std::memory_order_relaxed);
    slot.position.store(position, std::memory_order_relaxed);
    slot.sequence.store(count + 1, std::memory_order_release);
    positionHistoryCount_.store(count + 1, std::memory_order_release);
}

void RenderAheadMixer::resetPositionHistory() noexcept {
    positionHistoryStart_.store(positionHistoryCount_.load(std::memory_order_relaxed),
        std::memory_order_release);
}

bool RenderAheadMixer::readPositionRecord(std::uint64_t index,
    PositionRecord& record) const noexcept {
    const auto& slot = positionHistory_[index % PositionHistorySize];
    const auto before = slot.sequence.load(std::memory_order_acquire);
    record.frame = slot.frame.load(std::memory_order_relaxed);
    record.position = slot.position.load(std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_acquire);
    const auto after = slot.sequence.load(std::memory_order_relaxed);
    return before == index + 1 && after == before;
}

bool RenderAheadMixer::sourcePositionSnapshotFromHistory(std::uint32_t sourceHandle,
    std::uint32_t endpointDelayFrames, SourcePositionSnapshot& snapshot) noexcept {
    // Answers from positions the worker recorded after each chunk, so readers
    // never wait for a chunk that is still rendering.
    if (positionHistorySource_.load(std::memory_order_acquire) != sourceHandle) {
        return false;
    }

    const auto count = positionHistoryCount_.load(std::memory_order_acquire);
    const auto start = positionHistoryStart_.load(std::memory_order_acquire);
    if (count <= start) return false;
    const auto oldest = std::max(start,
        count > PositionHistorySize ? count - PositionHistorySize : 0);

    PositionRecord latest{};
    if (!readPositionRecord(count - 1, latest)) return false;
    const auto consumed = generationConsumedFrames_.load(std::memory_order_acquire);
    if (consumed > latest.frame) return false;

    const auto totalDelayFrames = latest.frame - consumed + endpointDelayFrames;
    if (totalDelayFrames >= latest.frame) return false;
    const auto outputFrame = latest.frame - totalDelayFrames;

    auto low = oldest;
    auto high = count - 1;
    PositionRecord before{};
    if (!readPositionRecord(low, before) || before.frame > outputFrame) return false;
    while (low < high) {
        const auto middle = low + (high - low + 1) / 2;
        PositionRecord record{};
        if (!readPositionRecord(middle, record)) return false;
        if (record.frame <= outputFrame) {
            low = middle;
            before = record;
        } else {
            high = middle - 1;
        }
    }

    auto heardPosition = before.position;
    if (before.frame < outputFrame) {
        PositionRecord after{};
        if (low + 1 >= count || !readPositionRecord(low + 1, after)) return false;
        if (after.position > before.position) {
            heardPosition += static_cast<std::int64_t>(
                static_cast<double>(after.position - before.position) *
                static_cast<double>(outputFrame - before.frame) /
                static_cast<double>(after.frame - before.frame));
        }
    }

    const auto maximumFrames = UINT32_MAX / (channels_ * sizeof(float));
    snapshot.totalDelayFrames = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(totalDelayFrames, maximumFrames));
    snapshot.decodePosition = latest.position;
    snapshot.heardPosition = heardPosition;
    positionOutputFrame_.store(outputFrame, std::memory_order_relaxed);
    return true;
}

void RenderAheadMixer::yieldWhileHeld() const noexcept {
    while (nextRenderHolds_.load(std::memory_order_acquire) != 0 &&
        !stopping_.load(std::memory_order_acquire)) {
        std::this_thread::yield();
    }
}

void RenderAheadMixer::updateMaximum(std::uint64_t nanoseconds) noexcept {
    auto previous = maximumRenderNs_.load(std::memory_order_relaxed);
    while (previous < nanoseconds && !maximumRenderNs_.compare_exchange_weak(
        previous, nanoseconds, std::memory_order_relaxed)) {
    }
}

} // namespace yarg::audio
