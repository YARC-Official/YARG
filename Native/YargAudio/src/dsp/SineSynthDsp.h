#pragma once

#include "BassCoreBindings.h"
#include "yarg_audio.h"

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

struct yarg_sine_synth_dsp {
    yarg_sine_synth_dsp(const yarg::audio::BassCoreBindings& bindings,
        std::uint32_t tempoStreamHandle, float toneVolume, float fadeSeconds) noexcept;

    const yarg::audio::BassCoreBindings& bass;
    std::uint32_t tempoStream;
    std::uint32_t channel = 0;
    std::uint32_t dsp = 0;
    float volume;
    float fadeSeconds;

    // Published by the game thread, read by the render thread. Together these map a tempo
    // stream position in seconds onto a song position: song = seconds + offset.
    std::atomic<std::uint64_t> songTimeOffsetBits;
    std::atomic<std::uint32_t> speedBits;

    // 1-based output channel (the odd channel of a speaker pair), or 0 to broadcast to every
    // channel. Published by the game thread when the setting changes, read on the render thread.
    std::atomic<std::uint32_t> outputChannel;

    // Tone schedule, sorted by start time. Replaced under the channel lock while attached, so
    // the render thread never observes a partially written table.
    std::vector<yarg_tone_segment> notes;

    // Render-thread-only scan and oscillator state.
    std::size_t noteIndex = 0;
    // Forces the next lookup to reposition the index instead of walking forward to it. Set
    // whenever the table is replaced, so a new schedule does not scan from the start.
    bool rescan = true;
    double phase = 0;
    float currentVolume = 0;
};

namespace yarg::audio {

void YARG_BASS_CALLBACK sineSynthDspProc(std::uint32_t dsp, std::uint32_t channel,
    void* buffer, std::uint32_t length, void* user) noexcept;

int sineSynthDspCreate(const BassCoreBindings& bass,
    const yarg_sine_synth_config* config, yarg_sine_synth_dsp** dsp) noexcept;
int sineSynthDspAttach(yarg_sine_synth_dsp* dsp, std::uint32_t channel,
    int priority, int* bassError) noexcept;
int sineSynthDspDetach(yarg_sine_synth_dsp* dsp, int* bassError) noexcept;
int sineSynthDspSetSchedule(yarg_sine_synth_dsp* dsp, const yarg_tone_segment* notes,
    std::size_t segmentCount, int* bassError) noexcept;
int sineSynthDspSetTiming(yarg_sine_synth_dsp* dsp, double songTimeOffset,
    float playbackSpeed) noexcept;
int sineSynthDspSetOutputChannel(yarg_sine_synth_dsp* dsp,
    std::uint32_t outputChannel) noexcept;

// Returns false when state must remain allocated because detach was not safe.
bool sineSynthDspDestroy(yarg_sine_synth_dsp* dsp) noexcept;

// Exposed for tests: renders one block into an interleaved float buffer.
void sineSynthDspRender(yarg_sine_synth_dsp& state, float* buffer, std::size_t frames,
    std::uint32_t channels, std::uint32_t sampleRate, double songTimeStart,
    double songTimeEnd) noexcept;

// Exposed for tests: frequency in Hz of the schedule at a song time, or 0 in a gap.
float sineSynthDspFrequencyAt(yarg_sine_synth_dsp& state, double songTime) noexcept;

} // namespace yarg::audio
