#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>

struct yarg_gain_dsp {
    yarg_gain_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle, float initialGain) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::atomic<std::uint32_t> gainBits;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK gainDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int gainDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float initialGain, int priority, yarg_gain_dsp** dsp, int* bassError) noexcept;
int gainDspSetGain(yarg_gain_dsp* dsp, float gain) noexcept;

// Returns false when state must remain allocated because detach was not safe.
bool gainDspDestroy(yarg_gain_dsp* dsp) noexcept;

} // namespace yarg::audio
