#include "one_shot/ScheduledSampleSource.h"

#include "BitCastCompat.h"

#include <algorithm>
#include <bit>
#include <cmath>
#include <limits>
#include <new>
#include <utility>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

namespace yarg::audio {
namespace {

bool validSchedule(const double* schedule, std::size_t count) noexcept {
    if (count > 0 && !schedule) return false;
    for (std::size_t i = 0; i < count; ++i) {
        if (!std::isfinite(schedule[i])) return false;
        if (i > 0 && schedule[i] < schedule[i - 1]) return false;
    }
    return true;
}

std::int64_t clampFrame(double frame) noexcept {
    constexpr auto minimum = std::numeric_limits<std::int64_t>::min();
    constexpr auto maximum = std::numeric_limits<std::int64_t>::max();
    const double lower = std::floor(frame);
    const double fraction = frame - lower;
    const double rounded = fraction < 0.5
        ? lower
        : fraction > 0.5
            ? lower + 1
            : std::fmod(std::abs(lower), 2.0) == 0 ? lower : lower + 1;
    if (!std::isfinite(rounded)) {
        return rounded < 0 ? minimum : maximum;
    }
    if (rounded <= static_cast<double>(minimum)) return minimum;
    if (rounded >= static_cast<double>(maximum)) return maximum;
    return static_cast<std::int64_t>(rounded);
}

} // namespace

std::unique_ptr<ScheduledSampleSource> ScheduledSampleSource::create(
    std::uint32_t sampleRate, std::uint32_t channels,
    const float* pcm, std::size_t pcmSampleCount,
    const double* schedule, std::size_t scheduleCount,
    double leadTime) noexcept {
    if (sampleRate == 0 || channels == 0 || !pcm || pcmSampleCount == 0 ||
        pcmSampleCount % channels != 0 || !std::isfinite(leadTime) ||
        leadTime < 0 || !validSchedule(schedule, scheduleCount)) {
        return nullptr;
    }

    try {
        std::vector<float> pcmCopy(pcm, pcm + pcmSampleCount);
        std::vector<double> scheduleCopy;
        if (scheduleCount > 0) {
            scheduleCopy.assign(schedule, schedule + scheduleCount);
        }
        return std::unique_ptr<ScheduledSampleSource>(new ScheduledSampleSource(
            sampleRate, channels, std::move(pcmCopy), std::move(scheduleCopy), leadTime));
    } catch (...) {
        return nullptr;
    }
}

ScheduledSampleSource::ScheduledSampleSource(std::uint32_t sampleRate,
    std::uint32_t channels, std::vector<float>&& pcm,
    std::vector<double>&& schedule, double leadTime) noexcept
    : sampleRate_(sampleRate), channels_(channels),
      sampleFrameCount_(pcm.size() / channels), leadTime_(leadTime),
      pcm_(std::move(pcm)), schedule_(std::move(schedule)),
      gainBits_(yarg::audio::bitCast<std::uint32_t>(1.0f)) {}

bool ScheduledSampleSource::reset(double anchorSongPosition,
    float playbackSpeed, bool paused, bool clearActiveVoices) noexcept {
    if (!std::isfinite(anchorSongPosition) || !std::isfinite(playbackSpeed) ||
        playbackSpeed <= 0) {
        return false;
    }

    anchorSongPosition_ = anchorSongPosition;
    playbackSpeed_ = playbackSpeed;
    cursorFrame_ = 0;
    if (clearActiveVoices) activeVoiceCount_ = 0;
    nextScheduledVoice_ = findNextSchedule();
    droppedVoiceCount_ = 0;
    pausedBits_.store(paused ? 1u : 0u, std::memory_order_relaxed);
    return true;
}

void ScheduledSampleSource::setPaused(bool paused) noexcept {
    pausedBits_.store(paused ? 1u : 0u, std::memory_order_relaxed);
}

bool ScheduledSampleSource::setGain(float gain) noexcept {
    if (!std::isfinite(gain)) return false;
    gainBits_.store(yarg::audio::bitCast<std::uint32_t>(gain), std::memory_order_relaxed);
    return true;
}

void ScheduledSampleSource::render(float* output, std::size_t outputFrames) noexcept {
    if (!output || outputFrames == 0) return;

    clearOutput(output, outputFrames);
    if (pausedBits_.load(std::memory_order_relaxed) != 0) return;

    const auto maximum = std::numeric_limits<std::int64_t>::max();
    const auto frameCount = outputFrames > static_cast<std::size_t>(maximum)
        ? maximum : static_cast<std::int64_t>(outputFrames);
    const std::int64_t bufferStartFrame = cursorFrame_;
    const std::int64_t bufferEndFrame = cursorFrame_ > maximum - frameCount
        ? maximum : cursorFrame_ + frameCount;
    cursorFrame_ = bufferEndFrame;

    const float gain = yarg::audio::bitCast<float>(gainBits_.load(std::memory_order_relaxed));
    mixActiveVoices(output, outputFrames, gain);
    startScheduledVoices(output, outputFrames, bufferStartFrame, bufferEndFrame, gain);
}

void ScheduledSampleSource::clearOutput(float* output,
    std::size_t outputFrames) const noexcept {
    for (std::size_t frame = 0; frame < outputFrames; ++frame) {
        for (std::uint32_t channel = 0; channel < channels_; ++channel) {
            output[frame * channels_ + channel] = 0;
        }
    }
}

void ScheduledSampleSource::mixActiveVoices(float* output,
    std::size_t outputFrames, float gain) noexcept {
    std::size_t writeIndex = 0;
    for (std::size_t i = 0; i < activeVoiceCount_; ++i) {
        Voice voice = voices_[i];
        mixVoice(output, outputFrames, 0, voice, gain);
        if (voice.sampleFrame < sampleFrameCount_) {
            voices_[writeIndex++] = voice;
        }
    }
    activeVoiceCount_ = writeIndex;
}

void ScheduledSampleSource::startScheduledVoices(float* output,
    std::size_t outputFrames, std::int64_t bufferStartFrame,
    std::int64_t bufferEndFrame, float gain) noexcept {
    while (nextScheduledVoice_ < schedule_.size()) {
        const std::int64_t target = targetFrame(schedule_[nextScheduledVoice_]);
        if (target >= bufferEndFrame) break;

        ++nextScheduledVoice_;
        if (target < bufferStartFrame) continue;

        const auto offset = static_cast<std::size_t>(target - bufferStartFrame);
        startVoice(output, outputFrames, offset, gain);
    }
}

void ScheduledSampleSource::startVoice(float* output, std::size_t outputFrames,
    std::size_t outputFrameOffset, float gain) noexcept {
    if (activeVoiceCount_ >= MaxActiveVoices) {
        if (droppedVoiceCount_ != std::numeric_limits<std::uint64_t>::max()) {
            ++droppedVoiceCount_;
        }
        return;
    }

    Voice voice{};
    mixVoice(output, outputFrames, outputFrameOffset, voice, gain);
    if (voice.sampleFrame < sampleFrameCount_) {
        voices_[activeVoiceCount_++] = voice;
    }
}

void ScheduledSampleSource::mixVoice(float* output, std::size_t outputFrames,
    std::size_t outputFrameOffset, Voice& voice, float gain) const noexcept {
    if (outputFrameOffset >= outputFrames || voice.sampleFrame >= sampleFrameCount_) {
        return;
    }

    const std::size_t availableOutputFrames = outputFrames - outputFrameOffset;
    const std::size_t availableSampleFrames = sampleFrameCount_ - voice.sampleFrame;
    const std::size_t framesToMix = std::min(availableOutputFrames, availableSampleFrames);
    for (std::size_t frame = 0; frame < framesToMix; ++frame) {
        const std::size_t sourceFrame = voice.sampleFrame + frame;
        const std::size_t outputFrame = outputFrameOffset + frame;
        for (std::uint32_t channel = 0; channel < channels_; ++channel) {
            output[outputFrame * channels_ + channel] +=
                pcm_[sourceFrame * channels_ + channel] * gain;
        }
    }
    voice.sampleFrame += framesToMix;
}

std::size_t ScheduledSampleSource::findNextSchedule() const noexcept {
    if (schedule_.empty()) return 0;

    if (leadTime_ == 0) {
        return static_cast<std::size_t>(std::lower_bound(
            schedule_.begin(), schedule_.end(), anchorSongPosition_) - schedule_.begin());
    }

    const double boundary = anchorSongPosition_ + leadTime_ * playbackSpeed_;
    if (!std::isfinite(boundary)) return schedule_.size();
    return static_cast<std::size_t>(std::upper_bound(
        schedule_.begin(), schedule_.end(), boundary) - schedule_.begin());
}

std::int64_t ScheduledSampleSource::targetFrame(
    double scheduledSongPosition) const noexcept {
    const double secondsFromAnchor =
        (scheduledSongPosition - anchorSongPosition_) / playbackSpeed_;
    const double outputSecondsFromAnchor = secondsFromAnchor - leadTime_;
    return clampFrame(outputSecondsFromAnchor * static_cast<double>(sampleRate_));
}

} // namespace yarg::audio
