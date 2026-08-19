#include "one_shot/NativeOneShotStream.h"

#include <cmath>
#include <cstring>
#include <limits>
#include <new>
#include <utility>

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassStreamDecode = 0x200000;
constexpr std::uint32_t BassMixerChannelNoRampIn = 0x800000;

void setBassError(int* target, const BassCoreBindings& core) noexcept {
    if (target) *target = core.error();
}

} // namespace

std::unique_ptr<NativeOneShotStream> NativeOneShotStream::create(
    BassCoreBindings& core, BassMixBindings& mix,
    std::uint32_t sampleRate, std::uint32_t channels,
    const float* pcm, std::size_t pcmSampleCount,
    const double* schedule, std::size_t scheduleCount,
    double leadTime, int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!core.oneShotValid() || !mix.oneShotValid()) return nullptr;

    auto source = ScheduledSampleSource::create(sampleRate, channels, pcm,
        pcmSampleCount, schedule, scheduleCount, leadTime);
    if (!source) return nullptr;

    try {
        auto stream = std::unique_ptr<NativeOneShotStream>(
            new NativeOneShotStream(core, mix, std::move(source)));
        if (stream->stream_ == 0) {
            setBassError(bassError, core);
            return nullptr;
        }
        return stream;
    } catch (...) {
        return nullptr;
    }
}

NativeOneShotStream::NativeOneShotStream(BassCoreBindings& core,
    BassMixBindings& mix, std::unique_ptr<ScheduledSampleSource>&& source) noexcept
    : core_(core), mix_(mix), source_(std::move(source)) {
    stream_ = core_.createStream(source_->sampleRate(), source_->channels(),
        BassSampleFloat | BassStreamDecode, &streamProc, this);
}

int NativeOneShotStream::attach(std::uint32_t mixer,
    double anchorSongPosition, float playbackSpeed, bool paused,
    int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (mixer == 0 || !std::isfinite(anchorSongPosition) ||
        !std::isfinite(playbackSpeed) || playbackSpeed <= 0) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }

    if (!source_ || stream_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    if (mixer_ != 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    if (!core_.lockChannel(mixer, true)) {
        setBassError(bassError, core_);
        return YARG_AUDIO_ERROR_BASS;
    }

    const int result = [&] {
        if (!validMixer(mixer, bassError)) return YARG_AUDIO_ERROR_UNSUPPORTED;
        if (!source_->reset(anchorSongPosition, playbackSpeed, paused, true)) {
            return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
        }
        if (!mix_.addChannel(mixer, stream_, BassMixerChannelNoRampIn)) {
            setBassError(bassError, core_);
            return YARG_AUDIO_ERROR_BASS;
        }
        mixer_ = mixer;
        paused_ = paused;
        return YARG_AUDIO_OK;
    }();

    core_.lockChannel(mixer, false);
    return result;
}

int NativeOneShotStream::resync(std::uint32_t mixer,
    double anchorSongPosition, float playbackSpeed, bool clearActiveVoices,
    int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (mixer == 0 || !std::isfinite(anchorSongPosition) ||
        !std::isfinite(playbackSpeed) || playbackSpeed <= 0) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }

    if (!source_ || stream_ == 0 || mixer_ != mixer)
        return YARG_AUDIO_ERROR_INVALID_STATE;
    if (!core_.lockChannel(mixer_, true)) {
        setBassError(bassError, core_);
        return YARG_AUDIO_ERROR_BASS;
    }
    const bool reset = source_->reset(anchorSongPosition, playbackSpeed, paused_,
        clearActiveVoices);
    core_.lockChannel(mixer_, false);
    return reset ? YARG_AUDIO_OK : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int NativeOneShotStream::setPaused(std::uint32_t mixer, bool paused,
    int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!source_ || stream_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    if (mixer_ != 0 && mixer != mixer_) return YARG_AUDIO_ERROR_INVALID_STATE;

    if (mixer_ != 0) {
        if (!core_.lockChannel(mixer_, true)) {
            setBassError(bassError, core_);
            return YARG_AUDIO_ERROR_BASS;
        }
        source_->setPaused(paused);
        paused_ = paused;
        core_.lockChannel(mixer_, false);
    } else {
        source_->setPaused(paused);
        paused_ = paused;
    }
    return YARG_AUDIO_OK;
}

int NativeOneShotStream::setGain(float gain) noexcept {
    if (!source_ || stream_ == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    return source_->setGain(gain) ? YARG_AUDIO_OK : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int NativeOneShotStream::detach(int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (mixer_ == 0) return YARG_AUDIO_OK;
    const std::uint32_t mixer = mixer_;
    if (!core_.lockChannel(mixer, true)) {
        setBassError(bassError, core_);
        return YARG_AUDIO_ERROR_BASS;
    }

    const bool removed = mix_.removeChannel(stream_);
    if (!removed) setBassError(bassError, core_);
    core_.lockChannel(mixer, false);
    if (!removed) return YARG_AUDIO_ERROR_BASS;

    mixer_ = 0;
    return YARG_AUDIO_OK;
}

bool NativeOneShotStream::destroy(int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!source_ && stream_ == 0) return true;

    if (mixer_ != 0 && detach(bassError) != YARG_AUDIO_OK) return false;
    if (stream_ != 0) {
        if (!core_.freeStream(stream_)) {
            setBassError(bassError, core_);
            return false;
        }
        stream_ = 0;
    }
    source_.reset();
    return true;
}

bool NativeOneShotStream::validMixer(std::uint32_t mixer,
    int* bassError) const noexcept {
    BassChannelInfo info{};
    if (!core_.getChannelInfo(mixer, info)) {
        setBassError(bassError, core_);
        return false;
    }
    if ((info.flags & BassSampleFloat) == 0 ||
        info.frequency != source_->sampleRate() ||
        info.channels != source_->channels()) {
        return false;
    }
    return true;
}

std::uint32_t YARG_BASS_CALLBACK NativeOneShotStream::streamProc(
    std::uint32_t, void* buffer, std::uint32_t length, void* user) noexcept {
    auto* stream = static_cast<NativeOneShotStream*>(user);
    if (!stream || !stream->source_ || !buffer || length == 0) return 0;

    const std::uint64_t frameBytes = static_cast<std::uint64_t>(
        stream->source_->channels()) * sizeof(float);
    if (frameBytes == 0 || length % frameBytes != 0) {
        std::memset(buffer, 0, length);
        return length;
    }

    stream->source_->render(static_cast<float*>(buffer),
        static_cast<std::size_t>(length / frameBytes));
    return length;
}

} // namespace yarg::audio
