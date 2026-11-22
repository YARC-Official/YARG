#include "dsp/PeakLimiterDsp.h"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <new>

yarg_peak_limiter_dsp::yarg_peak_limiter_dsp(const yarg::audio::BassCoreBindings& bindings,
    std::uint32_t channelHandle, std::uint32_t channels, float ceilingLevel,
    float release) noexcept
    : bass(bindings), channel(channelHandle), channelCount(channels),
      ceiling(ceilingLevel), releaseCoefficient(release) {}

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

} // namespace

void YARG_BASS_CALLBACK peakLimiterDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_peak_limiter_dsp*>(user);
    const std::size_t channels = state->channelCount;
    const std::size_t frames = static_cast<std::size_t>(length) / sizeof(float) / channels;
    auto* samples = static_cast<float*>(buffer);
    float gain = state->gain;

    for (std::size_t frame = 0; frame < frames; ++frame) {
        float* frameSamples = samples + frame * channels;
        float peak = 0.0f;
        for (std::size_t c = 0; c < channels; ++c) {
            peak = std::max(peak, std::fabs(frameSamples[c]));
        }

        const float target = peak > state->ceiling ? state->ceiling / peak : 1.0f;
        gain = target < gain ? target : target + (gain - target) * state->releaseCoefficient;

        for (std::size_t c = 0; c < channels; ++c) {
            frameSamples[c] *= gain;
        }
    }

    state->gain = gain;
}

int peakLimiterDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float ceiling, float releaseMs, int priority, yarg_peak_limiter_dsp** dsp,
    int* bassError) noexcept {
    if (dsp) *dsp = nullptr;
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0 || !std::isfinite(ceiling) || ceiling <= 0.0f ||
        !std::isfinite(releaseMs) || releaseMs <= 0.0f)
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (!bass.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    BassChannelInfo info{};
    if (!bass.getChannelInfo(channel, info)) {
        if (bassError) *bassError = bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }
    if (info.frequency == 0 || info.channels == 0) return YARG_AUDIO_ERROR_UNSUPPORTED;
    if ((info.flags & BassSampleFloat) == 0 && bass.getConfig(BassConfigFloatDsp) == 0)
        return YARG_AUDIO_ERROR_UNSUPPORTED;

    const auto releaseCoefficient = static_cast<float>(
        std::exp(-1.0 / (releaseMs / 1000.0 * info.frequency)));
    auto* state = new (std::nothrow) yarg_peak_limiter_dsp(bass, channel, info.channels,
        ceiling, releaseCoefficient);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;

    state->dsp = bass.setDsp(channel, &peakLimiterDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        delete state;
        return YARG_AUDIO_ERROR_BASS;
    }

    *dsp = state;
    return YARG_AUDIO_OK;
}

bool peakLimiterDspDestroy(yarg_peak_limiter_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;

    delete dsp;
    return true;
}

} // namespace yarg::audio
