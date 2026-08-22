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

std::size_t computeTargetFrames(std::uint32_t sampleRate, std::uint32_t callbackFrames,
    std::uint32_t milliseconds) {
    const auto configured = static_cast<std::size_t>(
        std::ceil(static_cast<double>(sampleRate) * milliseconds / 1000.0));
    return std::max(configured, static_cast<std::size_t>(callbackFrames) * 2);
}

} // namespace

RenderAheadMixer::RenderAheadMixer(std::unique_ptr<IAudioSource> source,
    std::uint32_t sampleRate, std::uint32_t channels, std::uint32_t callbackFrames,
    std::uint32_t bufferMilliseconds)
    : source_(std::move(source)), channels_(channels),
      targetFrames_(computeTargetFrames(sampleRate, callbackFrames, bufferMilliseconds)),
      lowWatermarkFrames_(targetFrames_ > RenderChunkFrames
          ? targetFrames_ - RenderChunkFrames : 0),
      ring_(targetFrames_ + RenderChunkFrames * 2, channels),
      scratch_(RenderChunkFrames * channels) {
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
        return ring_.available() >= targetFrames_ || failed_.load() ||
            stopping_.load();
    }) && ring_.available() >= targetFrames_;
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
    positionOutputFrame_.store(0, std::memory_order_relaxed);
    return true;
}

std::int64_t RenderAheadMixer::bufferedSourcePosition(
    std::uint32_t sourceHandle, std::uint32_t endpointDelayFrames) noexcept {
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
    if (before >= lowWatermarkFrames_ && ring_.available() <= lowWatermarkFrames_) {
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
        if (ring_.available() >= targetFrames_) {
            prefilled_.notify_all();
            std::unique_lock lock(mutex_);
            wake_.wait(lock, [this] {
                return stopping_.load() || ring_.available() <= lowWatermarkFrames_;
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
            }
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

        if (ring_.available() >= targetFrames_) prefilled_.notify_all();
    }
}

void RenderAheadMixer::updateMaximum(std::uint64_t nanoseconds) noexcept {
    auto previous = maximumRenderNs_.load(std::memory_order_relaxed);
    while (previous < nanoseconds && !maximumRenderNs_.compare_exchange_weak(
        previous, nanoseconds, std::memory_order_relaxed)) {
    }
}

} // namespace yarg::audio
