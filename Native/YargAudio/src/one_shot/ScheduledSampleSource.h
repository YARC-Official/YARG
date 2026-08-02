#pragma once

#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <vector>

namespace yarg::audio {

class ScheduledSampleSource final {
public:
    static constexpr std::size_t MaxActiveVoices = 64;

    // Copies pcm and schedule. The returned source is ready for render calls.
    static std::unique_ptr<ScheduledSampleSource> create(
        std::uint32_t sampleRate, std::uint32_t channels,
        const float* pcm, std::size_t pcmSampleCount,
        const double* schedule, std::size_t scheduleCount,
        double leadTime) noexcept;

    ScheduledSampleSource(const ScheduledSampleSource&) = delete;
    ScheduledSampleSource& operator=(const ScheduledSampleSource&) = delete;
    ScheduledSampleSource(ScheduledSampleSource&&) = delete;
    ScheduledSampleSource& operator=(ScheduledSampleSource&&) = delete;
    ~ScheduledSampleSource() = default;

    // Control operations must not run concurrently with render/reset state changes.
    // The BASS mixer lock provides that exclusion in the native integration.
    bool reset(double anchorSongPosition, float playbackSpeed, bool paused,
        bool clearActiveVoices) noexcept;
    void setPaused(bool paused) noexcept;
    bool setGain(float gain) noexcept;

    // Clears and fills interleaved float output. Never allocates or locks.
    void render(float* output, std::size_t outputFrames) noexcept;

    std::uint32_t channels() const noexcept { return channels_; }
    std::uint32_t sampleRate() const noexcept { return sampleRate_; }
    std::size_t sampleFrameCount() const noexcept { return sampleFrameCount_; }
    std::size_t activeVoiceCount() const noexcept { return activeVoiceCount_; }
    std::uint64_t droppedVoiceCount() const noexcept { return droppedVoiceCount_; }
    std::int64_t cursorFrame() const noexcept { return cursorFrame_; }

private:
    struct Voice {
        std::size_t sampleFrame = 0;
    };

    ScheduledSampleSource(std::uint32_t sampleRate, std::uint32_t channels,
        std::vector<float>&& pcm, std::vector<double>&& schedule,
        double leadTime) noexcept;

    void clearOutput(float* output, std::size_t outputFrames) const noexcept;
    void mixActiveVoices(float* output, std::size_t outputFrames,
        float gain) noexcept;
    void startScheduledVoices(float* output, std::size_t outputFrames,
        std::int64_t bufferStartFrame, std::int64_t bufferEndFrame,
        float gain) noexcept;
    void startVoice(float* output, std::size_t outputFrames,
        std::size_t outputFrameOffset, float gain) noexcept;
    void mixVoice(float* output, std::size_t outputFrames,
        std::size_t outputFrameOffset, Voice& voice, float gain) const noexcept;
    std::size_t findNextSchedule() const noexcept;
    std::int64_t targetFrame(double scheduledSongPosition) const noexcept;

    std::uint32_t sampleRate_;
    std::uint32_t channels_;
    std::size_t sampleFrameCount_;
    double leadTime_;
    std::vector<float> pcm_;
    std::vector<double> schedule_;
    std::array<Voice, MaxActiveVoices> voices_{};

    std::size_t activeVoiceCount_ = 0;
    std::size_t nextScheduledVoice_ = 0;
    std::uint64_t droppedVoiceCount_ = 0;
    std::int64_t cursorFrame_ = 0;
    double anchorSongPosition_ = 0;
    float playbackSpeed_ = 1;

    std::atomic<std::uint32_t> gainBits_;
    std::atomic<std::uint32_t> pausedBits_{0};
};

} // namespace yarg::audio
