#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace yarg::audio {

class PitchShifter {
public:
    static constexpr float MIN_RATIO = 0.5f;
    static constexpr float MAX_RATIO = 2.0f;
    static constexpr std::uint32_t MAX_CHANNELS = 32;

    static std::uint32_t latencyFrames(std::uint32_t sampleRate) noexcept;

    bool configure(std::uint32_t channels, std::uint32_t sampleRate) noexcept;
    void reset() noexcept;
    void setRatio(float ratio) noexcept;
    void process(float* interleaved, std::size_t frames) noexcept;

    std::uint32_t latency() const noexcept { return nominalDelay_; }
    bool settled() const noexcept;
    double currentDelay() const noexcept { return delay_; }
    std::uint64_t spliceCount() const noexcept { return spliceCount_; }

private:
    enum class SpliceKind { Down, Up, Return };

    void writeRing(const float* input, std::size_t frames) noexcept;
    void copyDelayed(float* output, std::size_t frames) noexcept;
    void processShifting(float* output, std::size_t frames) noexcept;
    void readTap(std::uint64_t frame, double delay, float gain, float* output) const noexcept;
    void maybeStartSplice(std::uint64_t nextFrame) noexcept;
    void startSplice(std::uint64_t nextFrame, std::int64_t lagLow, std::int64_t lagHigh,
        std::int64_t fallbackLag, bool force) noexcept;
    std::int64_t findBestLag(std::int64_t start, std::size_t length, std::int64_t lagLow,
        std::int64_t lagHigh, std::int64_t fallbackLag, float& correlation) noexcept;
    float mono(std::int64_t frame) const noexcept;

    std::uint32_t channels_ = 0;
    std::uint32_t sampleRate_ = 0;
    std::uint32_t nominalDelay_ = 0;
    std::uint32_t fadeFrames_ = 0;
    std::uint32_t halfJump_ = 0;
    std::uint32_t halfSearch_ = 0;
    std::uint32_t returnSearch_ = 0;
    std::uint32_t retryFrames_ = 0;
    std::uint32_t retryWindow_ = 0;
    std::uint32_t retryCountdown_ = 0;
    std::uint32_t decimation_ = 1;
    std::size_t chunkFrames_ = 0;
    std::uint64_t ringMask_ = 0;
    std::vector<float> ring_;
    std::vector<float> fadeLinear_;
    std::vector<float> fadePower_;
    std::vector<float> scratchA_;
    std::vector<float> scratchB_;

    std::uint64_t written_ = 0;
    double smoothing_ = 1.0;
    double target_ = 1.0;
    double ratio_ = 1.0;
    double delay_ = 0.0;
    double nextDelay_ = 0.0;
    bool fading_ = false;
    std::uint32_t fadePosition_ = 0;
    float fadeShape_ = 0.0f;
    std::uint64_t spliceCount_ = 0;
};

} // namespace yarg::audio
