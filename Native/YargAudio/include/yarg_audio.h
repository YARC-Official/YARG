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

#define YARG_AUDIO_ABI_VERSION 1u

typedef struct yarg_gain_dsp yarg_gain_dsp;
typedef struct yarg_freeverb_dsp yarg_freeverb_dsp;
typedef struct yarg_one_shot_stream yarg_one_shot_stream;

typedef enum yarg_audio_result {
    YARG_AUDIO_OK = 0,
    YARG_AUDIO_ERROR_INVALID_ARGUMENT = -1,
    YARG_AUDIO_ERROR_INVALID_STATE = -2,
    YARG_AUDIO_ERROR_UNSUPPORTED = -3,
    YARG_AUDIO_ERROR_DEPENDENCY = -4,
    YARG_AUDIO_ERROR_BASS = -5,
    YARG_AUDIO_ERROR_INTERNAL = -6,
    YARG_AUDIO_ERROR_SOURCE = -7
} yarg_audio_result;

typedef struct yarg_one_shot_config {
    uint32_t size;
    uint32_t sample_rate;
    uint32_t channels;
    uint32_t reserved;
    double lead_time;
} yarg_one_shot_config;

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
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_reset(
    yarg_freeverb_dsp* dsp);
YARG_AUDIO_API void YARG_AUDIO_CALL yarg_freeverb_dsp_destroy(yarg_freeverb_dsp* dsp);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_create(
    const yarg_one_shot_config* config,
    const float* pcm, uint64_t pcm_sample_count,
    const double* schedule, uint64_t schedule_count,
    yarg_one_shot_stream** stream, int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_attach(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchor_song_position, float playback_speed, int32_t paused,
    int32_t* bass_error);
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_one_shot_stream_resync(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchor_song_position, float playback_speed, int32_t* bass_error);
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

#ifdef __cplusplus
}
#endif
