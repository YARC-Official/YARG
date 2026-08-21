#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstdint>

struct yarg_noise_gate_dsp {
    yarg_noise_gate_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t channelHandle, std::uint32_t channels,
        float threshold, float floorGain, float attackCoefficient,
        std::uint32_t holdFrames, float releaseCoefficient,
        std::uint32_t sampleRate) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t channel;
    std::uint32_t dsp = 0;
    std::uint32_t channelCount;
    std::uint32_t sampleRate;
    std::atomic<std::uint32_t> thresholdSquaredBits;
    std::atomic<std::uint32_t> floorGainBits;
    std::atomic<std::uint32_t> attackCoefficientBits;
    std::atomic<std::uint32_t> holdFramesBits;
    std::atomic<std::uint32_t> releaseCoefficientBits;
    std::atomic<std::uint32_t> resetRequested;
    float envelopeSquared = 0.0f;
    float gain = 1.0f;
    std::uint32_t holdRemaining = 0;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK noiseGateDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int noiseGateDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float threshold, float floorGain, float attackMs, float holdMs,
    float releaseMs, int priority, yarg_noise_gate_dsp** dsp,
    int* bassError) noexcept;
int noiseGateDspRequestReset(yarg_noise_gate_dsp* dsp) noexcept;
int noiseGateDspSetParams(yarg_noise_gate_dsp* dsp, const yarg_noise_gate_params* params) noexcept;

bool noiseGateDspDestroy(yarg_noise_gate_dsp* dsp) noexcept;

}
