#include "RenderAheadMixer.h"

#include <algorithm>
#include <cmath>

#if defined(_WIN32)
#include <windows.h>
#endif

namespace yarg::audio {
namespace {

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
    std::uint32_t bufferMilliseconds, std::uint32_t positionSource)
    : source_(std::move(source)), channels_(channels), positionSource_(positionSource),
      targetFrames_(bufferFrames(sampleRate, callbackFrames, bufferMilliseconds)),
      ring_(bufferFrames(sampleRate, callbackFrames,
          std::max(bufferMilliseconds, MAXIMUM_BUFFER_MILLISECONDS)) + RenderChunkFrames * 2, channels),
      scratch_(RenderChunkFrames * channels),
      positionHistorySize_(ring_.capacity() / RenderChunkFrames * 2 + 1024),
      positionHistory_(std::make_unique<PositionRecordSlot[]>(positionHistorySize_)) {
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
    bool wasRunning;
    {
        std::lock_guard lock(mutex_);
        wasRunning = running_;
        if (wasRunning) stopping_.store(true, std::memory_order_release);
    }
    if (wasRunning) {
        wake_.notify_all();
        prefilled_.notify_all();
        if (worker_.joinable()) worker_.join();
        std::lock_guard lock(mutex_);
        running_ = false;
    }
    applyQueuedAttributes();
}

bool RenderAheadMixer::clear() {
    // Stop serializes with any in-flight source pull before indices move.
    stop();
    ring_.clear();
    generationProducedFrames_.store(0, std::memory_order_relaxed);
    generationConsumedFrames_.store(0, std::memory_order_relaxed);
    positionHistoryStart_.store(positionHistoryCount_.load(std::memory_order_relaxed),
        std::memory_order_release);
    positionError_.store(0, std::memory_order_relaxed);
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

bool RenderAheadMixer::queueSourceAttribute(std::uint32_t channel,
    std::uint32_t attribute, float value) noexcept {
    std::lock_guard lock(attributeMutex_);
    for (std::size_t i = 0; i < queuedAttributeCount_; ++i) {
        auto& write = queuedAttributes_[i];
        if (write.channel == channel && write.attribute == attribute) {
            write.value = value;
            return true;
        }
    }
    if (queuedAttributeCount_ == queuedAttributes_.size()) {
        return false;
    }
    queuedAttributes_[queuedAttributeCount_++] = AttributeWrite{channel, attribute, value};
    return true;
}

void RenderAheadMixer::applyQueuedAttributes() noexcept {
    std::array<AttributeWrite, MaximumQueuedAttributes> writes;
    std::size_t count;
    {
        std::lock_guard lock(attributeMutex_);
        writes = queuedAttributes_;
        count = queuedAttributeCount_;
        queuedAttributeCount_ = 0;
    }
    for (std::size_t i = 0; i < count; ++i) {
        source_->setAttribute(writes[i].channel, writes[i].attribute, writes[i].value);
    }
}

std::size_t RenderAheadMixer::lowWatermarkFrames() const noexcept {
    const auto target = targetFrames();
    return target > RenderChunkFrames ? target - RenderChunkFrames : 0;
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
        applyQueuedAttributes();
        const auto produced = generationProducedFrames_.load(std::memory_order_relaxed);
        if (produced == 0 && positionHistoryCount_.load(std::memory_order_relaxed) ==
            positionHistoryStart_.load(std::memory_order_relaxed)) {
            recordSourcePosition(0);
        }
        const int rendered = source_->read(scratch_.data(), requested);
        if (rendered > 0) {
            const auto frames = std::min<std::size_t>(rendered, requested);
            recordSourcePosition(produced + frames);
            const auto written = ring_.write(scratch_.data(), frames);
            producedFrames_.fetch_add(written, std::memory_order_relaxed);
            generationProducedFrames_.fetch_add(written, std::memory_order_release);
        }
        const auto elapsed = std::chrono::duration_cast<std::chrono::nanoseconds>(
            std::chrono::steady_clock::now() - started).count();
        updateMaximum(static_cast<std::uint64_t>(elapsed));

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

void RenderAheadMixer::recordSourcePosition(std::uint64_t frame) noexcept {
    const auto position = source_->position(positionSource_);
    if (position < 0) {
        const auto error = source_->lastError();
        positionError_.store(error != 0 ? error : -1, std::memory_order_release);
        return;
    }

    const auto count = positionHistoryCount_.load(std::memory_order_relaxed);
    auto& slot = positionHistory_[count % positionHistorySize_];
    slot.sequence.store(0, std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_release);
    slot.frame.store(frame, std::memory_order_relaxed);
    slot.position.store(position, std::memory_order_relaxed);
    slot.sequence.store(count + 1, std::memory_order_release);
    positionHistoryCount_.store(count + 1, std::memory_order_release);
    positionError_.store(0, std::memory_order_release);
}

bool RenderAheadMixer::readPositionRecord(std::uint64_t index,
    PositionRecord& record) const noexcept {
    const auto& slot = positionHistory_[index % positionHistorySize_];
    const auto before = slot.sequence.load(std::memory_order_acquire);
    record.frame = slot.frame.load(std::memory_order_relaxed);
    record.position = slot.position.load(std::memory_order_relaxed);
    std::atomic_thread_fence(std::memory_order_acquire);
    const auto after = slot.sequence.load(std::memory_order_relaxed);
    return before == index + 1 && after == before;
}

bool RenderAheadMixer::sourcePositionSnapshot(std::uint32_t endpointDelayFrames,
    SourcePositionSnapshot& snapshot) noexcept {
    const auto maximumFrames = UINT32_MAX / (channels_ * sizeof(float));
    if (generationProducedFrames_.load(std::memory_order_acquire) == 0) {
        snapshot.totalDelayFrames = static_cast<std::uint32_t>(
            std::min<std::uint64_t>(endpointDelayFrames, maximumFrames));
        snapshot.heardPosition = 0;
        snapshot.decodePosition = 0;
        return true;
    }
    if (positionError_.load(std::memory_order_acquire) != 0) return false;

    const auto consumed = generationConsumedFrames_.load(std::memory_order_acquire);
    const auto count = positionHistoryCount_.load(std::memory_order_acquire);
    const auto start = positionHistoryStart_.load(std::memory_order_acquire);
    if (count <= start) return false;
    const auto oldest = std::max(start,
        count > positionHistorySize_ ? count - positionHistorySize_ + 1 : 0);

    PositionRecord latest{};
    if (!readPositionRecord(count - 1, latest) || consumed > latest.frame) return false;
    const auto totalDelayFrames = latest.frame - consumed + endpointDelayFrames;
    const auto outputFrame = totalDelayFrames < latest.frame ? latest.frame - totalDelayFrames : 0;

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

    snapshot.totalDelayFrames = static_cast<std::uint32_t>(
        std::min<std::uint64_t>(totalDelayFrames, maximumFrames));
    snapshot.decodePosition = latest.position;
    snapshot.heardPosition = heardPosition;
    positionOutputFrame_.store(outputFrame, std::memory_order_relaxed);
    return true;
}

void RenderAheadMixer::updateMaximum(std::uint64_t nanoseconds) noexcept {
    auto previous = maximumRenderNs_.load(std::memory_order_relaxed);
    while (previous < nanoseconds && !maximumRenderNs_.compare_exchange_weak(
        previous, nanoseconds, std::memory_order_relaxed)) {
    }
}

} // namespace yarg::audio
