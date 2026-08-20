#include "dsp/NoiseGateDsp.h"

#include "BitCastCompat.h"

#include <cmath>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

float timeCoefficient(float milliseconds, std::uint32_t sampleRate) noexcept {
    if (milliseconds <= 0.0f) return 1.0f;
    const double seconds = static_cast<double>(milliseconds) * 0.001;
    return static_cast<float>(1.0 - std::exp(-1.0 / (seconds * sampleRate)));
}

std::uint32_t timeFrames(float milliseconds, std::uint32_t sampleRate) noexcept {
    const double frames = static_cast<double>(milliseconds) * 0.001 * sampleRate;
    if (frames >= std::numeric_limits<std::uint32_t>::max()) {
        return std::numeric_limits<std::uint32_t>::max();
    }
    return static_cast<std::uint32_t>(frames + 0.5);
}

void resetState(yarg_noise_gate_dsp* state) noexcept {
    state->envelopeSquared = 0.0f;
    state->gain = 1.0f;
    state->holdRemaining = 0;
}

void process(yarg_noise_gate_dsp* state, float* samples, std::size_t sampleCount) noexcept {
    const std::uint32_t channelCount = state->channelCount;
    const std::size_t frameCount = sampleCount / channelCount;

    const float thresholdSquared = yarg::audio::bitCast<float>(
        state->thresholdSquaredBits.load(std::memory_order_relaxed));
    const float floorGain = yarg::audio::bitCast<float>(
        state->floorGainBits.load(std::memory_order_relaxed));
    const float attackCoefficient = yarg::audio::bitCast<float>(
        state->attackCoefficientBits.load(std::memory_order_relaxed));
    const std::uint32_t holdFrames = state->holdFramesBits.load(std::memory_order_relaxed);
    const float releaseCoefficient = yarg::audio::bitCast<float>(
        state->releaseCoefficientBits.load(std::memory_order_relaxed));

    for (std::size_t frame = 0; frame < frameCount; ++frame) {
        const std::size_t frameOffset = frame * channelCount;
        float power = 0.0f;
        for (std::uint32_t channel = 0; channel < channelCount; ++channel) {
            const float sample = samples[frameOffset + channel];
            power += sample * sample;
        }
        power /= static_cast<float>(channelCount);

        const float detectorCoefficient = power > state->envelopeSquared
            ? attackCoefficient : releaseCoefficient;
        state->envelopeSquared +=
            (power - state->envelopeSquared) * detectorCoefficient;

        if (state->envelopeSquared >= thresholdSquared) {
            state->holdRemaining = holdFrames;
        }
        else if (state->holdRemaining > 0) {
            --state->holdRemaining;
        }

        const bool open = state->envelopeSquared >= thresholdSquared ||
            state->holdRemaining > 0;
        const float targetGain = open ? 1.0f : floorGain;
        const float gainCoefficient = targetGain > state->gain
            ? attackCoefficient : releaseCoefficient;
        state->gain += (targetGain - state->gain) * gainCoefficient;

        for (std::uint32_t channel = 0; channel < channelCount; ++channel) {
            samples[frameOffset + channel] *= state->gain;
        }
    }
}

void freeState(yarg_noise_gate_dsp* state) noexcept {
    if (!state) return;
    delete state;
}

}

yarg_noise_gate_dsp::yarg_noise_gate_dsp(
    const yarg::audio::BassCoreBindings& bindings, std::uint32_t channelHandle,
    std::uint32_t channels, float threshold, float floorGain,
    float attackCoefficientValue, std::uint32_t holdFrameCount,
    float releaseCoefficientValue, std::uint32_t sampleRateValue) noexcept
    : bass(bindings), channel(channelHandle), channelCount(channels),
      sampleRate(sampleRateValue),
      thresholdSquaredBits(yarg::audio::bitCast<std::uint32_t>(threshold * threshold)),
      floorGainBits(yarg::audio::bitCast<std::uint32_t>(floorGain)),
      attackCoefficientBits(yarg::audio::bitCast<std::uint32_t>(attackCoefficientValue)),
      holdFramesBits(holdFrameCount),
      releaseCoefficientBits(yarg::audio::bitCast<std::uint32_t>(releaseCoefficientValue)),
      resetRequested(0) {}

namespace yarg::audio {

void YARG_BASS_CALLBACK noiseGateDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_noise_gate_dsp*>(user);
    if (state->resetRequested.exchange(0, std::memory_order_relaxed) != 0) {
        resetState(state);
    }
    process(state, static_cast<float*>(buffer), length / sizeof(float));
}

int noiseGateDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float threshold, float floorGain, float attackMs, float holdMs,
    float releaseMs, int priority, yarg_noise_gate_dsp** dsp,
    int* bassError) noexcept {
    if (dsp) *dsp = nullptr;
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0 || !std::isfinite(threshold) ||
        !std::isfinite(floorGain) || !std::isfinite(attackMs) ||
        !std::isfinite(holdMs) || !std::isfinite(releaseMs) || threshold < 0.0f ||
        floorGain < 0.0f || floorGain > 1.0f || attackMs < 0.0f ||
        holdMs < 0.0f || releaseMs < 0.0f) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    if (!bass.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    BassChannelInfo info{};
    if (!bass.getChannelInfo(channel, info)) {
        if (bassError) *bassError = bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }
    if (info.frequency == 0 || info.channels == 0) {
        return YARG_AUDIO_ERROR_INVALID_STATE;
    }
    if ((info.flags & BassSampleFloat) == 0 && bass.getConfig(BassConfigFloatDsp) == 0) {
        return YARG_AUDIO_ERROR_UNSUPPORTED;
    }

    auto* state = new (std::nothrow) yarg_noise_gate_dsp(bass, channel, info.channels,
        threshold, floorGain, timeCoefficient(attackMs, info.frequency),
        timeFrames(holdMs, info.frequency), timeCoefficient(releaseMs, info.frequency),
        info.frequency);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;

    state->dsp = bass.setDsp(channel, &noiseGateDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        freeState(state);
        return YARG_AUDIO_ERROR_BASS;
    }

    *dsp = state;
    return YARG_AUDIO_OK;
}

int noiseGateDspRequestReset(yarg_noise_gate_dsp* dsp) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->resetRequested.store(1, std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

int noiseGateDspSetParams(yarg_noise_gate_dsp* dsp, const yarg_noise_gate_params* params) noexcept {
    if (!dsp || !params || params->size < sizeof(yarg_noise_gate_params)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    const float threshold = params->threshold;
    const float floorGain = params->floor_gain;
    const float attackMs = params->attack_ms;
    const float holdMs = params->hold_ms;
    const float releaseMs = params->release_ms;
    if (!std::isfinite(threshold) || !std::isfinite(floorGain) ||
        !std::isfinite(attackMs) || !std::isfinite(holdMs) || !std::isfinite(releaseMs) ||
        threshold < 0.0f || floorGain < 0.0f || floorGain > 1.0f ||
        attackMs < 0.0f || holdMs < 0.0f || releaseMs < 0.0f) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    dsp->thresholdSquaredBits.store(yarg::audio::bitCast<std::uint32_t>(threshold * threshold), std::memory_order_relaxed);
    dsp->floorGainBits.store(yarg::audio::bitCast<std::uint32_t>(floorGain), std::memory_order_relaxed);
    dsp->attackCoefficientBits.store(yarg::audio::bitCast<std::uint32_t>(timeCoefficient(attackMs, dsp->sampleRate)), std::memory_order_relaxed);
    dsp->holdFramesBits.store(timeFrames(holdMs, dsp->sampleRate), std::memory_order_relaxed);
    dsp->releaseCoefficientBits.store(yarg::audio::bitCast<std::uint32_t>(timeCoefficient(releaseMs, dsp->sampleRate)), std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool noiseGateDspDestroy(yarg_noise_gate_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;

    freeState(dsp);
    return true;
}

}
