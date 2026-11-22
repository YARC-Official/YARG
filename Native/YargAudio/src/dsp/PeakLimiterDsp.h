#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <cstdint>

struct yarg_peak_limiter_dsp {
    yarg_peak_limiter_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle, std::uint32_t channels, float ceiling,
        float releaseCoefficient) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::uint32_t channelCount;
    float ceiling;
    float releaseCoefficient;
    float gain = 1.0f;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK peakLimiterDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int peakLimiterDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float ceiling, float releaseMs, int priority, yarg_peak_limiter_dsp** dsp,
    int* bassError) noexcept;

bool peakLimiterDspDestroy(yarg_peak_limiter_dsp* dsp) noexcept;

} // namespace yarg::audio
