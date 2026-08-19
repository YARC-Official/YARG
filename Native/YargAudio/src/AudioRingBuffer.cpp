#include "AudioRingBuffer.h"

#include <algorithm>
#include <cstring>
#include <stdexcept>

namespace yarg::audio {

AudioRingBuffer::AudioRingBuffer(std::size_t capacityFrames, std::size_t channels)
    : capacityFrames_(capacityFrames), channels_(channels),
      samples_(capacityFrames * channels) {
    if (capacityFrames == 0 || channels == 0) {
        throw std::invalid_argument("ring dimensions must be nonzero");
    }
}

std::size_t AudioRingBuffer::available() const noexcept {
    const auto write = writeFrame_.load(std::memory_order_acquire);
    const auto read = readFrame_.load(std::memory_order_acquire);
    const auto difference = write >= read ? write - read : 0;
    return std::min(capacityFrames_, static_cast<std::size_t>(difference));
}

std::size_t AudioRingBuffer::freeSpace() const noexcept {
    return capacityFrames_ - available();
}

std::size_t AudioRingBuffer::write(const float* samples, std::size_t frames) noexcept {
    const auto write = writeFrame_.load(std::memory_order_relaxed);
    const auto read = readFrame_.load(std::memory_order_acquire);
    const auto used = write >= read
        ? std::min<std::uint64_t>(write - read, capacityFrames_) : 0;
    const auto count = std::min(frames,
        capacityFrames_ - static_cast<std::size_t>(used));
    const auto firstFrame = static_cast<std::size_t>(write % capacityFrames_);
    const auto firstCount = std::min(count, capacityFrames_ - firstFrame);

    std::memcpy(samples_.data() + firstFrame * channels_, samples,
        firstCount * channels_ * sizeof(float));
    std::memcpy(samples_.data(), samples + firstCount * channels_,
        (count - firstCount) * channels_ * sizeof(float));
    writeFrame_.store(write + count, std::memory_order_release);
    return count;
}

std::size_t AudioRingBuffer::read(float* samples, std::size_t frames) noexcept {
    const auto read = readFrame_.load(std::memory_order_relaxed);
    const auto write = writeFrame_.load(std::memory_order_acquire);
    const auto available = write >= read ? write - read : 0;
    const auto count = std::min(frames, static_cast<std::size_t>(available));
    const auto firstFrame = static_cast<std::size_t>(read % capacityFrames_);
    const auto firstCount = std::min(count, capacityFrames_ - firstFrame);

    std::memcpy(samples, samples_.data() + firstFrame * channels_,
        firstCount * channels_ * sizeof(float));
    std::memcpy(samples + firstCount * channels_, samples_.data(),
        (count - firstCount) * channels_ * sizeof(float));
    readFrame_.store(read + count, std::memory_order_release);
    return count;
}

void AudioRingBuffer::clear() noexcept {
    const auto write = writeFrame_.load(std::memory_order_acquire);
    readFrame_.store(write, std::memory_order_release);
}

} // namespace yarg::audio
