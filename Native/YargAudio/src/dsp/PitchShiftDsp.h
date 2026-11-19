#pragma once

#include "BassCoreBindings.h"
#include "dsp/PitchShifter.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>

struct yarg_pitch_shift_dsp {
    yarg_pitch_shift_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::uint32_t channelCount = 0;
    std::atomic<std::uint32_t> ratioBits;
    std::atomic<std::uint32_t> resetRequested;
    yarg::audio::PitchShifter shifter;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK pitchShiftDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int pitchShiftDspAttach(const BassCoreBindings& bass, std::uint32_t channel, int priority,
    yarg_pitch_shift_dsp** dsp, int* bassError) noexcept;
int pitchShiftDspSetRatio(yarg_pitch_shift_dsp* dsp, float ratio) noexcept;
int pitchShiftDspRequestReset(yarg_pitch_shift_dsp* dsp) noexcept;
int pitchShiftDspGetLatency(const yarg_pitch_shift_dsp* dsp, std::uint32_t* frames) noexcept;

bool pitchShiftDspDestroy(yarg_pitch_shift_dsp* dsp) noexcept;

} // namespace yarg::audio
