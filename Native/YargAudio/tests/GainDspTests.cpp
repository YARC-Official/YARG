#include "dsp/GainDsp.h"
#include "Test.h"

#include <cmath>
#include <cstdint>
#include <limits>
#include <string>
#include <thread>
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

yarg_gain_dsp* attach(BassCoreBindings& bass, MockBass& state, float gain = 1.0f) {
    mock = &state;
    yarg_gain_dsp* dsp = nullptr;
    int bassError = -1;
    REQUIRE(gainDspAttach(bass, 11, gain, 3, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr);
    REQUIRE(bassError == 0);
    REQUIRE(state.callback != nullptr);
    REQUIRE(state.callbackUser == dsp);
    return dsp;
}

void testProcessing() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);

    float samples[] = {0.25f, -0.5f, 2.0f, -3.0f};
    state.callback(17, 11, samples, sizeof(samples), state.callbackUser);
    REQUIRE(samples[0] == 0.25f);
    REQUIRE(samples[1] == -0.5f);
    REQUIRE(samples[2] == 2.0f);
    REQUIRE(samples[3] == -3.0f);

    REQUIRE(gainDspSetGain(dsp, -2.0f) == YARG_AUDIO_OK);
    state.callback(17, 11, samples, sizeof(samples), state.callbackUser);
    REQUIRE(samples[0] == -0.5f);
    REQUIRE(samples[1] == 1.0f);
    REQUIRE(samples[2] == -4.0f);
    REQUIRE(samples[3] == 6.0f);

    float malformed[] = {1.0f, 2.0f};
    state.callback(17, 11, malformed, sizeof(malformed) - 1, state.callbackUser);
    REQUIRE(malformed[0] == 1.0f && malformed[1] == 2.0f);
    state.callback(17, 11, nullptr, sizeof(float), state.callbackUser);
    state.callback(17, 11, malformed, 0, state.callbackUser);

    REQUIRE(gainDspDestroy(dsp));
}

void testAttachValidation() {
    MockBass state;
    mock = &state;
    BassCoreBindings bass(completeFunctions());
    yarg_gain_dsp* dsp = reinterpret_cast<yarg_gain_dsp*>(1);
    int bassError = -1;

    REQUIRE(gainDspAttach(bass, 0, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dsp == nullptr && bassError == 0);
    REQUIRE(gainDspAttach(bass, 1, std::numeric_limits<float>::quiet_NaN(),
        0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(gainDspAttach(bass, 1, std::numeric_limits<float>::infinity(),
        0, &dsp, &bassError) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    auto incomplete = completeFunctions();
    incomplete.channelSetDsp = nullptr;
    BassCoreBindings missing(incomplete);
    REQUIRE(gainDspAttach(missing, 1, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_DEPENDENCY);

    state.infoSucceeds = false;
    REQUIRE(gainDspAttach(bass, 1, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);
    state.infoSucceeds = true;

    state.channelFlags = 0;
    REQUIRE(gainDspAttach(bass, 1, 1, 0, &dsp, &bassError) ==
        YARG_AUDIO_ERROR_UNSUPPORTED);
    state.floatDspConfig = 1;
    REQUIRE(gainDspAttach(bass, 1, 1, 0, &dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(gainDspDestroy(dsp));

    state.dspResult = 0;
    state.channelFlags = 0x100;
    state.floatDspConfig = 0;
    REQUIRE(gainDspAttach(bass, 1, 1, 0, &dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(dsp == nullptr && bassError == state.error);
}

void testSetGainAndConcurrency() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);
    REQUIRE(gainDspSetGain(nullptr, 1) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(gainDspSetGain(dsp, std::numeric_limits<float>::infinity()) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    std::thread writer([&] {
        for (int i = 0; i < 10000; ++i) {
            REQUIRE(gainDspSetGain(dsp, (i & 1) ? 0.5f : 2.0f) == YARG_AUDIO_OK);
        }
    });
    for (int i = 0; i < 10000; ++i) {
        float sample = 1.0f;
        state.callback(17, 11, &sample, sizeof(sample), state.callbackUser);
        REQUIRE(sample == 0.5f || sample == 1.0f || sample == 2.0f);
    }
    writer.join();
    REQUIRE(gainDspDestroy(dsp));
}

void testDestroyFailurePolicy() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = attach(bass, state);

    state.lockSucceeds = false;
    REQUIRE(!gainDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>{"lock"});

    state.events.clear();
    state.lockSucceeds = true;
    state.removeSucceeds = false;
    REQUIRE(!gainDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));

    state.events.clear();
    state.removeSucceeds = true;
    REQUIRE(gainDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
    REQUIRE(gainDspDestroy(nullptr));
}

} // namespace

void runGainDspTests() {
    testProcessing();
    testAttachValidation();
    testSetGainAndConcurrency();
    testDestroyFailurePolicy();
}
