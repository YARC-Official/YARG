#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define YARG_AUDIO_CALL __cdecl
#if defined(YARG_AUDIO_BUILD)
#define YARG_AUDIO_API __declspec(dllexport)
#else
#define YARG_AUDIO_API __declspec(dllimport)
#endif
#else
#define YARG_AUDIO_CALL
#define YARG_AUDIO_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define YARG_AUDIO_ABI_VERSION 8u

typedef struct yarg_read_ahead_stream yarg_read_ahead_stream;
typedef struct yarg_gain_dsp yarg_gain_dsp;
typedef struct yarg_freeverb_dsp yarg_freeverb_dsp;
typedef struct yarg_dattorro_reverb_dsp yarg_dattorro_reverb_dsp;
typedef struct yarg_noise_gate_dsp yarg_noise_gate_dsp;
typedef struct yarg_one_shot_stream yarg_one_shot_stream;
typedef struct yarg_sine_synth_dsp yarg_sine_synth_dsp;

typedef enum yarg_audio_result {
    YARG_AUDIO_OK = 0,
    YARG_AUDIO_ERROR_INVALID_ARGUMENT = -1,
    YARG_AUDIO_ERROR_INVALID_STATE = -2,
    YARG_AUDIO_ERROR_UNSUPPORTED = -3,
    YARG_AUDIO_ERROR_DEPENDENCY = -4,
    YARG_AUDIO_ERROR_BASS = -5,
    YARG_AUDIO_ERROR_INTERNAL = -6,
    YARG_AUDIO_ERROR_SOURCE = -7,
    YARG_AUDIO_ERROR_TIMEOUT = -8
} yarg_audio_result;

typedef struct yarg_one_shot_config {
    uint32_t size;
    uint32_t sample_rate;
    uint32_t channels;
    uint32_t reserved;
    double lead_time;
} yarg_one_shot_config;
/* One segment of a tone schedule, in song seconds and MIDI pitch. A segment with equal start
   and end pitch is held; unequal pitches are interpolated linearly across the segment. */
typedef struct yarg_tone_segment {
    double start_time;
    double end_time;
    float start_pitch;
    float end_pitch;
} yarg_tone_segment;

typedef struct yarg_sine_synth_config {
    uint32_t size;
    uint32_t tempo_stream;
    float volume;
    float fade_seconds;
    /* 1-based output channel (the odd channel of a speaker pair, matching the experimental
       output-channel setting): 1 routes the tone to the first pair, 3 to the second, and so on.
       0 broadcasts to every channel, which is also the fallback when the value exceeds the
       device's channel count. */
    uint32_t output_channel;
} yarg_sine_synth_config;

typedef enum yarg_read_ahead_state {
    YARG_READ_AHEAD_CREATED = 0,
    YARG_READ_AHEAD_EMPTY = 1,
    YARG_READ_AHEAD_PREFILLING = 2,
    YARG_READ_AHEAD_READY = 3,
    YARG_READ_AHEAD_RUNNING = 4,
    YARG_READ_AHEAD_STARVED = 5,
    YARG_READ_AHEAD_SOURCE_FAILED = 6,
    YARG_READ_AHEAD_STOPPING = 7,
    YARG_READ_AHEAD_STOPPED = 8
} yarg_read_ahead_state;

typedef struct yarg_read_ahead_config {
    uint32_t size;
    int32_t bass_device_id;
    uint32_t source_mixer;
    uint32_t sample_rate;
    uint32_t channels;
    uint32_t minimum_block_frames;
    uint32_t buffer_milliseconds;
} yarg_read_ahead_config;

typedef struct yarg_read_ahead_stats {
    uint32_t size;
    uint32_t state;
    int32_t last_error;
    uint32_t target_frames;
    uint32_t queued_frames;
    uint32_t minimum_queued_frames;
    uint64_t produced_frames;
    uint64_t consumed_frames;
    uint64_t requested_frames;
    uint64_t underrun_frames;
    uint64_t underrun_events;
    uint64_t maximum_render_nanoseconds;
    uint64_t position_output_frame;
    uint32_t callback_frames;
    uint32_t callback_elapsed_frames;
    int64_t callback_correction_frames;
    int64_t callback_clock_offset_frames;
} yarg_read_ahead_stats;

typedef struct yarg_read_ahead_position_snapshot {
    uint32_t size;
    uint32_t total_delay_frames;
    int64_t heard_position;
    int64_t decode_position;
} yarg_read_ahead_position_snapshot;

YARG_AUDIO_API uint32_t YARG_AUDIO_CALL yarg_audio_get_abi_version(void);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_gain_dsp_attach(
    uint32_t channel, float initial_gain, int32_t priority,
    yarg_gain_dsp** dsp, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_gain_dsp_set_gain(
    yarg_gain_dsp* dsp, float gain);
YARG_AUDIO_API void YARG_AUDIO_CALL yarg_gain_dsp_destroy(yarg_gain_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_attach(
    uint32_t channel, float dry_mix, float wet_mix, float room_size,
    float damp, float width, int32_t priority,
    yarg_freeverb_dsp** dsp, int32_t* bass_error);
typedef struct yarg_freeverb_params {
    uint32_t size;
    float dry_mix;
    float wet_mix;
    float room_size;
    float damp;
    float width;
} yarg_freeverb_params;

typedef struct yarg_dattorro_reverb_params {
    uint32_t size;
    float dry_mix;
    float wet_mix;
    float room_size;
    float damp;
    float width;
} yarg_dattorro_reverb_params;

typedef struct yarg_noise_gate_params {
    uint32_t size;
    float threshold;
    float floor_gain;
    float attack_ms;
    float hold_ms;
    float release_ms;
} yarg_noise_gate_params;

YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_reset(
    yarg_freeverb_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_set_params(
    yarg_freeverb_dsp* dsp, const yarg_freeverb_params* params);
YARG_AUDIO_API void YARG_AUDIO_CALL yarg_freeverb_dsp_destroy(yarg_freeverb_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_attach(
    uint32_t channel, float dry_mix, float wet_mix, float room_size,
    float damp, float width, int32_t priority,
    yarg_dattorro_reverb_dsp** dsp, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_reset(
    yarg_dattorro_reverb_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_set_params(
    yarg_dattorro_reverb_dsp* dsp, const yarg_dattorro_reverb_params* params);
YARG_AUDIO_API void YARG_AUDIO_CALL yarg_dattorro_reverb_dsp_destroy(yarg_dattorro_reverb_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_attach(
    uint32_t channel, float threshold, float floor_gain, float attack_ms,
    float hold_ms, float release_ms, int32_t priority,
    yarg_noise_gate_dsp** dsp, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_reset(
    yarg_noise_gate_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_noise_gate_dsp_set_params(
    yarg_noise_gate_dsp* dsp, const yarg_noise_gate_params* params);
YARG_AUDIO_API void YARG_AUDIO_CALL yarg_noise_gate_dsp_destroy(yarg_noise_gate_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_create(
    const yarg_one_shot_config* config,
    const float* pcm, uint64_t pcm_sample_count,
    const double* schedule, uint64_t schedule_count,
    yarg_one_shot_stream** stream, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_attach(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchor_song_position, float playback_speed, int32_t paused,
    int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_resync_ex(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchor_song_position, float playback_speed,
    int32_t clear_active_voices, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_set_paused(
    yarg_one_shot_stream* stream, uint32_t mixer, int32_t paused,
    int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_set_gain(
    yarg_one_shot_stream* stream, float gain);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_detach(
    yarg_one_shot_stream* stream, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_destroy(
    yarg_one_shot_stream* stream, int32_t* bass_error);

YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_create(
    const yarg_sine_synth_config* config, yarg_sine_synth_dsp** dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_attach(
    yarg_sine_synth_dsp* dsp, uint32_t channel, int32_t priority, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_detach(
    yarg_sine_synth_dsp* dsp, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_schedule(
    yarg_sine_synth_dsp* dsp, const yarg_tone_segment* notes, uint64_t segment_count,
    int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_timing(
    yarg_sine_synth_dsp* dsp, double song_time_offset, float playback_speed);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_set_output_channel(
    yarg_sine_synth_dsp* dsp, uint32_t output_channel);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_sine_synth_dsp_destroy(
    yarg_sine_synth_dsp* dsp);

YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_create(
    const yarg_read_ahead_config* config, yarg_read_ahead_stream** stream,
    uint32_t* stream_handle, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_set_callback_clock(
    yarg_read_ahead_stream* stream, int32_t enabled);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_prefill(
    yarg_read_ahead_stream* stream, uint32_t timeout_milliseconds);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_flush(
    yarg_read_ahead_stream* stream);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_set_buffer_length(
    yarg_read_ahead_stream* stream, uint32_t buffer_milliseconds);
YARG_AUDIO_API int64_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_source_position(
    yarg_read_ahead_stream* stream, uint32_t source_handle,
    uint32_t endpoint_delay_frames, int32_t* error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_position_snapshot(
    yarg_read_ahead_stream* stream, uint32_t source_handle,
    uint32_t endpoint_delay_frames, yarg_read_ahead_position_snapshot* snapshot);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_get_stats(
    yarg_read_ahead_stream* stream, yarg_read_ahead_stats* stats);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_read_ahead_stream_destroy(
    yarg_read_ahead_stream* stream, int32_t* bass_error);

#ifdef __cplusplus
}
#endif
