#include "BassCoreBindings.h"
#include "BassMixBindings.h"
#include "PipeWireSourceLister.h"
#include "dsp/FreeverbDsp.h"
#include "dsp/GainDsp.h"
#include "one_shot/NativeOneShotStream.h"
#include "yarg_audio.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <memory>
#include <cstdint>

static_assert(sizeof(yarg_one_shot_config) == 24);
static_assert(sizeof(yarg_input_source) ==
    sizeof(uint32_t) + 5 * sizeof(int32_t) +
    YARG_AUDIO_NODE_NAME_MAX + YARG_AUDIO_DESCRIPTION_MAX + YARG_AUDIO_ALSA_PATH_MAX);
static_assert(sizeof(yarg_input_snapshot) ==
    sizeof(uint32_t) * 2 + YARG_AUDIO_MAX_INPUT_SOURCES * sizeof(yarg_input_source));

struct yarg_one_shot_stream {
    std::unique_ptr<yarg::audio::NativeOneShotStream> value;
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

int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_attach(uint32_t channel,
    float dry_mix, float wet_mix, float room_size, float damp, float width,
    int32_t priority, yarg_freeverb_dsp** dsp, int32_t* bass_error) {
    return yarg::audio::freeverbDspAttach(coreBassBindings(), channel, dry_mix,
        wet_mix, room_size, damp, width, priority, dsp, bass_error);
}

int32_t YARG_AUDIO_CALL yarg_freeverb_dsp_reset(yarg_freeverb_dsp* dsp) {
    return yarg::audio::freeverbDspRequestReset(dsp);
}

void YARG_AUDIO_CALL yarg_freeverb_dsp_destroy(yarg_freeverb_dsp* dsp) {
    (void) yarg::audio::freeverbDspDestroy(dsp);
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

int32_t YARG_AUDIO_CALL yarg_one_shot_stream_resync(
    yarg_one_shot_stream* stream, uint32_t mixer,
    double anchorSongPosition, float playbackSpeed, int32_t* bassError) {
    return yarg_one_shot_stream_resync_ex(stream, mixer, anchorSongPosition,
        playbackSpeed, 1, bassError);
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

namespace {

void copySourceString(char* destination, std::size_t capacity,
    const std::string& value) noexcept {
    if (value.empty()) {
        destination[0] = '\0';
        return;
    }
    std::strncpy(destination, value.c_str(), capacity - 1);
    destination[capacity - 1] = '\0';
}

} // namespace

int32_t YARG_AUDIO_CALL yarg_audio_list_input_sources(yarg_input_snapshot* snapshot) {
    if (!snapshot || snapshot->size < sizeof(yarg_input_snapshot)) {
        return YARG_AUDIO_ERROR_INVALID_ARGUMENT;
    }
    snapshot->source_count = 0;

    std::vector<yarg::audio::InputSourceInfo> sources;
    const int result = yarg::audio::PipeWireSourceLister{}.list(sources);
    if (result != 0) {
        return result;
    }

    const std::size_t count = std::min<std::size_t>(
        sources.size(), YARG_AUDIO_MAX_INPUT_SOURCES);
    for (std::size_t i = 0; i < count; ++i) {
        yarg_input_source& out = snapshot->sources[i];
        std::memset(&out, 0, sizeof out);
        out.size = sizeof out;
        out.alsa_card = sources[i].alsaCard;
        out.alsa_device = sources[i].alsaDevice;
        out.alsa_subdevice = sources[i].alsaSubdevice;
        out.capture_channel = sources[i].captureChannel;
        out.capture_channels = sources[i].captureChannels;
        copySourceString(out.node_name, sizeof out.node_name, sources[i].nodeName);
        copySourceString(out.description, sizeof out.description, sources[i].description);
        copySourceString(out.alsa_path, sizeof out.alsa_path, sources[i].alsaPath);
    }
    snapshot->source_count = static_cast<uint32_t>(count);
    return YARG_AUDIO_OK;
}
