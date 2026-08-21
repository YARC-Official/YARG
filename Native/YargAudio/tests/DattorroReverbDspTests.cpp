#include "dsp/DattorroReverbDsp.h"
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
    std::uint32_t dspResult = 19;
    int error = 5;
    bool infoSucceeds = true;
    bool lockSucceeds = true;
    bool removeSucceeds = true;
    std::uint32_t frequency = 44100;
    std::uint32_t channels = 2;
    BassDspProc callback = nullptr;
    void* callbackUser = nullptr;
    std::vector<std::string> events;
};

MockBass* mock = nullptr;

int YARG_BASS_CALL mockSetDevice(std::uint32_t) { return 1; }
std::uint32_t YARG_BASS_CALL mockGetData(std::uint32_t, void*, std::uint32_t length) { return length; }
int YARG_BASS_CALL mockError() { return mock->error; }
std::uint32_t YARG_BASS_CALL mockSetDsp(std::uint32_t, BassDspProc callback, void* user, int) {
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
std::uint32_t YARG_BASS_CALL mockGetConfig(std::uint32_t) { return mock->floatDspConfig; }

BassCoreFunctions completeFunctions() {
    return {&mockSetDevice, &mockGetData, &mockError, &mockSetDsp,
        &mockRemoveDsp, &mockChannelLock, &mockGetInfo, &mockGetConfig};
}

yarg_dattorro_reverb_dsp* attach(BassCoreBindings& bass, MockBass& state,
    float dryMix = 0.0f, float wetMix = 1.0f, float roomSize = 0.8f,
    float damp = 0.5f, float width = 1.0f) {
    mock = &state;
    yarg_dattorro_reverb_dsp* dsp = nullptr;
    int bassError = -1;
    REQUIRE(dattorroReverbDspAttach(bass, 11, dryMix, wetMix, roomSize, damp, width, 3, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr);
    REQUIRE(bassError == 0);
    REQUIRE(state.callback != nullptr);
    REQUIRE(state.callbackUser == dsp);
    return dsp;
}

void testDryPathAndMalformedBuffers() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state, 1.0f, 0.0f);
    float samples[] = {0.25f, -0.5f, 2.0f, -3.0f};
    state.callback(19, 11, samples, sizeof(samples), state.callbackUser);
    REQUIRE(samples[0] == 0.25f);
    REQUIRE(samples[1] == -0.5f);
    REQUIRE(samples[2] == 2.0f);
    REQUIRE(samples[3] == -3.0f);
    float malformed[] = {1.0f, 2.0f};
    state.callback(19, 11, malformed, sizeof(malformed) - 1, state.callbackUser);
    REQUIRE(malformed[0] == 1.0f && malformed[1] == 2.0f);
    state.callback(19, 11, nullptr, sizeof(float), state.callbackUser);
    state.callback(19, 11, malformed, 0, state.callbackUser);
    REQUIRE(dattorroReverbDspDestroy(dsp));
}

void testWetPathSilenceAndReset() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);
    std::vector<float> impulse(4000 * state.channels, 0.0f);
    impulse[0] = 1.0f;
    state.callback(19, 11, impulse.data(), static_cast<std::uint32_t>(impulse.size() * sizeof(float)), state.callbackUser);
    bool producedWetSignal = false;
    for (float sample : impulse) {
        if (sample != 0.0f) { producedWetSignal = true; break; }
    }
    REQUIRE(producedWetSignal);
    REQUIRE(dattorroReverbDspRequestReset(dsp) == YARG_AUDIO_OK);
    std::vector<float> silence(64, 0.0f);
    state.callback(19, 11, silence.data(), static_cast<std::uint32_t>(silence.size() * sizeof(float)), state.callbackUser);
    for (float sample : silence) { REQUIRE(sample == 0.0f); }
    REQUIRE(dattorroReverbDspRequestReset(nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dattorroReverbDspDestroy(dsp));
}

void testChannelShapes() {
    for (std::uint32_t channels : {1u, 2u, 3u, 6u}) {
        MockBass state;
        state.channels = channels;
        BassCoreBindings bass(completeFunctions());
        auto* dsp = attach(bass, state, 1.0f, 0.0f);
        std::vector<float> samples(channels * 3);
        for (std::size_t i = 0; i < samples.size(); ++i) samples[i] = static_cast<float>(i) * 0.25f - 1.0f;
        const auto expected = samples;
        state.callback(19, 11, samples.data(), static_cast<std::uint32_t>(samples.size() * sizeof(float)), state.callbackUser);
        REQUIRE(samples == expected);
        REQUIRE(dattorroReverbDspDestroy(dsp));
    }
}

void testAttachValidation() {
    MockBass state;
    mock = &state;
    BassCoreBindings bass(completeFunctions());
    yarg_dattorro_reverb_dsp* dsp = reinterpret_cast<yarg_dattorro_reverb_dsp*>(1);
    int bassError = -1;
    REQUIRE(dattorroReverbDspAttach(bass, 0, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dsp == nullptr && bassError == 0);
    REQUIRE(dattorroReverbDspAttach(bass, 1, std::numeric_limits<float>::quiet_NaN(), 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, std::numeric_limits<float>::infinity(), 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    auto incomplete = completeFunctions();
    incomplete.channelSetDsp = nullptr;
    BassCoreBindings missing(incomplete);
    REQUIRE(dattorroReverbDspAttach(missing, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_DEPENDENCY);
    state.infoSucceeds = false;
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);
    state.infoSucceeds = true;
    state.frequency = 0;
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_STATE);
    state.frequency = 44100;
    state.channelFlags = 0;
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_UNSUPPORTED);
    state.floatDspConfig = 1;
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dattorroReverbDspDestroy(dsp));
    state.dspResult = 0;
    state.channelFlags = 0x100;
    state.floatDspConfig = 0;
    REQUIRE(dattorroReverbDspAttach(bass, 1, 0, 1, 0, 0, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(dsp == nullptr && bassError == state.error);
}

void testDestroyFailurePolicy() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);
    state.lockSucceeds = false;
    REQUIRE(!dattorroReverbDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock"}));
    state.events.clear();
    state.lockSucceeds = true;
    state.removeSucceeds = false;
    REQUIRE(!dattorroReverbDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
    state.events.clear();
    state.removeSucceeds = true;
    REQUIRE(dattorroReverbDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
}

void testParamSetters() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
    yarg_dattorro_reverb_params p{};
    p.size = sizeof(p);
    p.dry_mix = 0.1f; p.wet_mix = 0.9f; p.room_size = 0.6f; p.damp = 0.4f; p.width = 0.8f;
    REQUIRE(dattorroReverbDspSetParams(dsp, &p) == YARG_AUDIO_OK);
    p.dry_mix = std::numeric_limits<float>::quiet_NaN();
    REQUIRE(dattorroReverbDspSetParams(dsp, &p) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    p = {};
    p.size = sizeof(p);
    REQUIRE(dattorroReverbDspSetParams(nullptr, &p) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    p.dry_mix = std::numeric_limits<float>::infinity();
    REQUIRE(dattorroReverbDspSetParams(dsp, &p) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dattorroReverbDspDestroy(dsp));
}

} // namespace

void runDattorroReverbDspTests() {
    testDryPathAndMalformedBuffers();
    testWetPathSilenceAndReset();
    testChannelShapes();
    testAttachValidation();
    testDestroyFailurePolicy();
    testParamSetters();
}
