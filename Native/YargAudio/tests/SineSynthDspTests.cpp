#include "dsp/SineSynthDsp.h"
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
    std::uint32_t channelCount = 2;
    std::uint32_t frequency = 48000;
    std::uint32_t floatDspConfig = 0;
    std::uint32_t dspResult = 21;
    int error = 7;
    bool infoSucceeds = true;
    bool lockSucceeds = true;
    bool removeSucceeds = true;
    std::int64_t position = 0;
    double seconds = 0;
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
    info->channels = mock->channelCount;
    info->frequency = mock->frequency;
    return 1;
}
std::uint32_t YARG_BASS_CALL mockGetConfig(std::uint32_t) {
    return mock->floatDspConfig;
}
std::uint64_t YARG_BASS_CALL mockGetPosition(std::uint32_t, std::uint32_t) {
    return mock->position < 0
        ? UINT64_MAX : static_cast<std::uint64_t>(mock->position);
}
double YARG_BASS_CALL mockBytes2Seconds(std::uint32_t, std::uint64_t) {
    return mock->seconds;
}

BassCoreFunctions completeFunctions() {
    BassCoreFunctions functions{};
    functions.setDevice = &mockSetDevice;
    functions.channelGetData = &mockGetData;
    functions.errorGetCode = &mockError;
    functions.channelSetDsp = &mockSetDsp;
    functions.channelRemoveDsp = &mockRemoveDsp;
    functions.channelLock = &mockChannelLock;
    functions.channelGetInfo = &mockGetInfo;
    functions.getConfig = &mockGetConfig;
    functions.channelGetPosition = &mockGetPosition;
    functions.channelBytes2Seconds = &mockBytes2Seconds;
    return functions;
}

yarg_sine_synth_config config(float volume = 1.0f, float fade = 0.015f) {
    yarg_sine_synth_config value{};
    value.size = sizeof(yarg_sine_synth_config);
    value.tempo_stream = 9;
    value.volume = volume;
    value.fade_seconds = fade;
    return value;
}

yarg_sine_synth_dsp* create(BassCoreBindings& bass, MockBass& state,
    float volume = 1.0f, float fade = 0.015f) {
    mock = &state;
    yarg_sine_synth_dsp* dsp = nullptr;
    const auto settings = config(volume, fade);
    REQUIRE(sineSynthDspCreate(bass, &settings, &dsp) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr);
    return dsp;
}

yarg_sine_note note(double start, double end, float startPitch, float endPitch) {
    yarg_sine_note value{};
    value.start_time = start;
    value.end_time = end;
    value.start_pitch = startPitch;
    value.end_pitch = endPitch;
    return value;
}

bool approx(double a, double b, double tolerance = 1e-3) {
    return std::fabs(a - b) <= tolerance;
}

void testFrequencyLookup() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    // A4 = MIDI 69 = 440 Hz, held; then a gap; then an octave slide.
    const yarg_sine_note notes[] = {
        note(1.0, 2.0, 69.0f, 69.0f),
        note(5.0, 6.0, 69.0f, 81.0f),
    };
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 2) == YARG_AUDIO_OK);

    REQUIRE(sineSynthDspFrequencyAt(*dsp, 0.5) == 0.0f);       // before the first note
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 1.0), 440.0));  // onset
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 1.5), 440.0));  // held
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 2.0) == 0.0f);       // exclusive end
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 3.0) == 0.0f);       // gap
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 5.0), 440.0));  // slide start
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 5.5), 622.254, 0.01)); // midpoint
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 7.0) == 0.0f);       // past the end

    REQUIRE(sineSynthDspDestroy(dsp));
}

// A gap longer than the backward seek threshold must not restart the scan. Testing the
// upcoming note's start time instead of the last serviced time made every such gap look like
// a seek, turning an amortized O(1) scan into a full rescan on every sample.
void testForwardGapDoesNotRestartScan() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_sine_note notes[] = {
        note(0.0, 1.0, 60.0f, 60.0f),
        note(10.0, 11.0, 62.0f, 62.0f),
    };
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 2) == YARG_AUDIO_OK);

    REQUIRE(sineSynthDspFrequencyAt(*dsp, 0.5) != 0.0f);
    REQUIRE(dsp->noteIndex == 0);

    // Well inside a 9 second gap, far more than the 0.5 s threshold ahead of the next note.
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 5.0) == 0.0f);
    REQUIRE(dsp->noteIndex == 1);

    // Still in the gap: the index must stay put rather than resetting to 0 and rescanning.
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 5.1) == 0.0f);
    REQUIRE(dsp->noteIndex == 1);

    // A genuine backward jump does restart the scan.
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 0.5) != 0.0f);
    REQUIRE(dsp->noteIndex == 0);

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testRenderFadesAndMixesAdditively() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_sine_note notes[] = {note(0.0, 10.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 1) == YARG_AUDIO_OK);

    constexpr std::size_t frames = 512;
    std::vector<float> buffer(frames * 2, 0.0f);
    sineSynthDspRender(*dsp, buffer.data(), frames, 2, 48000, 0.0, frames / 48000.0);

    // Both channels of a frame receive the same sample.
    for (std::size_t i = 0; i < frames; ++i) {
        REQUIRE(buffer[i * 2] == buffer[i * 2 + 1]);
    }

    // The tone ramps up rather than starting at full volume, and becomes audible.
    REQUIRE(std::fabs(buffer[0]) < 0.05f);
    REQUIRE(dsp->currentVolume > 0.0f);

    float peak = 0.0f;
    for (float sample : buffer) peak = std::max(peak, std::fabs(sample));
    REQUIRE(peak > 0.1f);

    // Existing buffer content is added to, never overwritten.
    std::vector<float> mixed(frames * 2, 1.0f);
    sineSynthDspRender(*dsp, mixed.data(), frames, 2, 48000, 0.1, 0.1 + frames / 48000.0);
    bool changed = false;
    for (float sample : mixed) {
        if (sample != 1.0f) changed = true;
    }
    REQUIRE(changed);

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testSilenceResetsPhase() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_sine_note notes[] = {note(0.0, 0.001, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 1) == YARG_AUDIO_OK);

    constexpr std::size_t frames = 4096;
    std::vector<float> buffer(frames, 0.0f);
    // Render well past the note so the fade completes and the oscillator goes silent.
    sineSynthDspRender(*dsp, buffer.data(), frames, 1, 48000, 0.0, frames / 48000.0);

    REQUIRE(dsp->currentVolume == 0.0f);
    REQUIRE(dsp->phase == 0.0);

    REQUIRE(sineSynthDspDestroy(dsp));
}

// Song time spans less than real time below normal speed, but the emitted pitch must not
// change: phase advances against the sample rate, not the song clock.
void testPlaybackSpeedDoesNotShiftPitch() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    const yarg_sine_note notes[] = {note(0.0, 100.0, 69.0f, 69.0f)};

    constexpr std::size_t frames = 256;
    std::vector<float> full(frames, 0.0f);
    std::vector<float> half(frames, 0.0f);

    auto* normal = create(bass, state);
    REQUIRE(sineSynthDspSetNotes(normal, notes, 1) == YARG_AUDIO_OK);
    normal->currentVolume = 1.0f; // skip the fade so the waveforms are directly comparable
    sineSynthDspRender(*normal, full.data(), frames, 1, 48000, 10.0,
        10.0 + frames / 48000.0);

    auto* slow = create(bass, state);
    REQUIRE(sineSynthDspSetNotes(slow, notes, 1) == YARG_AUDIO_OK);
    slow->currentVolume = 1.0f;
    // Same real duration, half the song time span.
    sineSynthDspRender(*slow, half.data(), frames, 1, 48000, 10.0,
        10.0 + frames / 48000.0 * 0.5);

    for (std::size_t i = 0; i < frames; ++i) {
        REQUIRE(approx(full[i], half[i], 1e-5));
    }

    REQUIRE(sineSynthDspDestroy(normal));
    REQUIRE(sineSynthDspDestroy(slow));
}

void testDspProcReadsSongPosition() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_sine_note notes[] = {note(0.0, 100.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 1) == YARG_AUDIO_OK);

    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 3, &bassError) == YARG_AUDIO_OK);
    REQUIRE(state.callback != nullptr && state.callbackUser == dsp);

    state.seconds = 4.0;
    REQUIRE(sineSynthDspSetTiming(dsp, 1.5, 1.0f) == YARG_AUDIO_OK);

    std::vector<float> buffer(256, 0.0f);
    state.callback(21, 11, buffer.data(), 256 * sizeof(float), state.callbackUser);

    // songTimeEnd = seconds + offset, so the scan ran around 5.5 s.
    REQUIRE(dsp->lastSongTime > 5.0 && dsp->lastSongTime <= 5.5);
    bool audible = false;
    for (float sample : buffer) {
        if (sample != 0.0f) audible = true;
    }
    REQUIRE(audible);

    // A failed position read drops the block instead of emitting anything.
    state.position = -1;
    std::vector<float> untouched(256, 0.0f);
    state.callback(21, 11, untouched.data(), 256 * sizeof(float), state.callbackUser);
    for (float sample : untouched) REQUIRE(sample == 0.0f);
    state.position = 0;

    // Malformed lengths are ignored.
    state.callback(21, 11, buffer.data(), 0, state.callbackUser);
    state.callback(21, 11, nullptr, sizeof(float), state.callbackUser);
    state.callback(21, 11, buffer.data(), sizeof(float) - 1, state.callbackUser);

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testEmptyNoteTableIsSilent() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);

    std::vector<float> buffer(128, 0.0f);
    state.callback(21, 11, buffer.data(), 128 * sizeof(float), state.callbackUser);
    for (float sample : buffer) REQUIRE(sample == 0.0f);

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testCreateAndAttachValidation() {
    MockBass state;
    mock = &state;
    BassCoreBindings bass(completeFunctions());
    yarg_sine_synth_dsp* dsp = reinterpret_cast<yarg_sine_synth_dsp*>(1);

    REQUIRE(sineSynthDspCreate(bass, nullptr, &dsp) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(dsp == nullptr);

    auto bad = config();
    bad.size = 1;
    REQUIRE(sineSynthDspCreate(bass, &bad, &dsp) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    bad = config();
    bad.tempo_stream = 0;
    REQUIRE(sineSynthDspCreate(bass, &bad, &dsp) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    bad = config();
    bad.fade_seconds = 0;
    REQUIRE(sineSynthDspCreate(bass, &bad, &dsp) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    bad = config();
    bad.volume = std::numeric_limits<float>::quiet_NaN();
    REQUIRE(sineSynthDspCreate(bass, &bad, &dsp) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    // Position symbols are required.
    auto incomplete = completeFunctions();
    incomplete.channelGetPosition = nullptr;
    BassCoreBindings missingPosition(incomplete);
    const auto good = config();
    REQUIRE(sineSynthDspCreate(missingPosition, &good, &dsp) ==
        YARG_AUDIO_ERROR_DEPENDENCY);

    incomplete = completeFunctions();
    incomplete.channelSetDsp = nullptr;
    BassCoreBindings missingCore(incomplete);
    REQUIRE(sineSynthDspCreate(missingCore, &good, &dsp) == YARG_AUDIO_ERROR_DEPENDENCY);

    auto* valid = create(bass, state);
    int bassError = -1;
    REQUIRE(sineSynthDspAttach(valid, 0, 0, &bassError) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    state.infoSucceeds = false;
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);
    state.infoSucceeds = true;

    state.channelFlags = 0;
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_ERROR_UNSUPPORTED);
    state.floatDspConfig = 1;
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_OK);
    // Attaching twice without detaching is a state error, not a silent second DSP.
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_ERROR_INVALID_STATE);
    REQUIRE(sineSynthDspDetach(valid) == YARG_AUDIO_OK);
    REQUIRE(sineSynthDspDetach(valid) == YARG_AUDIO_OK);
    state.channelFlags = 0x100;
    state.floatDspConfig = 0;

    state.dspResult = 0;
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);

    REQUIRE(sineSynthDspDestroy(valid));
    REQUIRE(sineSynthDspDestroy(nullptr));
}

void testSetNotesAndTimingValidation() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    REQUIRE(sineSynthDspSetNotes(nullptr, nullptr, 0) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetNotes(dsp, nullptr, 4) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetNotes(dsp, nullptr, 0) == YARG_AUDIO_OK);

    REQUIRE(sineSynthDspSetTiming(nullptr, 0, 1) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetTiming(dsp, std::numeric_limits<double>::quiet_NaN(), 1) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetTiming(dsp, 0, std::numeric_limits<float>::infinity()) ==
        YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetTiming(dsp, 1.0, 0.5f) == YARG_AUDIO_OK);

    // Replacing the table while attached must exclude the render thread.
    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);
    state.events.clear();
    const yarg_sine_note notes[] = {note(0.0, 1.0, 60.0f, 60.0f)};
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 1) == YARG_AUDIO_OK);
    REQUIRE(state.events == std::vector<std::string>({"lock", "unlock"}));
    REQUIRE(dsp->noteIndex == 0);

    state.lockSucceeds = false;
    REQUIRE(sineSynthDspSetNotes(dsp, notes, 1) == YARG_AUDIO_ERROR_BASS);
    state.lockSucceeds = true;

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testDestroyFailurePolicy() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);
    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);

    state.events.clear();
    state.lockSucceeds = false;
    REQUIRE(!sineSynthDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>{"lock"});

    state.events.clear();
    state.lockSucceeds = true;
    state.removeSucceeds = false;
    REQUIRE(!sineSynthDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));

    state.events.clear();
    state.removeSucceeds = true;
    REQUIRE(sineSynthDspDestroy(dsp));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
}

} // namespace

void runSineSynthDspTests() {
    testFrequencyLookup();
    testForwardGapDoesNotRestartScan();
    testRenderFadesAndMixesAdditively();
    testSilenceResetsPhase();
    testPlaybackSpeedDoesNotShiftPitch();
    testDspProcReadsSongPosition();
    testEmptyNoteTableIsSilent();
    testCreateAndAttachValidation();
    testSetNotesAndTimingValidation();
    testDestroyFailurePolicy();
}
