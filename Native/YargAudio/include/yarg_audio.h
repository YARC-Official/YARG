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

#define YARG_AUDIO_MAX_INPUT_SOURCES 32u
#define YARG_AUDIO_NODE_NAME_MAX 256u
#define YARG_AUDIO_DESCRIPTION_MAX 256u
#define YARG_AUDIO_ALSA_PATH_MAX 128u

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

/**
 * One PipeWire Audio/Source node backed by ALSA hardware.
 *
 * capture_channel >= 0 marks a channel-split source (e.g. a USB interface's
 * "Input 1"/"Input 2"): BASS records the parent hw PCM with capture_channels
 * channels and the caller extracts capture_channel. -1 means the source is not
 * split and records mono as usual.
 */
typedef struct yarg_input_source {
    uint32_t size;                /**< sizeof(yarg_input_source) */
    int32_t alsa_card;            /**< ALSA card index; -1 when unknown */
    int32_t alsa_device;          /**< ALSA device index; -1 when unknown */
    int32_t alsa_subdevice;       /**< ALSA subdevice index; -1 when unknown */
    int32_t capture_channel;      /**< channel to extract; -1 when not split */
    int32_t capture_channels;     /**< hw channels to record (2 for split) */
    char node_name[YARG_AUDIO_NODE_NAME_MAX];
    char description[YARG_AUDIO_DESCRIPTION_MAX];
    char alsa_path[YARG_AUDIO_ALSA_PATH_MAX];
} yarg_input_source;

/** Flat snapshot of PipeWire input sources. Caller supplies the buffer. */
typedef struct yarg_input_snapshot {
    uint32_t size;                /**< sizeof(yarg_input_snapshot) */
    uint32_t source_count;        /**< entries filled in sources[] */
    yarg_input_source sources[YARG_AUDIO_MAX_INPUT_SOURCES];
} yarg_input_snapshot;

YARG_AUDIO_API uint32_t YARG_AUDIO_CALL yarg_audio_get_abi_version(void);

/**
 * Snapshots PipeWire Audio/Source nodes (Linux only).
 *
 * Returns YARG_AUDIO_OK (possibly with fewer sources than present on timeout),
 * YARG_AUDIO_ERROR_DEPENDENCY when PipeWire is not available, or
 * YARG_AUDIO_ERROR_INVALID_ARGUMENT / YARG_AUDIO_ERROR_INTERNAL on failure.
 * snapshot->size must be >= sizeof(yarg_input_snapshot); the buffer may be
 * larger for forward compatibility, but never more than
 * YARG_AUDIO_MAX_INPUT_SOURCES entries are written.
 */
YARG_AUDIO_API int32_t YARG_AUDIO_CALL yarg_audio_list_input_sources(yarg_input_snapshot* snapshot);
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
