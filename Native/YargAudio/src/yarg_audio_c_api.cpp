#include "BassCoreBindings.h"
#include "dsp/FreeverbDsp.h"
#include "dsp/GainDsp.h"
#include "yarg_audio.h"

#include <cstdint>

namespace {

yarg::audio::BassCoreBindings& coreBassBindings() noexcept {
    static yarg::audio::BassCoreBindings bindings;
    static const bool loaded = bindings.load();
    (void) loaded;
    return bindings;
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
