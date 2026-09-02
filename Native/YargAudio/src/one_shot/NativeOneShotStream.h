#pragma once

#include "BassCoreBindings.h"
#include "BassMixBindings.h"
#include "one_shot/ScheduledSampleSource.h"
#include "yarg_audio.h"

#include <cstddef>
#include <cstdint>
#include <memory>

namespace yarg::audio {

class NativeOneShotStream final {
public:
    static std::unique_ptr<NativeOneShotStream> create(
        BassCoreBindings& core, BassMixBindings& mix,
        std::uint32_t sampleRate, std::uint32_t channels,
        const float* pcm, std::size_t pcmSampleCount,
        const double* schedule, std::size_t scheduleCount,
        double leadTime, int* bassError) noexcept;

    NativeOneShotStream(const NativeOneShotStream&) = delete;
    NativeOneShotStream& operator=(const NativeOneShotStream&) = delete;
    ~NativeOneShotStream() = default;

    int attach(std::uint32_t mixer, double anchorSongPosition,
        float playbackSpeed, bool paused, int* bassError) noexcept;
    int resync(std::uint32_t mixer, double anchorSongPosition,
        float playbackSpeed, bool clearActiveVoices, int* bassError) noexcept;
    int setPaused(std::uint32_t mixer, bool paused, int* bassError) noexcept;
    int setGain(float gain) noexcept;
    int detach(int* bassError) noexcept;

    // Returns false without freeing state when BASS cannot prove callback removal.
    bool destroy(int* bassError) noexcept;

private:
    NativeOneShotStream(BassCoreBindings& core, BassMixBindings& mix,
        std::unique_ptr<ScheduledSampleSource>&& source) noexcept;

    static std::uint32_t YARG_BASS_CALLBACK streamProc(
        std::uint32_t stream, void* buffer, std::uint32_t length,
        void* user) noexcept;
    bool validMixer(std::uint32_t mixer, int* bassError) const noexcept;

    BassCoreBindings& core_;
    BassMixBindings& mix_;
    std::unique_ptr<ScheduledSampleSource> source_;
    std::uint32_t stream_ = 0;
    std::uint32_t mixer_ = 0;
    bool paused_ = false;
};

} // namespace yarg::audio
