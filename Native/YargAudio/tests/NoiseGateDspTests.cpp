#include "dsp/NoiseGateDsp.h"
#include "Test.h"

#include <cmath>
#include <cstdint>
#include <limits>
#include <string>
#include <vector>

using namespace yarg::audio;

namespace {

struct MockBass {
    std::uint32_t channelFlags = 0x100;
    std::uint32_t floatDspConfig = 0;
    std::uint32_t dspResult = 17;
    int error = 5;
    bool infoSucceeds = true;
    bool lockSucceeds = true;
    bool removeSucceeds = true;
    std::uint32_t frequency = 48000;
    std::uint32_t channels = 2;
    BassDspProc callback = nullptr;
    void* callbackUser = nullptr;
    std::vector<std::string> events;
};

MockBass* mock = nullptr;

int YARG_BASS_CALL mockSetDevice(std::uint32_t) { return 1; }
std::uint32_t YARG_BASS_CALL mockGetData(std::uint32_t, void*, std::uint32_t length) {
    return length;
}
int YARG_BASS_CALL mockError() { return mock->error; }
std::uint32_t YARG_BASS_CALL mockSetDsp(
    std::uint32_t, BassDspProc callback, void* user, int) {
    mock->callback = callback;
    mock->callbackUser = user;
    return mock->dspResult;
}
int YARG_BASS_CALL mockRemoveDsp(std::uint32_t, std::uint32_t) {
    mock->events.emplace_back("remove");
    return mock->removeSucceeds;
}
int YARG_BASS_CALL mockChannelLock(std::uint32_t, int lock) {
    mock->events.emplace_back(lock ? "lock" : "unlock");
    return lock ? mock->lockSucceeds : true;
}
int YARG_BASS_CALL mockGetInfo(std::uint32_t, BassChannelInfo* info) {
    if (!mock->infoSucceeds) return 0;
    info->frequency = mock->frequency;
    info->channels = mock->channels;
    info->flags = mock->channelFlags;
    return 1;
}
std::uint32_t YARG_BASS_CALL mockGetConfig(std::uint32_t) {
    return mock->floatDspConfig;
}

BassCoreFunctions completeFunctions() {
    return {&mockSetDevice, &mockGetData, &mockError, &mockSetDsp,
        &mockRemoveDsp, &mockChannelLock, &mockGetInfo, &mockGetConfig};
}

yarg_noise_gate_dsp* attach(BassCoreBindings& bass, MockBass& state,
    float threshold = 0.1f, float floorGain = 0.0f, float attackMs = 0.0f,
    float holdMs = 0.0f, float releaseMs = 0.0f) {
    mock = &state;
    yarg_noise_gate_dsp* dsp = nullptr;
    int bassError = -1;
    REQUIRE(noiseGateDspAttach(bass, 11, threshold, floorGain, attackMs,
        holdMs, releaseMs, 3, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr);
    REQUIRE(bassError == 0);
    REQUIRE(state.callback != nullptr);
    REQUIRE(state.callbackUser == dsp);
    return dsp;
}

void testProcessingAndReset() {
    MockBass state;
    state.channels = 1;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);

    float samples[] = {0.05f, 0.05f, 0.2f, -0.2f};
    state.callback(17, 11, samples, sizeof(samples), state.callbackUser);
    REQUIRE(samples[0] == 0.0f);
    REQUIRE(samples[1] == 0.0f);
    REQUIRE(samples[2] == 0.2f);
    REQUIRE(samples[3] == -0.2f);

    REQUIRE(noiseGateDspRequestReset(dsp) == YARG_AUDIO_OK);
    float silence[] = {0.0f, 0.0f};
    state.callback(17, 11, silence, sizeof(silence), state.callbackUser);
    REQUIRE(silence[0] == 0.0f && silence[1] == 0.0f);
    REQUIRE(noiseGateDspRequestReset(nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(noiseGateDspDestroy(dsp));
}

void testStereoLinkingAndMalformedBuffers() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state, 0.5f, 0.0f);

    float samples[] = {1.0f, 0.0f, 0.0f, 0.0f};
    state.callback(17, 11, samples, sizeof(samples), state.callbackUser);
    REQUIRE(samples[0] == 1.0f && samples[1] == 0.0f);
    REQUIRE(samples[2] == 0.0f && samples[3] == 0.0f);

    float malformed[] = {1.0f, 2.0f};
    state.callback(17, 11, malformed, sizeof(float), state.callbackUser);
    REQUIRE(malformed[0] == 1.0f && malformed[1] == 2.0f);
    state.callback(17, 11, nullptr, sizeof(float), state.callbackUser);
    state.callback(17, 11, malformed, 0, state.callbackUser);
    REQUIRE(noiseGateDspDestroy(dsp));
}

void testSmoothingAcrossBlocks() {
    MockBass state;
    state.channels = 1;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state, 0.1f, 0.0f, 10.0f, 0.0f, 10.0f);

    std::vector<float> silence(4800, 0.0f);
    state.callback(17, 11, silence.data(),
        static_cast<std::uint32_t>(silence.size() * sizeof(float)), state.callbackUser);

    std::vector<float> opening(4800, 1.0f);
    state.callback(17, 11, opening.data(),
        static_cast<std::uint32_t>(opening.size() * sizeof(float)), state.callbackUser);
    REQUIRE(opening.front() < opening.back());
    REQUIRE(opening.back() > 0.5f);

    std::vector<float> closing(4800, 0.01f);
    state.callback(17, 11, closing.data(),
        static_cast<std::uint32_t>(closing.size() * sizeof(float)), state.callbackUser);
    REQUIRE(closing.front() > closing.back());
    REQUIRE(closing.back() < 0.1f);
    REQUIRE(noiseGateDspDestroy(dsp));
}

void testAttachValidation() {
    MockBass state;
    mock = &state;
    BassCoreBindings bass(completeFunctions());
    yarg_noise_gate_dsp* dsp = reinterpret_cast<yarg_noise_gate_dsp*>(1);
    int bassError = -1;

    REQUIRE(noiseGateDspAttach(bass, 0, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dsp == nullptr && bassError == 0);
    REQUIRE(noiseGateDspAttach(bass, 1, std::numeric_limits<float>::quiet_NaN(),
        0, 1, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(noiseGateDspAttach(bass, 1, 0.1f, 2, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    auto incomplete = completeFunctions();
    incomplete.channelSetDsp = nullptr;
    BassCoreBindings missing(incomplete);
    REQUIRE(noiseGateDspAttach(missing, 1, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_DEPENDENCY);

    state.infoSucceeds = false;
    REQUIRE(noiseGateDspAttach(bass, 1, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);
    state.infoSucceeds = true;

    state.frequency = 0;
    REQUIRE(noiseGateDspAttach(bass, 1, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_INVALID_STATE);
    state.frequency = 48000;

    state.channelFlags = 0;
    REQUIRE(noiseGateDspAttach(bass, 1, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_UNSUPPORTED);
    state.floatDspConfig = 1;
    REQUIRE(noiseGateDspAttach(bass, 1, 0.1f, 0, 1, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_OK);
    REQUIRE(noiseGateDspDestroy(dsp));
}

void testDestroyFailurePolicy() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);

    state.lockSucceeds = false;
    REQUIRE(!noiseGateDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>{"lock"});

    state.events.clear();
    state.lockSucceeds = true;
    state.removeSucceeds = false;
    REQUIRE(!noiseGateDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));

    state.events.clear();
    state.removeSucceeds = true;
    REQUIRE(noiseGateDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
}

}

void runNoiseGateDspTests() {
    testProcessingAndReset();
    testStereoLinkingAndMalformedBuffers();
    testSmoothingAcrossBlocks();
    testAttachValidation();
    testDestroyFailurePolicy();
}
