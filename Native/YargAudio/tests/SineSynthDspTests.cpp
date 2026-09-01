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

yarg_tone_segment note(double start, double end, float startPitch, float endPitch) {
    yarg_tone_segment value{};
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
    const yarg_tone_segment notes[] = {
        note(1.0, 2.0, 69.0f, 69.0f),
        note(5.0, 6.0, 69.0f, 81.0f),
    };
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 2, nullptr) == YARG_AUDIO_OK);

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

    const yarg_tone_segment notes[] = {
        note(0.0, 1.0, 60.0f, 60.0f),
        note(10.0, 11.0, 62.0f, 62.0f),
    };
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 2, nullptr) == YARG_AUDIO_OK);

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

    const yarg_tone_segment notes[] = {note(0.0, 10.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);

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

// Sums the absolute tone energy each channel of an interleaved block received. A channel the
// tone never wrote stays exactly zero, so a zero sum is a reliable "silent here" assertion.
std::vector<double> channelEnergy(const std::vector<float>& buffer, std::uint32_t channels) {
    std::vector<double> energy(channels, 0.0);
    for (std::size_t frame = 0; frame * channels < buffer.size(); ++frame) {
        for (std::uint32_t ch = 0; ch < channels; ++ch) {
            energy[ch] += std::fabs(buffer[frame * channels + ch]);
        }
    }
    return energy;
}

// The guide tone must land only on the configured output pair, so a multichannel setup does not
// hear it from every speaker. This is the behaviour the managed layer drives from the
// experimental default-channel setting.
void testOutputChannelRoutesToConfiguredPair() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_tone_segment notes[] = {note(0.0, 10.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);

    constexpr std::size_t frames = 512;
    constexpr std::uint32_t channels = 6;

    auto renderFresh = [&](std::uint32_t channelSetting) {
        REQUIRE(sineSynthDspSetOutputChannel(dsp, channelSetting) == YARG_AUDIO_OK);
        // Reset the oscillator so each render starts from the same silence-to-tone ramp.
        REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);
        dsp->currentVolume = 0.0f;
        std::vector<float> buffer(frames * channels, 0.0f);
        sineSynthDspRender(*dsp, buffer.data(), frames, channels, 48000, 0.0,
            frames / 48000.0);
        return channelEnergy(buffer, channels);
    };

    // Value 1 -> the first pair (indices 0 and 1); the other four channels stay silent.
    {
        const auto energy = renderFresh(1);
        REQUIRE(energy[0] > 0.0);
        REQUIRE(energy[1] > 0.0);
        REQUIRE(energy[0] == energy[1]);
        for (std::uint32_t ch = 2; ch < channels; ++ch) REQUIRE(energy[ch] == 0.0);
    }

    // Value 3 -> the second pair (indices 2 and 3); nothing leaks to the others.
    {
        const auto energy = renderFresh(3);
        for (std::uint32_t ch = 0; ch < channels; ++ch) {
            const bool inPair = ch == 2 || ch == 3;
            REQUIRE((energy[ch] > 0.0) == inPair);
        }
    }

    // 0 keeps the fallback of broadcasting to every channel (used for stereo and for an
    // unset value), so nothing regresses for a plain two-channel device.
    {
        const auto energy = renderFresh(0);
        for (std::uint32_t ch = 0; ch < channels; ++ch) REQUIRE(energy[ch] > 0.0);
    }

    // A value past the device's channel count falls back to broadcasting rather than writing
    // out of bounds or going silent.
    {
        const auto energy = renderFresh(channels + 1);
        for (std::uint32_t ch = 0; ch < channels; ++ch) REQUIRE(energy[ch] > 0.0);
    }

    REQUIRE(sineSynthDspDestroy(dsp));
}

// An odd-channel device (e.g. 4.1 with five speakers) exposes its final channel on its own. The
// tone must drive that single channel without touching the phantom sixth index past the buffer.
void testOutputChannelMonoTail() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_tone_segment notes[] = {note(0.0, 10.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);
    REQUIRE(sineSynthDspSetOutputChannel(dsp, 5) == YARG_AUDIO_OK);

    constexpr std::size_t frames = 256;
    constexpr std::uint32_t channels = 5;
    std::vector<float> buffer(frames * channels, 0.0f);
    sineSynthDspRender(*dsp, buffer.data(), frames, channels, 48000, 0.0, frames / 48000.0);

    const auto energy = channelEnergy(buffer, channels);
    for (std::uint32_t ch = 0; ch < channels; ++ch) REQUIRE((energy[ch] > 0.0) == (ch == 4));

    REQUIRE(sineSynthDspDestroy(dsp));
}

// The setter validates its handle and its result is observable through the config on create.
void testOutputChannelValidationAndCreateConfig() {
    REQUIRE(sineSynthDspSetOutputChannel(nullptr, 1) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    MockBass state;
    BassCoreBindings bass(completeFunctions());
    mock = &state;

    auto settings = config();
    settings.output_channel = 3;
    yarg_sine_synth_dsp* dsp = nullptr;
    REQUIRE(sineSynthDspCreate(bass, &settings, &dsp) == YARG_AUDIO_OK);
    REQUIRE(dsp != nullptr);
    REQUIRE(dsp->outputChannel.load() == 3);

    REQUIRE(sineSynthDspSetOutputChannel(dsp, 1) == YARG_AUDIO_OK);
    REQUIRE(dsp->outputChannel.load() == 1);

    REQUIRE(sineSynthDspDestroy(dsp));
}

void testSilenceResetsPhase() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_tone_segment notes[] = {note(0.0, 0.001, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);

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
    const yarg_tone_segment notes[] = {note(0.0, 100.0, 69.0f, 69.0f)};

    constexpr std::size_t frames = 256;
    std::vector<float> full(frames, 0.0f);
    std::vector<float> half(frames, 0.0f);

    auto* normal = create(bass, state);
    REQUIRE(sineSynthDspSetSchedule(normal, notes, 1, nullptr) == YARG_AUDIO_OK);
    normal->currentVolume = 1.0f; // skip the fade so the waveforms are directly comparable
    sineSynthDspRender(*normal, full.data(), frames, 1, 48000, 10.0,
        10.0 + frames / 48000.0);

    auto* slow = create(bass, state);
    REQUIRE(sineSynthDspSetSchedule(slow, notes, 1, nullptr) == YARG_AUDIO_OK);
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

    const yarg_tone_segment notes[] = {
        note(0.0, 1.0, 69.0f, 69.0f),
        note(5.0, 6.0, 69.0f, 69.0f),
    };
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 2, nullptr) == YARG_AUDIO_OK);

    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 3, &bassError) == YARG_AUDIO_OK);
    REQUIRE(state.callback != nullptr && state.callbackUser == dsp);

    state.seconds = 4.0;
    REQUIRE(sineSynthDspSetTiming(dsp, 1.5, 1.0f) == YARG_AUDIO_OK);

    std::vector<float> buffer(256, 0.0f);
    state.callback(21, 11, buffer.data(), 256 * sizeof(float), state.callbackUser);

    // songTimeEnd = seconds + offset = 5.5, which lands inside the second segment.
    REQUIRE(dsp->noteIndex == 1);
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

// Clearing the schedule while a tone sounds must fade it out rather than cut it off. This is
// the path taken when the player toggles the guide pitch off mid-note.
void testClearingScheduleFadesOutInsteadOfCuttingOff() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    // A 1 ms fade ramps in 48 frames, so a single 128-frame block covers a full ramp.
    auto* dsp = create(bass, state, 1.0f, 0.001f);

    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);

    const yarg_tone_segment notes[] = {note(0.0, 10.0, 69.0f, 69.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);

    // 128 frames of a held note: the tone reaches full volume.
    std::vector<float> buffer(256, 0.0f);
    state.seconds = 1.0;
    state.callback(21, 11, buffer.data(), 256 * sizeof(float), state.callbackUser);
    REQUIRE(approx(dsp->currentVolume, 1.0));

    REQUIRE(sineSynthDspSetSchedule(dsp, nullptr, 0, nullptr) == YARG_AUDIO_OK);

    buffer.assign(256, 0.0f);
    state.seconds = 1.005;
    state.callback(21, 11, buffer.data(), 256 * sizeof(float), state.callbackUser);

    // The ramp down has to be audible in the block, and must land on silence at zero phase so
    // the next tone starts on a zero crossing.
    bool rendered = false;
    for (float sample : buffer) {
        if (sample != 0.0f) rendered = true;
    }
    REQUIRE(rendered);
    REQUIRE(dsp->currentVolume == 0.0f);
    REQUIRE(dsp->phase == 0.0);

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
    REQUIRE(sineSynthDspDetach(valid, nullptr) == YARG_AUDIO_OK);
    REQUIRE(sineSynthDspDetach(valid, nullptr) == YARG_AUDIO_OK);
    state.channelFlags = 0x100;
    state.floatDspConfig = 0;

    state.dspResult = 0;
    REQUIRE(sineSynthDspAttach(valid, 11, 0, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == state.error);

    REQUIRE(sineSynthDspDestroy(valid));
    REQUIRE(sineSynthDspDestroy(nullptr));
}

void testSetScheduleAndTimingValidation() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    REQUIRE(sineSynthDspSetSchedule(nullptr, nullptr, 0, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetSchedule(dsp, nullptr, 4, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);
    REQUIRE(sineSynthDspSetSchedule(dsp, nullptr, 0, nullptr) == YARG_AUDIO_OK);

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
    const yarg_tone_segment notes[] = {note(0.0, 1.0, 60.0f, 60.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_OK);
    REQUIRE(state.events == std::vector<std::string>({"lock", "unlock"}));
    REQUIRE(dsp->noteIndex == 0);

    state.lockSucceeds = false;
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 1, nullptr) == YARG_AUDIO_ERROR_BASS);
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

// Detach must exclude the render thread while removing the proc, and must keep its handles
// when removal genuinely fails: clearing them would make the next set_schedule swap the table
// unlocked while the still-installed proc reads it.
void testDetachLocksAndKeepsHandlesOnFailure() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    int bassError = -1;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);

    state.events.clear();
    REQUIRE(sineSynthDspDetach(dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
    REQUIRE(dsp->dsp == 0);

    // Removal failing for anything other than a freed channel leaves the DSP installed.
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);
    state.events.clear();
    state.removeSucceeds = false;
    state.error = 7;
    REQUIRE(sineSynthDspDetach(dsp, &bassError) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(bassError == 7);
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
    REQUIRE(dsp->dsp != 0);

    // A channel that is already gone cannot be running the proc, so that is a clean detach.
    state.error = 5; // BASS_ERROR_HANDLE
    REQUIRE(sineSynthDspDetach(dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp->dsp == 0);

    // The same applies when it is the lock that reports the channel is gone.
    state.removeSucceeds = true;
    REQUIRE(sineSynthDspAttach(dsp, 11, 0, &bassError) == YARG_AUDIO_OK);
    state.lockSucceeds = false;
    REQUIRE(sineSynthDspDetach(dsp, &bassError) == YARG_AUDIO_OK);
    REQUIRE(dsp->dsp == 0);

    state.lockSucceeds = true;
    REQUIRE(sineSynthDspDestroy(dsp));
}

// A schedule the render thread cannot scan correctly must be rejected outright rather than
// silently mis-rendered, and non-finite values must never reach the phase accumulator.
void testScheduleValidation() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_tone_segment good[] = {note(0.0, 1.0, 60.0f, 60.0f), note(2.0, 3.0, 62.0f, 62.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, good, 2, nullptr) == YARG_AUDIO_OK);

    const yarg_tone_segment unsorted[] = {note(2.0, 3.0, 60.0f, 60.0f), note(0.0, 1.0, 62.0f, 62.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, unsorted, 2, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    const yarg_tone_segment inverted[] = {note(3.0, 2.0, 60.0f, 60.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, inverted, 1, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    const double nan = std::numeric_limits<double>::quiet_NaN();
    const float infinity = std::numeric_limits<float>::infinity();
    const yarg_tone_segment badTime[] = {note(nan, 1.0, 60.0f, 60.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, badTime, 1, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    const yarg_tone_segment badPitch[] = {note(0.0, 1.0, infinity, 60.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, badPitch, 1, nullptr) == YARG_AUDIO_ERROR_INVALID_ARGUMENT);

    // A rejected table leaves the previous one in place rather than clearing it.
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 0.5), 261.626, 0.01));

    // Non-pitched notes are silence, not a 7.7 Hz rumble.
    const yarg_tone_segment nonPitched[] = {note(0.0, 1.0, -1.0f, -1.0f)};
    REQUIRE(sineSynthDspSetSchedule(dsp, nonPitched, 1, nullptr) == YARG_AUDIO_OK);
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 0.5) == 0.0f);

    REQUIRE(sineSynthDspDestroy(dsp));
}

// A rewind shorter than the old fixed threshold still has to replay the notes it moved back
// over, and a replaced table must not make the render thread walk the whole song to catch up.
void testShortRewindReplaysNotes() {
    MockBass state;
    BassCoreBindings bass(completeFunctions());
    auto* dsp = create(bass, state);

    const yarg_tone_segment notes[] = {
        note(10.0, 10.2, 69.0f, 69.0f),
        note(11.0, 11.2, 71.0f, 71.0f),
    };
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 2, nullptr) == YARG_AUDIO_OK);

    // Play past the first note, so the index has advanced beyond it.
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 10.1), 440.0));
    REQUIRE(sineSynthDspFrequencyAt(*dsp, 10.4) == 0.0f);
    REQUIRE(dsp->noteIndex == 1);

    // Rewind 0.3 s -- less than the 0.5 s threshold this used to require. The note has to
    // sound again rather than be skipped as silence.
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 10.1), 440.0));
    REQUIRE(dsp->noteIndex == 0);

    // Replacing the table repositions rather than rescanning from the start.
    REQUIRE(sineSynthDspSetSchedule(dsp, notes, 2, nullptr) == YARG_AUDIO_OK);
    REQUIRE(approx(sineSynthDspFrequencyAt(*dsp, 11.1), 493.883, 0.01));
    REQUIRE(dsp->noteIndex == 1);

    REQUIRE(sineSynthDspDestroy(dsp));
}

} // namespace

void runSineSynthDspTests() {
    testFrequencyLookup();
    testForwardGapDoesNotRestartScan();
    testRenderFadesAndMixesAdditively();
    testOutputChannelRoutesToConfiguredPair();
    testOutputChannelMonoTail();
    testOutputChannelValidationAndCreateConfig();
    testSilenceResetsPhase();
    testPlaybackSpeedDoesNotShiftPitch();
    testDspProcReadsSongPosition();
    testEmptyNoteTableIsSilent();
    testClearingScheduleFadesOutInsteadOfCuttingOff();
    testCreateAndAttachValidation();
    testSetScheduleAndTimingValidation();
    testDestroyFailurePolicy();
    testDetachLocksAndKeepsHandlesOnFailure();
    testScheduleValidation();
    testShortRewindReplaysNotes();
}
