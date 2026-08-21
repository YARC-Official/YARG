#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>

struct yarg_freeverb_dsp {
    yarg_freeverb_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle, std::uint32_t channels,
        float dryMix, float wetMix, float roomSize, float damp, float width) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::uint32_t channelCount;

    int combStatesOffset = 0;
    int allPassStatesOffset = 0;
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

void YARG_BASS_CALLBACK freeverbDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int freeverbDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float dryMix, float wetMix, float roomSize, float damp, float width,
    int priority, yarg_freeverb_dsp** dsp, int* bassError) noexcept;
int freeverbDspRequestReset(yarg_freeverb_dsp* dsp) noexcept;
int freeverbDspSetParams(yarg_freeverb_dsp* dsp, const yarg_freeverb_params* params) noexcept;

// Returns false when state must remain allocated because detach was not safe.
bool freeverbDspDestroy(yarg_freeverb_dsp* dsp) noexcept;

} // namespace yarg::audio
