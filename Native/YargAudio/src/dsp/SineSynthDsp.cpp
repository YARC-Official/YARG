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
      speedBits(yarg::audio::bitCast<std::uint32_t>(1.0f)),
      outputChannel(0) {}

namespace yarg::audio {
namespace {

constexpr std::uint32_t BassSampleFloat = 0x100;
constexpr std::uint32_t BassConfigFloatDsp = 9;
constexpr std::uint32_t BassPositionByte = 0;
constexpr std::uint32_t BassPositionDecode = 0x10000000;

// BASS_ERROR_HANDLE. A channel that no longer exists cannot be running the proc, so removal
// failing this way means the DSP is already gone rather than still installed.
constexpr int BassErrorHandle = 5;

constexpr float MinimumSpeed = 0.0001f;
constexpr double Tau = 6.283185307179586476925286766559;

float midiPitchToHz(float midiPitch) noexcept {
    // Negative pitch is the non-pitched sentinel (talkies, percussion). It has to be silence
    // rather than a sub-audible rumble the fade never returns from; the managed implementation
    // this replaced carried the same guard.
    if (!(midiPitch >= 0.0f)) return 0.0f;
    return 440.0f * std::pow(2.0f, (midiPitch - 69.0f) / 12.0f);
}

// The render thread scans segments with a forward-only index, so an out-of-order segment
// would be skipped silently rather than reported. Non-finite values are worse: they reach the
// phase accumulator and turn every subsequent sample into NaN, which propagates into the song
// mixer. Reject the whole table instead, as validOneShotSchedule does for its own payload.
bool validSchedule(const yarg_tone_segment* notes, std::size_t count) noexcept {
    for (std::size_t i = 0; i < count; ++i) {
        const yarg_tone_segment& note = notes[i];
        if (!std::isfinite(note.start_time) || !std::isfinite(note.end_time)) return false;
        if (!std::isfinite(note.start_pitch) || !std::isfinite(note.end_pitch)) return false;
        if (note.end_time < note.start_time) return false;
        if (i > 0 && note.start_time < notes[i - 1].start_time) return false;
    }
    return true;
}

} // namespace

float sineSynthDspFrequencyAt(yarg_sine_synth_dsp& state, double songTime) noexcept {
    const auto& notes = state.notes;
    if (notes.empty()) return 0.0f;

    // The index only ever advances, so it is stale exactly when the song has moved back before
    // a segment it already passed -- which is what the previous segment's end time tests.
    // Testing the upcoming segment's start time instead would rescan on every ordinary gap,
    // and a fixed backward-jump threshold silently missed any rewind shorter than it, leaving
    // the replayed notes silent.
    const bool movedBack = state.noteIndex > 0 &&
        state.noteIndex <= notes.size() &&
        songTime < notes[state.noteIndex - 1].end_time;

    if (state.rescan || movedBack) {
        // Reposition rather than restart: rescanning from zero walks the whole song's
        // schedule on the render thread every time a practice section loops.
        const auto target = std::lower_bound(notes.begin(), notes.end(), songTime,
            [](const yarg_tone_segment& note, double time) { return note.end_time <= time; });
        state.noteIndex = static_cast<std::size_t>(target - notes.begin());
        state.rescan = false;
    }

    while (state.noteIndex < notes.size() && notes[state.noteIndex].end_time <= songTime) {
        ++state.noteIndex;
    }

    if (state.noteIndex >= notes.size()) return 0.0f;

    const yarg_tone_segment& note = notes[state.noteIndex];
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

    // Resolve the target speaker pair once per block. A 1-based setting value maps its odd
    // channel to the left of the pair (1 -> index 0, 3 -> index 2, ...). 0, or a value past the
    // device's channels, falls back to writing every channel, so a stereo device and any
    // misconfiguration keep the previous full-mix behaviour instead of going silent.
    const std::uint32_t channelSetting =
        state.outputChannel.load(std::memory_order_relaxed);
    const bool routeToPair = channelSetting != 0 && channelSetting <= channels;
    const std::uint32_t leftChannel = routeToPair ? channelSetting - 1 : 0;
    const bool hasRightChannel = routeToPair && leftChannel + 1 < channels;

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
        if (routeToPair) {
            frame[leftChannel] += sample;
            if (hasRightChannel) {
                frame[leftChannel + 1] += sample;
            }
        } else {
            for (std::uint32_t ch = 0; ch < channels; ++ch) {
                frame[ch] += sample;
            }
        }

        // A frequency of 0 holds the phase, so that the fade out at the end of a tone ramps
        // down from where the waveform stopped instead of continuing it. Phase advances in
        // real output time, so the tone keeps its written pitch at any playback speed.
        if (frequency > 0.0f) {
            state.phase += static_cast<double>(frequency) / sampleRate;
            // Subtracting 1.0 only re-normalizes while the increment stays below a full cycle.
            // Above sampleRate it does not, and the accumulator grows without bound, losing
            // precision in sin() as it goes.
            state.phase -= std::floor(state.phase);
        }
    }
}

void YARG_BASS_CALLBACK sineSynthDspProc(std::uint32_t, std::uint32_t,
    void* buffer, std::uint32_t length, void* user) noexcept {
    if (!buffer || !user || length == 0 || length % sizeof(float) != 0) return;

    auto* state = static_cast<yarg_sine_synth_dsp*>(user);

    // An empty table is not a reason to skip the block. A tone that was sounding when the
    // schedule was cleared still has to ramp down, or it cuts off mid-cycle and clicks.
    // sineSynthDspFrequencyAt reports 0 Hz for an empty table, which drives the fade to zero.

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
    // >=, not ==: the size field exists so a caller built against a newer header with an
    // appended field still works against an older plugin. validOneShotConfig and
    // validReadAheadConfig both compare this way; == would make any future addition to
    // yarg_sine_synth_config a hard break instead of an additive one.
    if (!dsp || !config || config->size < sizeof(yarg_sine_synth_config))
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

    state->outputChannel.store(config->output_channel, std::memory_order_relaxed);

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

int sineSynthDspDetach(yarg_sine_synth_dsp* dsp, int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (dsp->dsp == 0) return YARG_AUDIO_OK;

    const std::uint32_t channel = dsp->channel;

    // Removal has to be serialized against the render thread, as sineSynthDspDestroy does:
    // without the lock a callback can still be in flight when the caller goes on to replace
    // the schedule or delete this object.
    if (!dsp->bass.lockChannel(channel, true)) {
        const int error = dsp->bass.error();
        if (error != BassErrorHandle) {
            if (bassError) *bassError = error;
            return YARG_AUDIO_ERROR_BASS;
        }
        // The channel is gone, so the proc cannot run again. This is the ordinary teardown
        // path, where the mixer is freed before the tone channel is disposed.
        dsp->dsp = 0;
        dsp->channel = 0;
        return YARG_AUDIO_OK;
    }

    const bool removed = dsp->bass.removeDsp(channel, dsp->dsp);
    const int error = removed ? 0 : dsp->bass.error();
    dsp->bass.lockChannel(channel, false);

    // Keep the handles when removal genuinely failed. The proc is still installed, and
    // clearing them would make the next sineSynthDspSetSchedule swap the table without the
    // lock -- freeing the vector under the render thread.
    if (!removed && error != BassErrorHandle) {
        if (bassError) *bassError = error;
        return YARG_AUDIO_ERROR_BASS;
    }

    dsp->dsp = 0;
    dsp->channel = 0;
    return YARG_AUDIO_OK;
}

int sineSynthDspSetSchedule(yarg_sine_synth_dsp* dsp, const yarg_tone_segment* notes,
    std::size_t segmentCount, int* bassError) noexcept {
    if (bassError) *bassError = 0;
    if (!dsp || (segmentCount > 0 && !notes)) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    if (!validSchedule(notes, segmentCount)) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;

    std::vector<yarg_tone_segment> replacement;
    try {
        replacement.assign(notes, notes + segmentCount);
    } catch (...) {
        return YARG_AUDIO_ERROR_INTERNAL;
    }

    // The render thread reads the table without synchronization, so exclude it while swapping.
    const bool attached = dsp->dsp != 0 && dsp->channel != 0;
    if (attached && !dsp->bass.lockChannel(dsp->channel, true)) {
        if (bassError) *bassError = dsp->bass.error();
        return YARG_AUDIO_ERROR_BASS;
    }

    dsp->notes.swap(replacement);
    dsp->noteIndex = 0;
    dsp->rescan = true;

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

int sineSynthDspSetOutputChannel(yarg_sine_synth_dsp* dsp,
    std::uint32_t outputChannel) noexcept {
    if (!dsp) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;

    dsp->outputChannel.store(outputChannel, std::memory_order_relaxed);
    return YARG_AUDIO_OK;
}

bool sineSynthDspDestroy(yarg_sine_synth_dsp* dsp) noexcept {
    if (!dsp) return true;

    // Detach owns the lock/remove/verify sequence; destroying is that plus the delete. Sharing
    // it means a channel that has already been freed tears down cleanly here too, instead of
    // reporting failure and leaking the object with its proc still registered.
    if (sineSynthDspDetach(dsp, nullptr) != YARG_AUDIO_OK) return false;

    delete dsp;
    return true;
}

} // namespace yarg::audio
