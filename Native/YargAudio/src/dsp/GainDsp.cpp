#include "dsp/GainDsp.h"

#include "BitCastCompat.h"

#include <bit>
#include <cmath>
#include <cstddef>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

yarg_gain_dsp::yarg_gain_dsp(const yarg::audio::BassCoreBindings& bindings,
    std::uint32_t channelHandle, float initialGain) noexcept
    : bass(bindings), channel(channelHandle),
      gainBits(yarg::audio::bitCast<std::uint32_t>(initialGain)) {}

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

} // namespace

void YARG_BASS_CALLBACK gainDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_gain_dsp*>(user);
    const float gain = yarg::audio::bitCast<float>(
        state->gainBits.load(std::memory_order_relaxed));
    auto* samples = static_cast<float*>(buffer);
    const auto sampleCount = static_cast<std::size_t>(length) / sizeof(float);
    for (std::size_t i = 0; i < sampleCount; ++i) {
        samples[i] *= gain;
    }
}

int gainDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float initialGain, int priority, yarg_gain_dsp** dsp, int* bassError) noexcept {
    if (dsp) *dsp = nullptr;
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0 || !std::isfinite(initialGain))
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (!bass.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    BassChannelInfo info{};
    if (!bass.getChannelInfo(channel, info)) {
        if (bassError) *bassError = bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }
    if ((info.flags & BassSampleFloat) == 0 && bass.getConfig(BassConfigFloatDsp) == 0)
        return YARG_AUDIO_ERROR_UNSUPPORTED;

    auto* state = new (std::nothrow) yarg_gain_dsp(bass, channel, initialGain);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;

    state->dsp = bass.setDsp(channel, &gainDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        delete state;
        return YARG_AUDIO_ERROR_BASS;
    }

    *dsp = state;
    return YARG_AUDIO_OK;
}

int gainDspSetGain(yarg_gain_dsp* dsp, float gain) noexcept {
    if (!dsp || !std::isfinite(gain)) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->gainBits.store(yarg::audio::bitCast<std::uint32_t>(gain), std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool gainDspDestroy(yarg_gain_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;

    delete dsp;
    return true;
}

} // namespace yarg::audio
