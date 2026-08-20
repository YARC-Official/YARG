#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>

struct yarg_dattorro_reverb_dsp {
    yarg_dattorro_reverb_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle, std::uint32_t channels,
        std::uint32_t sampleRate,
        float dryMix, float wetMix, float roomSize, float damp, float width) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::uint32_t channelCount;
    std::uint32_t sampleRate;

    int engineStatesOffset = 0;
    int lfoPhasesOffset = 0;
    int delaySamplesOffset = 0;
    int delaySampleCount = 0;
    std::atomic<std::uint32_t> resetRequested;

    std::atomic<std::uint32_t> roomFeedbackBits;
    std::atomic<std::uint32_t> dampingBits;
    std::atomic<std::uint32_t> wetMixBits;
    std::atomic<std::uint32_t> sameChannelWetMixBits;
    std::atomic<std::uint32_t> crossChannelWetMixBits;
    std::atomic<std::uint32_t> dryMixBits;
    std::atomic<std::uint32_t> widthBits;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK dattorroReverbDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int dattorroReverbDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float dryMix, float wetMix, float roomSize, float damp, float width,
    int priority, yarg_dattorro_reverb_dsp** dsp, int* bassError) noexcept;
int dattorroReverbDspRequestReset(yarg_dattorro_reverb_dsp* dsp) noexcept;
int dattorroReverbDspSetParams(yarg_dattorro_reverb_dsp* dsp, const yarg_dattorro_reverb_params* params) noexcept;

bool dattorroReverbDspDestroy(yarg_dattorro_reverb_dsp* dsp) noexcept;

}
