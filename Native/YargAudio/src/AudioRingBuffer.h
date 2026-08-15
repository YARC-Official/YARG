#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace yarg::audio {

/**
 * Stores interleaved audio between a producer and a consumer without blocking either one.
 */
class AudioRingBuffer {
public:
    AudioRingBuffer(std::size_t capacityFrames, std::size_t channels);

    std::size_t write(const float* samples, std::size_t frames) noexcept;
    std::size_t read(float* samples, std::size_t frames) noexcept;
    std::size_t available() const noexcept;
    std::size_t freeSpace() const noexcept;
    std::size_t capacity() const noexcept { return capacityFrames_; }

    // Call only while consumer is stopped.
    void clear() noexcept;

private:
    const std::size_t capacityFrames_;
    const std::size_t channels_;
    std::vector<float> samples_;
    alignas(64) std::atomic<std::uint64_t> writeFrame_{0};
    alignas(64) std::atomic<std::uint64_t> readFrame_{0};
};

} // namespace yarg::audio
