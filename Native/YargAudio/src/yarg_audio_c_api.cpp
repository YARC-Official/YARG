#include "BassCoreBindings.h"
#include "BassMixBindings.h"
#include "ReadAheadStream.h"
#include "dsp/DattorroReverbDsp.h"
#include "dsp/FreeverbDsp.h"
#include "dsp/GainDsp.h"
#include "dsp/NoiseGateDsp.h"
#include "dsp/SineSynthDsp.h"
#include "one_shot/NativeOneShotStream.h"
#include "yarg_audio.h"

#include <cmath>
#include <cstdint>
#include <limits>
#include <memory>

static_assert(sizeof(yarg_read_ahead_config) == 28);
static_assert(sizeof(yarg_read_ahead_stats) == 104);
static_assert(sizeof(yarg_read_ahead_position_snapshot) == 24);
static_assert(sizeof(yarg_one_shot_config) == 24);
static_assert(sizeof(yarg_freeverb_params) == 24);
static_assert(sizeof(yarg_dattorro_reverb_params) == 24);
static_assert(sizeof(yarg_noise_gate_params) == 24);
static_assert(sizeof(yarg_tone_segment) == 24);
static_assert(sizeof(yarg_sine_synth_config) == 20);
static_assert(sizeof(int32_t) == sizeof(int));

struct yarg_one_shot_stream {
    std::unique_ptr<yarg::audio::NativeOneShotStream> value;
};

struct yarg_read_ahead_stream {
    std::unique_ptr<yarg::audio::ReadAheadStream> value;
};

namespace {

yarg::audio::BassCoreBindings& coreBassBindings() noexcept {
    static yarg::audio::BassCoreBindings bindings;
    static const bool loaded = bindings.load();
    (void) loaded;
    return bindings;
}

yarg::audio::BassMixBindings& mixBassBindings() noexcept {
    static yarg::audio::BassMixBindings bindings;
    static const bool loaded = bindings.load();
    (void) loaded;
    return bindings;
}

bool validOneShotConfig(const yarg_one_shot_config* config) noexcept {
    return config && config->size >= sizeof(yarg_one_shot_config) &&
        config->sample_rate > 0 && config->channels > 0 &&
        std::isfinite(config->lead_time) && config->lead_time >= 0;
}

bool validReadAheadConfig(const yarg_read_ahead_config* config) noexcept {
    return config && config->size >= sizeof(yarg_read_ahead_config) &&
        config->source_mixer != 0 && config->sample_rate > 0 &&
        config->channels > 0 && config->minimum_block_frames > 0;
}

bool validOneShotCounts(std::uint64_t pcmSampleCount,
    std::uint64_t scheduleCount) noexcept {
    constexpr auto maximum = std::numeric_limits<std::size_t>::max();
    return pcmSampleCount <= maximum && scheduleCount <= maximum &&
        pcmSampleCount <= maximum / sizeof(float) &&
        scheduleCount <= maximum / sizeof(double);
}

bool validOneShotSchedule(const double* schedule, std::size_t count) noexcept {
    if (count > 0 && !schedule) return false;
    for (std::size_t i = 0; i < count; ++i) {
        if (!std::isfinite(schedule[i])) return false;
        if (i > 0 && schedule[i] < schedule[i - 1]) return false;
    }
    return true;
}

void storeBassError(int32_t* target, int error) noexcept {
    if (target) *target = static_cast<int32_t>(error);
}

} // namespace

uint32_t YARG_AUDIO_CALL yarg_audio_get_abi_version(void) {
    return YARG_AUDIO_ABI_VERSION;
}

int32_t YARG_AUDIO_CALL yarg_gain_dsp_attach(uint32_t channel,
    float initial_gain, int32_t priority, yarg_gain_dsp** dsp, int32_t* bass_error) {
    return yarg::audio::gainDspAttach(coreBassBindings(), channel, initial_gain,
        priority, dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_gain_dsp_set_gain(yarg_gain_dsp* dsp, float gain) {
    return yarg::audio::gainDspSetGain(dsp, gain);
}

void YARG_AUDIO_CALL yarg_gain_dsp_destroy(yarg_gain_dsp* dsp) {
    (void) yarg::audio::gainDspDestroy(dsp);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_create(
    const yarg_sine_synth_config* config, yarg_sine_synth_dsp** dsp) {
    return yarg::audio::sineSynthDspCreate(coreBassBindings(), config, dsp);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_attach(yarg_sine_synth_dsp* dsp,
    uint32_t channel, int32_t priority, int32_t* bass_error) {
    return yarg::audio::sineSynthDspAttach(dsp, channel, priority, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_detach(yarg_sine_synth_dsp* dsp,
    int32_t* bass_error) {
    return yarg::audio::sineSynthDspDetach(dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_schedule(yarg_sine_synth_dsp* dsp,
    const yarg_tone_segment* notes, uint64_t segment_count, int32_t* bass_error) {
    if (bass_error) *bass_error = 0;
    constexpr auto maximum = std::numeric_limits<std::size_t>::max();
    if (segment_count > maximum / sizeof(yarg_tone_segment))
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    return yarg::audio::sineSynthDspSetSchedule(dsp, notes,
        static_cast<std::size_t>(segment_count), bass_error);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_timing(yarg_sine_synth_dsp* dsp,
    double song_time_offset, float playback_speed) {
    return yarg::audio::sineSynthDspSetTiming(dsp, song_time_offset, playback_speed);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_output_channel(yarg_sine_synth_dsp* dsp,
    uint32_t output_channel) {
    return yarg::audio::sineSynthDspSetOutputChannel(dsp, output_channel);
}

int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_destroy(yarg_sine_synth_dsp* dsp) {
    return yarg::audio::sineSynthDspDestroy(dsp)
        ? YARG_AUDIO_OK : YARG_AUDIO_ERROR_INVALID_STATE;
}

int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_attach(uint32_t channel,
    float dry_mix, float wet_mix, float room_size, float damp, float width,
    int32_t priority, yarg_freeverb_dsp** dsp, int32_t* bass_error) {
    return yarg::audio::freeverbDspAttach(coreBassBindings(), channel, dry_mix,
        wet_mix, room_size, damp, width, priority, dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_reset(yarg_freeverb_dsp* dsp) {
    return yarg::audio::freeverbDspRequestReset(dsp);
}

int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_set_params(yarg_freeverb_dsp* dsp, const yarg_freeverb_params* params) {
    return yarg::audio::freeverbDspSetParams(dsp, params);
}

void YARG_AUDIO_CALL yarg_freeverb_dsp_destroy(yarg_freeverb_dsp* dsp) {
    (void) yarg::audio::freeverbDspDestroy(dsp);
}

int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_attach(uint32_t channel,
    float dry_mix, float wet_mix, float room_size, float damp, float width,
    int32_t priority, yarg_dattorro_reverb_dsp** dsp, int32_t* bass_error) {
    return yarg::audio::dattorroReverbDspAttach(coreBassBindings(), channel, dry_mix,
        wet_mix, room_size, damp, width, priority, dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_reset(yarg_dattorro_reverb_dsp* dsp) {
    return yarg::audio::dattorroReverbDspRequestReset(dsp);
}

int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_set_params(yarg_dattorro_reverb_dsp* dsp, const yarg_dattorro_reverb_params* params) {
    return yarg::audio::dattorroReverbDspSetParams(dsp, params);
}

void YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_destroy(yarg_dattorro_reverb_dsp* dsp) {
    (void) yarg::audio::dattorroReverbDspDestroy(dsp);
}

int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_attach(uint32_t channel,
    float threshold, float floor_gain, float attack_ms, float hold_ms,
    float release_ms, int32_t priority, yarg_noise_gate_dsp** dsp,
    int32_t* bass_error) {
    return yarg::audio::noiseGateDspAttach(coreBassBindings(), channel, threshold,
        floor_gain, attack_ms, hold_ms, release_ms, priority, dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_reset(yarg_noise_gate_dsp* dsp) {
    return yarg::audio::noiseGateDspRequestReset(dsp);
}

int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_set_params(yarg_noise_gate_dsp* dsp, const yarg_noise_gate_params* params) {
    return yarg::audio::noiseGateDspSetParams(dsp, params);
}

void YARG_AUDIO_CALL yarg_noise_gate_dsp_destroy(yarg_noise_gate_dsp* dsp) {
    (void) yarg::audio::noiseGateDspDestroy(dsp);
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_create(
    const yarg_one_shot_config* config, const float* pcm,
    uint64_t pcmSampleCount, const double* schedule, uint64_t scheduleCount,
    yarg_one_shot_stream** stream, int32_t* bassError) {
    if (stream) *stream = nullptr;
    if (bassError) *bassError = 0;
    if (!stream || !validOneShotConfig(config) || !validOneShotCounts(
        pcmSampleCount, scheduleCount) || !pcm || pcmSampleCount == 0 ||
        pcmSampleCount % config->channels != 0 ||
        !validOneShotSchedule(schedule, static_cast<std::size_t>(scheduleCount))) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }

    auto& core = coreBassBindings();
    auto& mix = mixBassBindings();
    if (!core.oneShotValid() || !mix.oneShotValid())
        return YARG_AUDIO_ERROR_DEPENDENCY;

    int error = 0;
    auto value = yarg::audio::NativeOneShotStream::create(core, mix,
        config->sample_rate, config->channels, pcm,
        static_cast<std::size_t>(pcmSampleCount), schedule,
        static_cast<std::size_t>(scheduleCount), config->lead_time, &error);
    if (!value) {
        storeBassError(bassError, error);
        return error != 0 ? YARG_AUDIO_ERROR_BASS : YARG_AUDIO_ERROR_SOURCE;
    }

    try {
        auto result = std::make_unique<yarg_one_shot_stream>();
        result->value = std::move(value);
        *stream = result.release();
        return YARG_AUDIO_OK;
    } catch (...) {
        int cleanupError = 0;
        if (value && !value->destroy(&cleanupError)) value.release();
        return YARG_AUDIO_ERROR_INTERNAL;
    }
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_attach(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchorSongPosition, float playbackSpeed, int32_t paused,
    int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    const int result = stream->value->attach(mixer, anchorSongPosition,
        playbackSpeed, paused != 0, &error);
    storeBassError(bassError, error);
    return result;
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_resync_ex(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchorSongPosition, float playbackSpeed, int32_t clearActiveVoices,
    int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    const int result = stream->value->resync(mixer, anchorSongPosition,
        playbackSpeed, clearActiveVoices != 0, &error);
    storeBassError(bassError, error);
    return result;
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_set_paused(
    yarg_one_shot_stream* stream, uint32_t mixer, int32_t paused,
    int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    const int result = stream->value->setPaused(mixer, paused != 0, &error);
    storeBassError(bassError, error);
    return result;
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_set_gain(
    yarg_one_shot_stream* stream, float gain) {
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    return stream->value->setGain(gain);
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_detach(
    yarg_one_shot_stream* stream, int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    const int result = stream->value->detach(&error);
    storeBassError(bassError, error);
    return result;
}

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_destroy(
    yarg_one_shot_stream* stream, int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    if (!stream->value->destroy(&error)) {
        storeBassError(bassError, error);
        return error != 0 ? YARG_AUDIO_ERROR_BASS : YARG_AUDIO_ERROR_INVALID_STATE;
    }
    storeBassError(bassError, error);
    delete stream;
    return YARG_AUDIO_OK;
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_create(
    const yarg_read_ahead_config* config, yarg_read_ahead_stream** stream,
    uint32_t* streamHandle, int32_t* bassError) {
    if (stream) *stream = nullptr;
    if (streamHandle) *streamHandle = 0;
    if (bassError) *bassError = 0;
    if (!stream || !streamHandle || !validReadAheadConfig(config))
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;

    auto& core = coreBassBindings();
    auto& mix = mixBassBindings();
    if (!core.readAheadValid() || !mix.valid()) return YARG_AUDIO_ERROR_DEPENDENCY;

    int error = 0;
    auto value = yarg::audio::ReadAheadStream::create(core, mix, *config, &error);
    if (!value) {
        storeBassError(bassError, error);
        return error != 0 ? YARG_AUDIO_ERROR_BASS : YARG_AUDIO_ERROR_INTERNAL;
    }

    try {
        auto result = std::make_unique<yarg_read_ahead_stream>();
        *streamHandle = value->streamHandle();
        result->value = std::move(value);
        *stream = result.release();
        return YARG_AUDIO_OK;
    } catch (...) {
        return YARG_AUDIO_ERROR_INTERNAL;
    }
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_prefill(
    yarg_read_ahead_stream* stream, uint32_t timeoutMilliseconds) {
    return stream && stream->value
        ? stream->value->prefill(timeoutMilliseconds)
        : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_set_callback_clock(
    yarg_read_ahead_stream* stream, int32_t enabled) {
    return stream && stream->value
        ? stream->value->setCallbackClockEnabled(enabled != 0)
        : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_flush(
    yarg_read_ahead_stream* stream) {
    return stream && stream->value
        ? stream->value->flush() : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_set_buffer_length(
    yarg_read_ahead_stream* stream, uint32_t bufferMilliseconds) {
    return stream && stream->value
        ? stream->value->setBufferLength(bufferMilliseconds)
        : YARG_AUDIO_ERROR_INVALID_ARGUMENT;
}

int64_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_source_position(
    yarg_read_ahead_stream* stream, uint32_t source,
    uint32_t endpointDelayFrames, int32_t* error) {
    if (!stream || !stream->value || !error) return -1;
    int result = YARG_AUDIO_OK;
    const auto position = stream->value->getSourcePosition(
        source, endpointDelayFrames, result);
    *error = result;
    return position;
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_position_snapshot(
    yarg_read_ahead_stream* stream, uint32_t source,
    uint32_t endpointDelayFrames, yarg_read_ahead_position_snapshot* snapshot) {
    if (!stream || !stream->value || !snapshot ||
        snapshot->size < sizeof(yarg_read_ahead_position_snapshot)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    return stream->value->getPositionSnapshot(
        source, endpointDelayFrames, *snapshot);
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_stats(
    yarg_read_ahead_stream* stream, yarg_read_ahead_stats* stats) {
    if (!stream || !stream->value || !stats ||
        stats->size < sizeof(yarg_read_ahead_stats)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    return stream->value->getStats(*stats);
}

int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_destroy(
    yarg_read_ahead_stream* stream, int32_t* bassError) {
    if (bassError) *bassError = 0;
    if (!stream || !stream->value) return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    int error = 0;
    if (!stream->value->destroy(&error)) {
        storeBassError(bassError, error);
        return error != 0 ? YARG_AUDIO_ERROR_BASS : YARG_AUDIO_ERROR_INVALID_STATE;
    }
    delete stream;
    return YARG_AUDIO_OK;
}
