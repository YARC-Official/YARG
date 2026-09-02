#include "dsp/FreeverbDsp.h"

#include "BitCastCompat.h"

#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <limits>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

constexpr std::uint32_t ReferenceSampleRate = 44100;
constexpr int StereoSpread = 23;
constexpr int CombFilterCount = 8;
constexpr int AllPassFilterCount = 4;

constexpr float FixedGain = 0.015f;
constexpr float ScaleDamp = 0.4f;
constexpr float ScaleRoom = 0.28f;
constexpr float OffsetRoom = 0.7f;
constexpr float AllPassFeedback = 0.5f;

constexpr int CombTunings[CombFilterCount] = {
    1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617,
};

constexpr int AllPassTunings[AllPassFilterCount] = {
    556, 441, 341, 225,
};

struct FreeverbFilterState {
    int bufferOffset;
    int bufferLength;
    int index;
    float filterStore;
};

static_assert(sizeof(FreeverbFilterState) == 16);

float clamp(float value, float minimum, float maximum) noexcept {
    return value < minimum ? minimum : (value > maximum ? maximum : value);
}

float clamp01(float value) noexcept {
    return clamp(value, 0.0f, 1.0f);
}

float computeRoomFeedback(float roomSize) noexcept {
    return clamp01(roomSize) * ScaleRoom + OffsetRoom;
}

float computeDamping(float damp) noexcept {
    return clamp01(damp) * ScaleDamp;
}

float clampWet(float wet) noexcept {
    return clamp(wet, 0.0f, 3.0f);
}

float computeSameChannelWet(float wet, float width) noexcept {
    return clampWet(wet) * (clamp01(width) * 0.5f + 0.5f);
}

float computeCrossChannelWet(float wet, float width) noexcept {
    return clampWet(wet) * ((1.0f - clamp01(width)) * 0.5f);
}

// Match Math.Round(double) used by the managed implementation: nearest,
// ties to even. All inputs are positive, so integer arithmetic is exact.
std::uint64_t scaleDelay(int referenceLength, std::uint32_t sampleRate) noexcept {
    const std::uint64_t numerator = static_cast<std::uint64_t>(referenceLength) * sampleRate;
    const std::uint64_t quotient = numerator / ReferenceSampleRate;
    const std::uint64_t remainder = numerator % ReferenceSampleRate;
    std::uint64_t result = quotient;
    if (remainder > ReferenceSampleRate / 2 ||
        (remainder == ReferenceSampleRate / 2 && (quotient & 1u) != 0)) {
        ++result;
    }
    return result == 0 ? 1 : result;
}

bool calculateDelaySampleCount(std::uint32_t sampleRate, std::uint32_t channelCount,
    std::uint64_t& delaySampleCount) noexcept {
    std::uint64_t evenChannelCount = 0;
    std::uint64_t oddChannelCount = 0;
    for (int tuning : CombTunings) {
        evenChannelCount += scaleDelay(tuning, sampleRate);
        oddChannelCount += scaleDelay(tuning + StereoSpread, sampleRate);
    }
    for (int tuning : AllPassTunings) {
        evenChannelCount += scaleDelay(tuning, sampleRate);
        oddChannelCount += scaleDelay(tuning + StereoSpread, sampleRate);
    }

    const std::uint64_t pairs = channelCount / 2;
    const std::uint64_t oddChannel = channelCount & 1u;
    const std::uint64_t pairDelay = evenChannelCount + oddChannelCount;
    if (pairs != 0 && pairDelay > std::numeric_limits<std::uint64_t>::max() / pairs) {
        return false;
    }
    delaySampleCount = pairs * pairDelay;
    if (oddChannel != 0) {
        if (delaySampleCount > std::numeric_limits<std::uint64_t>::max() - evenChannelCount) {
            return false;
        }
        delaySampleCount += evenChannelCount;
    }
    return true;
}

bool calculateAllocationSize(std::uint32_t sampleRate, std::uint32_t channelCount,
    std::uint64_t& totalBytes, std::uint64_t& delaySampleCount) noexcept {
    if (sampleRate == 0 || channelCount == 0 ||
        !calculateDelaySampleCount(sampleRate, channelCount, delaySampleCount)) {
        return false;
    }

    const std::uint64_t combStateCount =
        static_cast<std::uint64_t>(channelCount) * CombFilterCount;
    const std::uint64_t allPassStateCount =
        static_cast<std::uint64_t>(channelCount) * AllPassFilterCount;
    const std::uint64_t filterStateCount = combStateCount + allPassStateCount;
    const std::uint64_t filterStateBytes = filterStateCount * sizeof(FreeverbFilterState);
    const std::uint64_t stateBytes = sizeof(yarg_freeverb_dsp);
    constexpr std::uint64_t maxStateBytes = std::numeric_limits<int>::max();

    if (delaySampleCount > maxStateBytes ||
        filterStateBytes > maxStateBytes - stateBytes ||
        delaySampleCount >
            (maxStateBytes - stateBytes - filterStateBytes) / sizeof(float)) {
        return false;
    }

    totalBytes = stateBytes + filterStateBytes + delaySampleCount * sizeof(float);
    return true;
}

FreeverbFilterState* getCombStates(yarg_freeverb_dsp* state) noexcept {
    return reinterpret_cast<FreeverbFilterState*>(
        reinterpret_cast<std::byte*>(state) + state->combStatesOffset);
}

FreeverbFilterState* getAllPassStates(yarg_freeverb_dsp* state) noexcept {
    return reinterpret_cast<FreeverbFilterState*>(
        reinterpret_cast<std::byte*>(state) + state->allPassStatesOffset);
}

float* getDelaySamples(yarg_freeverb_dsp* state) noexcept {
    return reinterpret_cast<float*>(
        reinterpret_cast<std::byte*>(state) + state->delaySamplesOffset);
}

void initializeFilterStates(yarg_freeverb_dsp* state, std::uint32_t sampleRate) noexcept {
    auto* combStates = getCombStates(state);
    auto* allPassStates = getAllPassStates(state);
    int sampleOffset = 0;
    for (std::uint32_t channel = 0; channel < state->channelCount; ++channel) {
        const int stereoOffset = (channel & 1u) == 0 ? 0 : StereoSpread;
        for (int i = 0; i < CombFilterCount; ++i) {
            auto& filter = combStates[channel * CombFilterCount + i];
            filter.bufferOffset = sampleOffset;
            filter.bufferLength = static_cast<int>(
                scaleDelay(CombTunings[i] + stereoOffset, sampleRate));
            sampleOffset += filter.bufferLength;
        }
        for (int i = 0; i < AllPassFilterCount; ++i) {
            auto& filter = allPassStates[channel * AllPassFilterCount + i];
            filter.bufferOffset = sampleOffset;
            filter.bufferLength = static_cast<int>(
                scaleDelay(AllPassTunings[i] + stereoOffset, sampleRate));
            sampleOffset += filter.bufferLength;
        }
    }
}

void resetState(yarg_freeverb_dsp* state) noexcept {
    auto* combStates = getCombStates(state);
    const std::uint64_t combCount =
        static_cast<std::uint64_t>(state->channelCount) * CombFilterCount;
    for (std::uint64_t i = 0; i < combCount; ++i) {
        combStates[i].index = 0;
        combStates[i].filterStore = 0.0f;
    }

    auto* allPassStates = getAllPassStates(state);
    const std::uint64_t allPassCount =
        static_cast<std::uint64_t>(state->channelCount) * AllPassFilterCount;
    for (std::uint64_t i = 0; i < allPassCount; ++i) {
        allPassStates[i].index = 0;
    }

    std::memset(getDelaySamples(state), 0,
        static_cast<std::size_t>(state->delaySampleCount) * sizeof(float));
}

inline float undenormalize(float value) noexcept {
    return value > -1e-30f && value < 1e-30f ? 0.0f : value;
}

inline float processComb(FreeverbFilterState& state, float* delaySamples,
    float input, float feedback, float damp) noexcept {
    float* buffer = delaySamples + state.bufferOffset;
    const float output = undenormalize(buffer[state.index]);
    state.filterStore = undenormalize(
        output * (1.0f - damp) + state.filterStore * damp);
    buffer[state.index] = input + state.filterStore * feedback;
    if (++state.index == state.bufferLength) {
        state.index = 0;
    }
    return output;
}

inline float processAllPass(FreeverbFilterState& state, float* delaySamples,
    float input) noexcept {
    float* buffer = delaySamples + state.bufferOffset;
    const float buffered = undenormalize(buffer[state.index]);
    const float output = buffered - input;
    buffer[state.index] = input + buffered * AllPassFeedback;
    if (++state.index == state.bufferLength) {
        state.index = 0;
    }
    return output;
}

inline float processChannel(float input, std::uint32_t channel,
    FreeverbFilterState* combStates, FreeverbFilterState* allPassStates,
    float* delaySamples, float roomFeedback, float damping) noexcept {
    float output = 0.0f;
    const std::uint32_t combOffset = channel * CombFilterCount;
    for (int i = 0; i < CombFilterCount; ++i) {
        output += processComb(combStates[combOffset + i], delaySamples, input,
            roomFeedback, damping);
    }

    const std::uint32_t allPassOffset = channel * AllPassFilterCount;
    for (int i = 0; i < AllPassFilterCount; ++i) {
        output = processAllPass(allPassStates[allPassOffset + i], delaySamples, output);
    }
    return output;
}

void process(yarg_freeverb_dsp* state, float* samples, std::size_t sampleCount) noexcept {
    const std::uint32_t channelCount = state->channelCount;
    const std::size_t frameCount = sampleCount / channelCount;
    auto* combStates = getCombStates(state);
    auto* allPassStates = getAllPassStates(state);
    float* delaySamples = getDelaySamples(state);

    const float roomFeedback = yarg::audio::bitCast<float>(
        state->roomFeedbackBits.load(std::memory_order_relaxed));
    const float damping = yarg::audio::bitCast<float>(
        state->dampingBits.load(std::memory_order_relaxed));
    const float wetMix = yarg::audio::bitCast<float>(
        state->wetMixBits.load(std::memory_order_relaxed));
    const float sameChannelWetMix = yarg::audio::bitCast<float>(
        state->sameChannelWetMixBits.load(std::memory_order_relaxed));
    const float crossChannelWetMix = yarg::audio::bitCast<float>(
        state->crossChannelWetMixBits.load(std::memory_order_relaxed));
    const float dryMix = yarg::audio::bitCast<float>(
        state->dryMixBits.load(std::memory_order_relaxed));

    for (std::size_t frame = 0; frame < frameCount; ++frame) {
        const std::size_t frameOffset = frame * channelCount;
        for (std::uint32_t channel = 0; channel < channelCount; channel += 2) {
            const std::uint32_t rightChannel = channel + 1;
            const bool hasRightChannel = rightChannel < channelCount;
            const float leftInput = samples[frameOffset + channel];
            const float rightInput = hasRightChannel
                ? samples[frameOffset + rightChannel]
                : leftInput;
            const float input = (hasRightChannel ? leftInput + rightInput : leftInput) * FixedGain;

            const float leftWet = processChannel(input, channel,
                combStates, allPassStates, delaySamples, roomFeedback, damping);
            float leftOutput;
            if (hasRightChannel) {
                const float rightWet = processChannel(input, rightChannel,
                    combStates, allPassStates, delaySamples, roomFeedback, damping);
                leftOutput = leftWet * sameChannelWetMix +
                    rightWet * crossChannelWetMix;
                const float rightOutput = rightWet * sameChannelWetMix +
                    leftWet * crossChannelWetMix;
                samples[frameOffset + rightChannel] =
                    rightOutput + rightInput * dryMix;
            }
            else {
                leftOutput = leftWet * wetMix;
            }

            samples[frameOffset + channel] = leftOutput + leftInput * dryMix;
        }
    }
}

yarg_freeverb_dsp* allocateState(const yarg::audio::BassCoreBindings& bass,
    std::uint32_t channel,
    std::uint32_t channelCount, std::uint32_t sampleRate, float dryMix, float wetMix,
    float roomSize, float damp, float width) noexcept {
    std::uint64_t totalBytes = 0;
    std::uint64_t delaySampleCount = 0;
    if (!calculateAllocationSize(sampleRate, channelCount, totalBytes, delaySampleCount)) {
        return nullptr;
    }

    void* memory = ::operator new(static_cast<std::size_t>(totalBytes), std::nothrow);
    if (!memory) return nullptr;

    auto* state = new (memory) yarg_freeverb_dsp(bass, channel, channelCount,
        dryMix, wetMix, roomSize, damp, width);
    std::memset(reinterpret_cast<std::byte*>(state) + sizeof(yarg_freeverb_dsp), 0,
        static_cast<std::size_t>(totalBytes) - sizeof(yarg_freeverb_dsp));

    const std::uint64_t combStateBytes =
        static_cast<std::uint64_t>(channelCount) * CombFilterCount * sizeof(FreeverbFilterState);
    const std::uint64_t allPassStateBytes =
        static_cast<std::uint64_t>(channelCount) * AllPassFilterCount * sizeof(FreeverbFilterState);
    state->combStatesOffset = static_cast<int>(sizeof(yarg_freeverb_dsp));
    state->allPassStatesOffset = state->combStatesOffset + static_cast<int>(combStateBytes);
    state->delaySamplesOffset = state->allPassStatesOffset + static_cast<int>(allPassStateBytes);
    state->delaySampleCount = static_cast<int>(delaySampleCount);
    initializeFilterStates(state, sampleRate);
    return state;
}

void freeState(yarg_freeverb_dsp* state) noexcept {
    if (!state) return;
    state->~yarg_freeverb_dsp();
    ::operator delete(state);
}

} // namespace

yarg_freeverb_dsp::yarg_freeverb_dsp(
    const yarg::audio::BassCoreBindings& bindings, std::uint32_t channelHandle,
    std::uint32_t channels, float dryMix, float wetMix, float roomSize,
    float damp, float width) noexcept
    : bass(bindings), channel(channelHandle), channelCount(channels), resetRequested(0),
      roomFeedbackBits(yarg::audio::bitCast<std::uint32_t>(computeRoomFeedback(roomSize))),
      dampingBits(yarg::audio::bitCast<std::uint32_t>(computeDamping(damp))),
      wetMixBits(yarg::audio::bitCast<std::uint32_t>(clampWet(wetMix))),
      sameChannelWetMixBits(yarg::audio::bitCast<std::uint32_t>(computeSameChannelWet(wetMix, width))),
      crossChannelWetMixBits(yarg::audio::bitCast<std::uint32_t>(computeCrossChannelWet(wetMix, width))),
      dryMixBits(yarg::audio::bitCast<std::uint32_t>(clamp(dryMix, 0.0f, 1.0f))),
      widthBits(yarg::audio::bitCast<std::uint32_t>(clamp01(width))) {}

namespace yarg::audio {

void YARG_BASS_CALLBACK freeverbDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_freeverb_dsp*>(user);
    if (state->resetRequested.exchange(0, std::memory_order_relaxed) != 0) {
        resetState(state);
    }
    process(state, static_cast<float*>(buffer), length / sizeof(float));
}

int freeverbDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float dryMix, float wetMix, float roomSize, float damp, float width,
    int priority, yarg_freeverb_dsp** dsp, int* bassError) noexcept {
    if (dsp) *dsp = nullptr;
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0 || !std::isfinite(dryMix) || !std::isfinite(wetMix) ||
        !std::isfinite(roomSize) || !std::isfinite(damp) || !std::isfinite(width)) {
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

    auto* state = allocateState(bass, channel, info.channels, info.frequency,
        dryMix, wetMix, roomSize, damp, width);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;

    state->dsp = bass.setDsp(channel, &freeverbDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        freeState(state);
        return YARG_AUDIO_ERROR_BASS;
    }

    *dsp = state;
    return YARG_AUDIO_OK;
}

int freeverbDspRequestReset(yarg_freeverb_dsp* dsp) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->resetRequested.store(1, std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

int freeverbDspSetParams(yarg_freeverb_dsp* dsp, const yarg_freeverb_params* params) noexcept {
    if (!dsp || !params || params->size < sizeof(yarg_freeverb_params)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    const float dryMix = params->dry_mix;
    const float wetMix = params->wet_mix;
    const float roomSize = params->room_size;
    const float damp = params->damp;
    const float width = params->width;
    if (!std::isfinite(dryMix) || !std::isfinite(wetMix) ||
        !std::isfinite(roomSize) || !std::isfinite(damp) || !std::isfinite(width)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    const float clampedWet = clampWet(wetMix);
    const float clampedWidth = clamp01(width);
    dsp->dryMixBits.store(
        yarg::audio::bitCast<std::uint32_t>(clamp(dryMix, 0.0f, 1.0f)),
        std::memory_order_relaxed);
    dsp->wetMixBits.store(yarg::audio::bitCast<std::uint32_t>(clampedWet), std::memory_order_relaxed);
    dsp->widthBits.store(yarg::audio::bitCast<std::uint32_t>(clampedWidth), std::memory_order_relaxed);
    dsp->sameChannelWetMixBits.store(
        yarg::audio::bitCast<std::uint32_t>(computeSameChannelWet(clampedWet, clampedWidth)),
        std::memory_order_relaxed);
    dsp->crossChannelWetMixBits.store(
        yarg::audio::bitCast<std::uint32_t>(computeCrossChannelWet(clampedWet, clampedWidth)),
        std::memory_order_relaxed);
    dsp->roomFeedbackBits.store(
        yarg::audio::bitCast<std::uint32_t>(computeRoomFeedback(roomSize)),
        std::memory_order_relaxed);
    dsp->dampingBits.store(
        yarg::audio::bitCast<std::uint32_t>(computeDamping(damp)),
        std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool freeverbDspDestroy(yarg_freeverb_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;

    freeState(dsp);
    return true;
}

} // namespace yarg::audio
