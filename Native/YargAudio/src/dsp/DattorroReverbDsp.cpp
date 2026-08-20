#include "dsp/DattorroReverbDsp.h"

#include "BitCastCompat.h"

#include <cmath>
#include <cstddef>
#ifdef _MSC_VER
#pragma warning(disable: 4127)
#endif
#include <cstdint>
#include <cstring>
#include <limits>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);

namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;

constexpr std::uint32_t ReferenceSampleRate = 29761;
constexpr int DiffuserCount = 4;

constexpr float FixedGain = 0.015f;
constexpr float ScaleDamp = 0.5f;
constexpr float ScaleRoom = 0.40f;
constexpr float OffsetRoom = 0.30f;
constexpr float ModAllpassFeedback = 0.70f;
constexpr float TankAllpassFeedback = 0.50f;
constexpr float DattorroOutputGain = 15.4f;
constexpr float TwoPi = 6.283185307179586f;
constexpr float ModFreq1 = 0.70f;
constexpr float ModFreq2 = 0.93f;
constexpr int ModDepthRef = 16;

constexpr int DiffuserTunings[DiffuserCount] = {
    142, 107, 379, 277
};

constexpr float DiffuserFeedbacks[DiffuserCount] = {
    0.75f, 0.75f, 0.625f, 0.625f
};

// Tank delay tunings (reference sample rate: 29761 Hz)
constexpr int TankModAllpassL = 672;
constexpr int TankDelay2L = 4453;
constexpr int TankAllpass3L = 1800;
constexpr int TankDelay4L = 3720;

constexpr int TankModAllpassR = 908;
constexpr int TankDelay2R = 4217;
constexpr int TankAllpass3R = 2656;
constexpr int TankDelay4R = 3163;

// Output tap offsets
constexpr int TapL_R2_1 = 266;
constexpr int TapL_R2_2 = 2974;
constexpr int TapL_R3 = 1913;
constexpr int TapL_R4 = 1996;
constexpr int TapL_L2 = 1990;
constexpr int TapL_L3 = 187;
constexpr int TapL_L4 = 1066;

constexpr int TapR_L2_1 = 353;
constexpr int TapR_L2_2 = 3627;
constexpr int TapR_L3 = 1228;
constexpr int TapR_L4 = 2673;
constexpr int TapR_R2 = 2111;
constexpr int TapR_R3 = 335;
constexpr int TapR_R4 = 121;

struct SimpleDelayState {
    int bufferOffset;
    int bufferLength;
    int index;
};

struct ModDelayState {
    int bufferOffset;
    int bufferLength;
    int nominalDelay;
    int index;
};

struct DattorroEngineState {
    SimpleDelayState diffusers[DiffuserCount];
    ModDelayState modAllpassL;
    SimpleDelayState delay2L;
    SimpleDelayState allpass3L;
    SimpleDelayState delay4L;

    ModDelayState modAllpassR;
    SimpleDelayState delay2R;
    SimpleDelayState allpass3R;
    SimpleDelayState delay4R;

    float filterStoreL;
    float filterStoreR;
    float delay4OutL;
    float delay4OutR;
};

static_assert(sizeof(SimpleDelayState) == 12);
static_assert(sizeof(ModDelayState) == 16);

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

std::uint32_t getEngineCount(std::uint32_t channelCount) noexcept {
    return (channelCount + 1u) / 2u;
}

bool calculateDelaySampleCount(std::uint32_t sampleRate, std::uint32_t channelCount,
    std::uint64_t& delaySampleCount) noexcept {
    const std::uint32_t engineCount = getEngineCount(channelCount);
    std::uint64_t total = 0;
    for (std::uint32_t eng = 0; eng < engineCount; ++eng) {
        for (int i = 0; i < DiffuserCount; ++i) {
            total += scaleDelay(DiffuserTunings[i], sampleRate);
            if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        }

        // Left loop
        total += scaleDelay(TankModAllpassL + 32, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankDelay2L, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankAllpass3L, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankDelay4L, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;

        // Right loop
        total += scaleDelay(TankModAllpassR + 32, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankDelay2R, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankAllpass3R, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
        total += scaleDelay(TankDelay4R, sampleRate);
        if (total > static_cast<std::uint64_t>(std::numeric_limits<int>::max())) return false;
    }
    delaySampleCount = total;
    return true;
}

bool calculateAllocationSize(std::uint32_t sampleRate, std::uint32_t channelCount,
    std::uint64_t& totalBytes, std::uint64_t& delaySampleCount) noexcept {
    if (sampleRate == 0 || channelCount == 0 ||
        !calculateDelaySampleCount(sampleRate, channelCount, delaySampleCount)) {
        return false;
    }
    const std::uint32_t engineCount = getEngineCount(channelCount);
    const std::uint64_t engineBytes = static_cast<std::uint64_t>(engineCount) * sizeof(DattorroEngineState);
    const std::uint64_t lfoBytes = static_cast<std::uint64_t>(engineCount) * 2 * sizeof(float);
    const std::uint64_t stateBytes = sizeof(yarg_dattorro_reverb_dsp);
    constexpr std::uint64_t maxStateBytes = std::numeric_limits<int>::max();
    if (delaySampleCount > maxStateBytes ||
        engineBytes > maxStateBytes - stateBytes ||
        lfoBytes > maxStateBytes - stateBytes - engineBytes ||
        delaySampleCount > (maxStateBytes - stateBytes - engineBytes - lfoBytes) / sizeof(float)) {
        return false;
    }
    totalBytes = stateBytes + engineBytes + lfoBytes + delaySampleCount * sizeof(float);
    return true;
}

DattorroEngineState* getEngineStates(yarg_dattorro_reverb_dsp* state) noexcept {
    return reinterpret_cast<DattorroEngineState*>(
        reinterpret_cast<std::byte*>(state) + state->engineStatesOffset);
}

float* getLfoPhases(yarg_dattorro_reverb_dsp* state) noexcept {
    return reinterpret_cast<float*>(
        reinterpret_cast<std::byte*>(state) + state->lfoPhasesOffset);
}

float* getDelaySamples(yarg_dattorro_reverb_dsp* state) noexcept {
    return reinterpret_cast<float*>(
        reinterpret_cast<std::byte*>(state) + state->delaySamplesOffset);
}

void initializeFilterStates(yarg_dattorro_reverb_dsp* state, std::uint32_t sampleRate) noexcept {
    auto* engines = getEngineStates(state);
    const std::uint32_t engineCount = getEngineCount(state->channelCount);
    int sampleOffset = 0;
    for (std::uint32_t eng = 0; eng < engineCount; ++eng) {
        auto& e = engines[eng];
        for (int i = 0; i < DiffuserCount; ++i) {
            e.diffusers[i].bufferOffset = sampleOffset;
            e.diffusers[i].bufferLength = static_cast<int>(scaleDelay(DiffuserTunings[i], sampleRate));
            e.diffusers[i].index = 0;
            sampleOffset += e.diffusers[i].bufferLength;
        }

        // Left loop
        e.modAllpassL.bufferOffset = sampleOffset;
        e.modAllpassL.nominalDelay = static_cast<int>(scaleDelay(TankModAllpassL, sampleRate));
        e.modAllpassL.bufferLength = static_cast<int>(scaleDelay(TankModAllpassL + 32, sampleRate));
        e.modAllpassL.index = 0;
        sampleOffset += e.modAllpassL.bufferLength;

        e.delay2L.bufferOffset = sampleOffset;
        e.delay2L.bufferLength = static_cast<int>(scaleDelay(TankDelay2L, sampleRate));
        e.delay2L.index = 0;
        sampleOffset += e.delay2L.bufferLength;

        e.allpass3L.bufferOffset = sampleOffset;
        e.allpass3L.bufferLength = static_cast<int>(scaleDelay(TankAllpass3L, sampleRate));
        e.allpass3L.index = 0;
        sampleOffset += e.allpass3L.bufferLength;

        e.delay4L.bufferOffset = sampleOffset;
        e.delay4L.bufferLength = static_cast<int>(scaleDelay(TankDelay4L, sampleRate));
        e.delay4L.index = 0;
        sampleOffset += e.delay4L.bufferLength;

        // Right loop
        e.modAllpassR.bufferOffset = sampleOffset;
        e.modAllpassR.nominalDelay = static_cast<int>(scaleDelay(TankModAllpassR, sampleRate));
        e.modAllpassR.bufferLength = static_cast<int>(scaleDelay(TankModAllpassR + 32, sampleRate));
        e.modAllpassR.index = 0;
        sampleOffset += e.modAllpassR.bufferLength;

        e.delay2R.bufferOffset = sampleOffset;
        e.delay2R.bufferLength = static_cast<int>(scaleDelay(TankDelay2R, sampleRate));
        e.delay2R.index = 0;
        sampleOffset += e.delay2R.bufferLength;

        e.allpass3R.bufferOffset = sampleOffset;
        e.allpass3R.bufferLength = static_cast<int>(scaleDelay(TankAllpass3R, sampleRate));
        e.allpass3R.index = 0;
        sampleOffset += e.allpass3R.bufferLength;

        e.delay4R.bufferOffset = sampleOffset;
        e.delay4R.bufferLength = static_cast<int>(scaleDelay(TankDelay4R, sampleRate));
        e.delay4R.index = 0;
        sampleOffset += e.delay4R.bufferLength;

        e.filterStoreL = 0.0f;
        e.filterStoreR = 0.0f;
        e.delay4OutL = 0.0f;
        e.delay4OutR = 0.0f;
    }
}

void resetState(yarg_dattorro_reverb_dsp* state) noexcept {
    auto* engines = getEngineStates(state);
    const std::uint32_t engineCount = getEngineCount(state->channelCount);
    for (std::uint32_t eng = 0; eng < engineCount; ++eng) {
        auto& e = engines[eng];
        for (int i = 0; i < DiffuserCount; ++i) {
            e.diffusers[i].index = 0;
        }
        e.modAllpassL.index = 0;
        e.delay2L.index = 0;
        e.allpass3L.index = 0;
        e.delay4L.index = 0;

        e.modAllpassR.index = 0;
        e.delay2R.index = 0;
        e.allpass3R.index = 0;
        e.delay4R.index = 0;

        e.filterStoreL = 0.0f;
        e.filterStoreR = 0.0f;
        e.delay4OutL = 0.0f;
        e.delay4OutR = 0.0f;
    }
    auto* lfo = getLfoPhases(state);
    for (std::uint32_t i = 0; i < engineCount * 2; ++i) {
        lfo[i] = 0.0f;
    }
    std::memset(getDelaySamples(state), 0,
        static_cast<std::size_t>(state->delaySampleCount) * sizeof(float));
}

inline float undenormalize(float v) noexcept {
    return v > -1e-30f && v < 1e-30f ? 0.0f : v;
}

inline float processAllPass(SimpleDelayState& st, float* base, float input, float feedback) noexcept {
    float* buf = base + st.bufferOffset;
    const float buffered = undenormalize(buf[st.index]);
    const float out = buffered - feedback * input;
    buf[st.index] = undenormalize(input + feedback * out);
    if (++st.index == st.bufferLength) st.index = 0;
    return out;
}

inline float processModAllpass(ModDelayState& st, float* base, float input, float feedback,
    float modDepth, float phase) noexcept {
    float* buf = base + st.bufferOffset;
    const float mod = std::sin(phase) * modDepth;
    const float readOffset = static_cast<float>(st.nominalDelay) + mod;
    const int intPart = static_cast<int>(std::floor(readOffset));
    const float frac = readOffset - static_cast<float>(intPart);

    int posA = st.index + st.bufferLength - intPart;
    posA %= st.bufferLength;
    if (posA < 0) posA += st.bufferLength;

    int posB = posA + st.bufferLength - 1;
    if (posB >= st.bufferLength) posB -= st.bufferLength;

    const float a = undenormalize(buf[posA]);
    const float b = undenormalize(buf[posB]);
    const float buffered = a + frac * (b - a);

    const float out = buffered - feedback * input;
    buf[st.index] = undenormalize(input + feedback * out);
    if (++st.index == st.bufferLength) st.index = 0;
    return out;
}

inline float readTap(const SimpleDelayState& st, const float* base, int tapOffset) noexcept {
    int pos = st.index + st.bufferLength - tapOffset;
    if (pos >= st.bufferLength) pos -= st.bufferLength;
    if (pos < 0) {
        pos %= st.bufferLength;
        if (pos < 0) pos += st.bufferLength;
    }
    return undenormalize(base[st.bufferOffset + pos]);
}

inline void processDattorroEngine(DattorroEngineState& e, float* lfo, float* delaySamples,
    float input, float roomFeedback, float damping, std::uint32_t sampleRate,
    float modDepth, float& outL, float& outR) noexcept {
    float diffused = input;
    for (int i = 0; i < DiffuserCount; ++i) {
        diffused = processAllPass(e.diffusers[i], delaySamples, diffused, DiffuserFeedbacks[i]);
    }

    const float modInc1 = TwoPi * ModFreq1 / static_cast<float>(sampleRate);
    const float modInc2 = TwoPi * ModFreq2 / static_cast<float>(sampleRate);

    const float tankInputL = diffused + e.delay4OutR * roomFeedback;
    const float tankInputR = diffused + e.delay4OutL * roomFeedback;

    // Left Loop
    const float modL1_out = processModAllpass(e.modAllpassL, delaySamples, tankInputL,
        ModAllpassFeedback, modDepth, lfo[0]);
    float* delay2LBuf = delaySamples + e.delay2L.bufferOffset;
    const float delay2L_out = undenormalize(delay2LBuf[e.delay2L.index]);
    delay2LBuf[e.delay2L.index] = undenormalize(modL1_out);
    if (++e.delay2L.index == e.delay2L.bufferLength) e.delay2L.index = 0;

    const float dampL = undenormalize(delay2L_out * (1.0f - damping) + e.filterStoreL * damping);
    e.filterStoreL = dampL;
    const float dampL_scaled = dampL * roomFeedback;

    const float allpass3L_out = processAllPass(e.allpass3L, delaySamples, dampL_scaled, TankAllpassFeedback);
    float* delay4LBuf = delaySamples + e.delay4L.bufferOffset;
    const float delay4L_out = undenormalize(delay4LBuf[e.delay4L.index]);
    delay4LBuf[e.delay4L.index] = undenormalize(allpass3L_out);
    if (++e.delay4L.index == e.delay4L.bufferLength) e.delay4L.index = 0;
    e.delay4OutL = delay4L_out;

    // Right Loop
    const float modR1_out = processModAllpass(e.modAllpassR, delaySamples, tankInputR,
        ModAllpassFeedback, modDepth, lfo[1]);
    float* delay2RBuf = delaySamples + e.delay2R.bufferOffset;
    const float delay2R_out = undenormalize(delay2RBuf[e.delay2R.index]);
    delay2RBuf[e.delay2R.index] = undenormalize(modR1_out);
    if (++e.delay2R.index == e.delay2R.bufferLength) e.delay2R.index = 0;

    const float dampR = undenormalize(delay2R_out * (1.0f - damping) + e.filterStoreR * damping);
    e.filterStoreR = dampR;
    const float dampR_scaled = dampR * roomFeedback;

    const float allpass3R_out = processAllPass(e.allpass3R, delaySamples, dampR_scaled, TankAllpassFeedback);
    float* delay4RBuf = delaySamples + e.delay4R.bufferOffset;
    const float delay4R_out = undenormalize(delay4RBuf[e.delay4R.index]);
    delay4RBuf[e.delay4R.index] = undenormalize(allpass3R_out);
    if (++e.delay4R.index == e.delay4R.bufferLength) e.delay4R.index = 0;
    e.delay4OutR = delay4R_out;

    // Advance LFOs
    lfo[0] += modInc1;
    if (lfo[0] >= TwoPi) lfo[0] -= TwoPi;
    lfo[1] += modInc2;
    if (lfo[1] >= TwoPi) lfo[1] -= TwoPi;

    // Scale tap offsets to current sample rate
    const int tL_R2_1 = static_cast<int>(scaleDelay(TapL_R2_1, sampleRate));
    const int tL_R2_2 = static_cast<int>(scaleDelay(TapL_R2_2, sampleRate));
    const int tL_R3   = static_cast<int>(scaleDelay(TapL_R3, sampleRate));
    const int tL_R4   = static_cast<int>(scaleDelay(TapL_R4, sampleRate));
    const int tL_L2   = static_cast<int>(scaleDelay(TapL_L2, sampleRate));
    const int tL_L3   = static_cast<int>(scaleDelay(TapL_L3, sampleRate));
    const int tL_L4   = static_cast<int>(scaleDelay(TapL_L4, sampleRate));

    const int tR_L2_1 = static_cast<int>(scaleDelay(TapR_L2_1, sampleRate));
    const int tR_L2_2 = static_cast<int>(scaleDelay(TapR_L2_2, sampleRate));
    const int tR_L3   = static_cast<int>(scaleDelay(TapR_L3, sampleRate));
    const int tR_L4   = static_cast<int>(scaleDelay(TapR_L4, sampleRate));
    const int tR_R2   = static_cast<int>(scaleDelay(TapR_R2, sampleRate));
    const int tR_R3   = static_cast<int>(scaleDelay(TapR_R3, sampleRate));
    const int tR_R4   = static_cast<int>(scaleDelay(TapR_R4, sampleRate));

    // Tap summation:
    // Left:  +R2[266] + R2[2974] - R3[1913] + R4[1996] - L2[1990] - L3[187] - L4[1066]
    // Right: +L2[353] + L2[3627] - L3[1228] + L4[2673] - R2[2111] - R3[335] - R4[121]
    const float rawL = readTap(e.delay2R, delaySamples, tL_R2_1) +
                       readTap(e.delay2R, delaySamples, tL_R2_2) -
                       readTap(e.allpass3R, delaySamples, tL_R3) +
                       readTap(e.delay4R, delaySamples, tL_R4) -
                       readTap(e.delay2L, delaySamples, tL_L2) -
                       readTap(e.allpass3L, delaySamples, tL_L3) -
                       readTap(e.delay4L, delaySamples, tL_L4);

    const float rawR = readTap(e.delay2L, delaySamples, tR_L2_1) +
                       readTap(e.delay2L, delaySamples, tR_L2_2) -
                       readTap(e.allpass3L, delaySamples, tR_L3) +
                       readTap(e.delay4L, delaySamples, tR_L4) -
                       readTap(e.delay2R, delaySamples, tR_R2) -
                       readTap(e.allpass3R, delaySamples, tR_R3) -
                       readTap(e.delay4R, delaySamples, tR_R4);

    outL = rawL * DattorroOutputGain;
    outR = rawR * DattorroOutputGain;
}

void process(yarg_dattorro_reverb_dsp* state, float* samples, std::size_t sampleCount) noexcept {
    const std::uint32_t channelCount = state->channelCount;
    const std::size_t frameCount = sampleCount / channelCount;
    auto* engines = getEngineStates(state);
    auto* lfo = getLfoPhases(state);
    float* delays = getDelaySamples(state);

    const float roomFeedback = yarg::audio::bitCast<float>(state->roomFeedbackBits.load(std::memory_order_relaxed));
    const float damping = yarg::audio::bitCast<float>(state->dampingBits.load(std::memory_order_relaxed));
    const float wetMix = yarg::audio::bitCast<float>(state->wetMixBits.load(std::memory_order_relaxed));
    const float sameWet = yarg::audio::bitCast<float>(state->sameChannelWetMixBits.load(std::memory_order_relaxed));
    const float crossWet = yarg::audio::bitCast<float>(state->crossChannelWetMixBits.load(std::memory_order_relaxed));
    const float dryMix = yarg::audio::bitCast<float>(state->dryMixBits.load(std::memory_order_relaxed));
    const float modDepth = static_cast<float>(scaleDelay(ModDepthRef, state->sampleRate));

    for (std::size_t frame = 0; frame < frameCount; ++frame) {
        const std::size_t base = frame * channelCount;
        for (std::uint32_t ch = 0; ch < channelCount; ch += 2) {
            const std::uint32_t engineIdx = ch / 2;
            const std::uint32_t right = ch + 1;
            const bool hasRight = right < channelCount;
            const float leftIn = samples[base + ch];
            const float rightIn = hasRight ? samples[base + right] : leftIn;
            const float monoInput = (hasRight ? leftIn + rightIn : leftIn) * FixedGain;

            float leftWetRaw = 0.0f;
            float rightWetRaw = 0.0f;
            processDattorroEngine(engines[engineIdx], lfo + engineIdx * 2, delays,
                monoInput, roomFeedback, damping, state->sampleRate, modDepth,
                leftWetRaw, rightWetRaw);

            if (hasRight) {
                const float leftOut = leftWetRaw * sameWet + rightWetRaw * crossWet;
                const float rightOut = rightWetRaw * sameWet + leftWetRaw * crossWet;
                samples[base + right] = rightOut + rightIn * dryMix;
                samples[base + ch] = leftOut + leftIn * dryMix;
            } else {
                const float monoOut = leftWetRaw * wetMix;
                samples[base + ch] = monoOut + leftIn * dryMix;
            }
        }
    }
}

yarg_dattorro_reverb_dsp* allocateState(const yarg::audio::BassCoreBindings& bass,
    std::uint32_t channel, std::uint32_t channelCount, std::uint32_t sampleRate,
    float dryMix, float wetMix, float roomSize, float damp, float width) noexcept {
    std::uint64_t totalBytes = 0;
    std::uint64_t delaySampleCount = 0;
    if (!calculateAllocationSize(sampleRate, channelCount, totalBytes, delaySampleCount)) return nullptr;
    void* mem = ::operator new(static_cast<std::size_t>(totalBytes), std::nothrow);
    if (!mem) return nullptr;
    auto* state = new (mem) yarg_dattorro_reverb_dsp(bass, channel, channelCount, sampleRate, dryMix, wetMix, roomSize, damp, width);
    std::memset(reinterpret_cast<std::byte*>(state) + sizeof(yarg_dattorro_reverb_dsp), 0,
        static_cast<std::size_t>(totalBytes) - sizeof(yarg_dattorro_reverb_dsp));

    const std::uint32_t engineCount = getEngineCount(channelCount);
    const std::uint64_t engineBytes = static_cast<std::uint64_t>(engineCount) * sizeof(DattorroEngineState);
    const std::uint64_t lfoBytes = static_cast<std::uint64_t>(engineCount) * 2 * sizeof(float);

    state->engineStatesOffset = static_cast<int>(sizeof(yarg_dattorro_reverb_dsp));
    state->lfoPhasesOffset = state->engineStatesOffset + static_cast<int>(engineBytes);
    state->delaySamplesOffset = state->lfoPhasesOffset + static_cast<int>(lfoBytes);
    state->delaySampleCount = static_cast<int>(delaySampleCount);

    initializeFilterStates(state, sampleRate);
    float* lfo = getLfoPhases(state);
    for (std::uint32_t eng = 0; eng < engineCount; ++eng) {
        lfo[eng * 2 + 0] = 0.0f;
        lfo[eng * 2 + 1] = 1.57079633f;
    }
    return state;
}

void freeState(yarg_dattorro_reverb_dsp* state) noexcept {
    if (!state) return;
    state->~yarg_dattorro_reverb_dsp();
    ::operator delete(state);
}

} // namespace

yarg_dattorro_reverb_dsp::yarg_dattorro_reverb_dsp(
    const yarg::audio::BassCoreBindings& bindings, std::uint32_t channelHandle,
    std::uint32_t channels, std::uint32_t sr,
    float dryMix, float wetMix, float roomSize, float damp, float width) noexcept
    : bass(bindings), channel(channelHandle), channelCount(channels), sampleRate(sr), resetRequested(0),
      roomFeedbackBits(yarg::audio::bitCast<std::uint32_t>(computeRoomFeedback(roomSize))),
      dampingBits(yarg::audio::bitCast<std::uint32_t>(computeDamping(damp))),
      wetMixBits(yarg::audio::bitCast<std::uint32_t>(clampWet(wetMix))),
      sameChannelWetMixBits(yarg::audio::bitCast<std::uint32_t>(computeSameChannelWet(wetMix, width))),
      crossChannelWetMixBits(yarg::audio::bitCast<std::uint32_t>(computeCrossChannelWet(wetMix, width))),
      dryMixBits(yarg::audio::bitCast<std::uint32_t>(clamp(dryMix, 0.0f, 1.0f))),
      widthBits(yarg::audio::bitCast<std::uint32_t>(clamp01(width))) {}

namespace yarg::audio {

void YARG_BASS_CALLBACK dattorroReverbDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;
    auto* state = static_cast<yarg_dattorro_reverb_dsp*>(user);
    if (state->resetRequested.exchange(0, std::memory_order_relaxed) != 0) {
        resetState(state);
    }
    process(state, static_cast<float*>(buffer), length / sizeof(float));
}

int dattorroReverbDspAttach(const BassCoreBindings& bass, std::uint32_t channel,
    float dryMix, float wetMix, float roomSize, float damp, float width,
    int priority, yarg_dattorro_reverb_dsp** dsp, int* bassError) noexcept {
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
    if (info.frequency == 0 || info.channels == 0) return YARG_AUDIO_ERROR_INVALID_STATE;
    if ((info.flags & BassSampleFloat) == 0 && bass.getConfig(BassConfigFloatDsp) == 0) {
        return YARG_AUDIO_ERROR_UNSUPPORTED;
    }
    auto* state = allocateState(bass, channel, info.channels, info.frequency,
        dryMix, wetMix, roomSize, damp, width);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;
    state->dsp = bass.setDsp(channel, &dattorroReverbDspProc, state, priority);
    if (state->dsp == 0) {
        if (bassError) *bassError = bass.error();
        freeState(state);
        return YARG_AUDIO_ERROR_BASS;
    }
    *dsp = state;
    return YARG_AUDIO_OK;
}

int dattorroReverbDspRequestReset(yarg_dattorro_reverb_dsp* dsp) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    dsp->resetRequested.store(1, std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

int dattorroReverbDspSetParams(yarg_dattorro_reverb_dsp* dsp, const yarg_dattorro_reverb_params* params) noexcept {
    if (!dsp || !params || params->size < sizeof(yarg_dattorro_reverb_params)) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
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
    dsp->dryMixBits.store(yarg::audio::bitCast<std::uint32_t>(clamp(dryMix, 0.0f, 1.0f)), std::memory_order_relaxed);
    dsp->wetMixBits.store(yarg::audio::bitCast<std::uint32_t>(clampedWet), std::memory_order_relaxed);
    dsp->widthBits.store(yarg::audio::bitCast<std::uint32_t>(clampedWidth), std::memory_order_relaxed);
    dsp->sameChannelWetMixBits.store(yarg::audio::bitCast<std::uint32_t>(computeSameChannelWet(clampedWet, clampedWidth)), std::memory_order_relaxed);
    dsp->crossChannelWetMixBits.store(yarg::audio::bitCast<std::uint32_t>(computeCrossChannelWet(clampedWet, clampedWidth)), std::memory_order_relaxed);
    dsp->roomFeedbackBits.store(yarg::audio::bitCast<std::uint32_t>(computeRoomFeedback(roomSize)), std::memory_order_relaxed);
    dsp->dampingBits.store(yarg::audio::bitCast<std::uint32_t>(computeDamping(damp)), std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool dattorroReverbDspDestroy(yarg_dattorro_reverb_dsp* dsp) noexcept {
    if (!dsp) return true;
    if (!dsp->bass.lockChannel(dsp->channel, true)) return false;
    const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->bass.lockChannel(dsp->channel, false);
    if (!removed) return false;
    freeState(dsp);
    return true;
}

} // namespace yarg::audio
