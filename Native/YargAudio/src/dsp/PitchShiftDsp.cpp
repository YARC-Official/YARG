#include "dsp/PitchShiftDsp.h"

#include "BitCastCompat.h"

#include <cmath>
#include <cstddef>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

yarg_pitch_shift_dsp::yarg_pitch_shift_dsp(const yarg::audio::BassCoreBindings& bindings,
    std::uint32_t channelHandle) noexcept
    : bass(bindings), channel(channelHandle),
      ratioBits(yarg::audio::bitCast<std::uint32_t>(1.0f)), resetRequested(0) {}

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

} // namespace

void YARG_BASS_CALLBACK pitchShiftDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0) return;

    auto* state = static_cast<yarg_pitch_shift_dsp*>(user);
    const std::size_t frameBytes = sizeof(float) * state->channelCount;
    if (length % frameBytes != 0) return;

    state->shifter.setRatio(bitCast<float>(state->ratioBits.load(std::memory_order_relaxed)));
    if (state->resetRequested.exchange(0, std::memory_order_acquire) != 0) {
        state->shifter.reset();
    }
    state->shifter.process(static_cast<float*>(buffer), length / frameBytes);
}

int pitchShiftDspAttach(const BassCoreBindings& bass, std::uint32_t channel, int priority,
    yarg_pitch_shift_dsp** dsp, int* bassError) noexcept {
    if (dsp) *dsp = nullptr;
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (!bass.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    BassChannelInfo info{};
    if (!bass.getChannelInfo(channel, info)) {
        if (bassError) *bassError = bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }
    if (info.channels == 0 || info.channels > PitchShifter::MAX_CHANNELS ||
        PitchShifter::latencyFrames(info.frequency) == 0) {
        return YARG_AUDIO_ERROR_UNSUPPORTED;
    }
    if ((info.flags & BassSampleFloat) == 0 && bass.getConfig(BassConfigFloatDsp) == 0) {
        return YARG_AUDIO_ERROR_UNSUPPORTED;
    }

    auto* state = new (std::nothrow) yarg_pitch_shift_dsp(bass, channel);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;
    state->channelCount = info.channels;
    if (!state->shifter.configure(info.channels, info.frequency)) {
        delete state;
        return YARG_AUDIO_ERROR_INTERNAL;
    }

    state->dsp = bass.setDsp(channel, &pitchShiftDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        delete state;
        return YARG_AUDIO_ERROR_BASS;
    }

    *dsp = state;
    return YARG_AUDIO_OK;
}

int pitchShiftDspSetRatio(yarg_pitch_shift_dsp* dsp, float ratio) noexcept {
    if (!dsp || !std::isfinite(ratio) || ratio <= 0.0f) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->ratioBits.store(bitCast<std::uint32_t>(ratio), std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

int pitchShiftDspRequestReset(yarg_pitch_shift_dsp* dsp) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->resetRequested.store(1, std::memory_order_release);
    return YARG_AUDIO_OK;
}

int pitchShiftDspGetLatency(const yarg_pitch_shift_dsp* dsp, std::uint32_t* frames) noexcept {
    if (!dsp || !frames) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    *frames = dsp->shifter.latency();
    return YARG_AUDIO_OK;
}

bool pitchShiftDspDestroy(yarg_pitch_shift_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;

    delete dsp;
    return true;
}

} // namespace yarg::audio
