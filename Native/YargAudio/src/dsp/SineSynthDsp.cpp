#include "dsp/SineSynthDsp.h"

#include "BitCastCompat.h"

#include <algorithm>
#include <cmath>
#include <new>

static_assert(std::atomic<std::uint32_t>::is_always_lock_free);
static_assert(std::atomic<std::uint64_t>::is_always_lock_free);

yarg_sine_synth_dsp::yarg_sine_synth_dsp(const yarg::audio::BassCoreBindings& bindings,
    std::uint32_t tempoStreamHandle, float toneVolume, float fade) noexcept
    : bass(bindings), tempoStream(tempoStreamHandle), volume(toneVolume),
      fadeSeconds(fade),
      songTimeOffsetBits(yarg::audio::bitCast<std::uint64_t>(0.0)),
      speedBits(yarg::audio::bitCast<std::uint32_t>(1.0f)) {}

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;
constexpr std::uint32_t BassPositionByte = 0;
constexpr std::uint32_t BassPositionDecode = 0x10000000;

// A backward jump larger than this is a seek (a practice section loop) rather than ordinary
// forward progress, and restarts the scan.
constexpr double BackwardSeekThreshold = 0.5;

constexpr float MinimumSpeed = 0.0001f;
constexpr double Tau = 6.283185307179586476925286766559;

float midiPitchToHz(float midiPitch) noexcept {
    return 440.0f * std::pow(2.0f, (midiPitch - 69.0f) / 12.0f);
}

} // namespace

float sineSynthDspFrequencyAt(yarg_sine_synth_dsp& state, double songTime) noexcept {
    const auto& notes = state.notes;
    if (notes.empty()) return 0.0f;

    // Restart the scan only on a genuine backward jump. The index sits on the next note while
    // in a gap, so testing that note's start time would rescan on every ordinary gap.
    if (state.hasLastSongTime && songTime < state.lastSongTime - BackwardSeekThreshold) {
        state.noteIndex = 0;
    }
    state.lastSongTime = songTime;
    state.hasLastSongTime = true;

    while (state.noteIndex < notes.size() && notes[state.noteIndex].end_time <= songTime) {
        ++state.noteIndex;
    }

    if (state.noteIndex >= notes.size()) return 0.0f;

    const yarg_sine_note& note = notes[state.noteIndex];
    if (songTime < note.start_time) return 0.0f;

    float pitch = note.start_pitch;
    const double span = note.end_time - note.start_time;
    if (span > 0 && note.end_pitch != note.start_pitch) {
        const double progress = std::clamp((songTime - note.start_time) / span, 0.0, 1.0);
        pitch = note.start_pitch +
            static_cast<float>(progress) * (note.end_pitch - note.start_pitch);
    }

    return midiPitchToHz(pitch);
}

void sineSynthDspRender(yarg_sine_synth_dsp& state, float* buffer, std::size_t frames,
    std::uint32_t channels, std::uint32_t sampleRate, double songTimeStart,
    double songTimeEnd) noexcept {
    if (!buffer || frames == 0 || channels == 0 || sampleRate == 0) return;

    const float rampRate = 1.0f / (state.fadeSeconds * static_cast<float>(sampleRate));
    const double step = (songTimeEnd - songTimeStart) / static_cast<double>(frames);
    double songTime = songTimeStart;

    for (std::size_t i = 0; i < frames; ++i) {
        const float frequency = sineSynthDspFrequencyAt(state, songTime);
        songTime += step;

        const float target = frequency > 0.0f ? state.volume : 0.0f;
        if (state.currentVolume < target) {
            state.currentVolume = std::min(state.currentVolume + rampRate, target);
        } else if (state.currentVolume > target) {
            state.currentVolume = std::max(state.currentVolume - rampRate, target);
        }

        // Silent, so there is nothing to mix in. Restart the next tone from zero phase so that
        // it always begins on a zero crossing.
        if (state.currentVolume <= 0.0f) {
            state.phase = 0.0;
            continue;
        }

        const float sample = state.currentVolume * static_cast<float>(std::sin(state.phase * Tau));
        float* frame = buffer + static_cast<std::size_t>(i) * channels;
        for (std::uint32_t ch = 0; ch < channels; ++ch) {
            frame[ch] += sample;
        }

        // A frequency of 0 holds the phase, so that the fade out at the end of a tone ramps
        // down from where the waveform stopped instead of continuing it. Phase advances in
        // real output time, so the tone keeps its written pitch at any playback speed.
        if (frequency > 0.0f) {
            state.phase += static_cast<double>(frequency) / sampleRate;
            if (state.phase >= 1.0) state.phase -= 1.0;
        }
    }
}

void YARG_BASS_CALLBACK sineSynthDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_sine_synth_dsp*>(user);
    if (state->notes.empty()) return;

    BassChannelInfo info{};
    if (!state->bass.getChannelInfo(state->channel, info)) return;
    if (info.channels == 0 || info.frequency == 0) return;

    const auto sampleCount = static_cast<std::size_t>(length) / sizeof(float);
    const std::size_t frames = sampleCount / info.channels;
    if (frames == 0) return;

    // The song mixer pulls the tempo stream synchronously, so its decode position is the song
    // time at the end of the block being processed.
    const std::int64_t bytes = state->bass.getPosition(
        state->tempoStream, BassPositionByte | BassPositionDecode);
    if (bytes < 0) return;

    const double seconds = state->bass.bytesToSeconds(state->tempoStream, bytes);
    if (!(seconds >= 0)) return;

    const double offset = bitCast<double>(
        state->songTimeOffsetBits.load(std::memory_order_relaxed));
    const float speed = bitCast<float>(state->speedBits.load(std::memory_order_relaxed));
    if (!std::isfinite(offset) || !std::isfinite(speed)) return;

    const double songTimeEnd = seconds + offset;
    const double songTimeStart = songTimeEnd -
        (static_cast<double>(frames) / info.frequency) * std::max(MinimumSpeed, speed);

    sineSynthDspRender(*state, static_cast<float*>(buffer), frames, info.channels,
        info.frequency, songTimeStart, songTimeEnd);
}

int sineSynthDspCreate(const BassCoreBindings& bass, const yarg_sine_synth_config* config,
    yarg_sine_synth_dsp** dsp) noexcept {
    if (dsp) *dsp = nullptr;
    if (!dsp || !config || config->size != sizeof(yarg_sine_synth_config))
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (config->tempo_stream == 0 || !std::isfinite(config->volume) ||
        !std::isfinite(config->fade_seconds) || config->fade_seconds <= 0 ||
        config->volume < 0)
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (!bass.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;
    if (!bass.positionValid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    auto* state = new (std::nothrow) yarg_sine_synth_dsp(bass, config->tempo_stream,
        config->volume, config->fade_seconds);
    if (!state) return YARG_AUDIO_ERROR_INTERNAL;

    *dsp = state;
    return YARG_AUDIO_OK;
}

int sineSynthDspAttach(yarg_sine_synth_dsp* dsp, std::uint32_t channel, int priority,
    int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!dsp || channel == 0) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (dsp->dsp != 0) return YARG_AUDIO_ERROR_INVALID_STATE;

    BassChannelInfo info{};
    if (!dsp->bass.getChannelInfo(channel, info)) {
        if (bassError) *bassError = dsp->bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }
    if ((info.flags & BassSampleFloat) == 0 && dsp->bass.getConfig(BassConfigFloatDsp) == 0)
        return YARG_AUDIO_ERROR_UNSUPPORTED;

    dsp->channel = channel;
    dsp->dsp = dsp->bass.setDsp(channel, &sineSynthDspProc, dsp, priority);
    if (dsp->dsp == 0) {
        if (bassError) *bassError = dsp->bass.error();
        dsp->channel = 0;
        return YARG_AUDIO_ERROR_BASS;
    }

    return YARG_AUDIO_OK;
}

int sineSynthDspDetach(yarg_sine_synth_dsp* dsp) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (dsp->dsp == 0) return YARG_AUDIO_OK;

    // Removal can fail if the channel is already gone, which is expected during teardown. Drop
    // the handles either way: the channel cannot outlive this call in a state where the DSP is
    // still reachable, and keeping a stale handle would block a later attach.
    dsp->bass.removeDsp(dsp->channel, dsp->dsp);
    dsp->dsp = 0;
    dsp->channel = 0;
    return YARG_AUDIO_OK;
}

int sineSynthDspSetNotes(yarg_sine_synth_dsp* dsp, const yarg_sine_note* notes,
    std::size_t noteCount) noexcept {
    if (!dsp || (noteCount > 0 && !notes)) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;

    std::vector<yarg_sine_note> replacement;
    try {
        replacement.assign(notes, notes + noteCount);
    } catch (...) {
        return YARG_AUDIO_ERROR_INTERNAL;
    }

    // The render thread reads the table without synchronization, so exclude it while swapping.
    const bool attached = dsp->dsp != 0 && dsp->channel != 0;
    if (attached && !dsp->bass.lockChannel(dsp->channel, true))
        return YARG_AUDIO_ERROR_BASS;

    dsp->notes.swap(replacement);
    dsp->noteIndex = 0;
    dsp->hasLastSongTime = false;

    if (attached) dsp->bass.lockChannel(dsp->channel, false);
    return YARG_AUDIO_OK;
}

int sineSynthDspSetTiming(yarg_sine_synth_dsp* dsp, double songTimeOffset,
    float playbackSpeed) noexcept {
    if (!dsp || !std::isfinite(songTimeOffset) || !std::isfinite(playbackSpeed))
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;

    dsp->songTimeOffsetBits.store(bitCast<std::uint64_t>(songTimeOffset),
        std::memory_order_relaxed);
    dsp->speedBits.store(bitCast<std::uint32_t>(playbackSpeed), std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool sineSynthDspDestroy(yarg_sine_synth_dsp* dsp) noexcept {
    if (!dsp) return true;

    if (dsp->dsp != 0) {
        if (!dsp->bass.lockChannel(dsp->channel, true)) return false;

        const bool removed = dsp->bass.removeDsp(dsp->channel, dsp->dsp);
        dsp->bass.lockChannel(dsp->channel, false);
        if (!removed) return false;

        dsp->dsp = 0;
        dsp->channel = 0;
    }

    delete dsp;
    return true;
}

} // namespace yarg::audio
