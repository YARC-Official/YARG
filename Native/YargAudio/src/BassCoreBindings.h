#pragma once

#include "BassTypes.h"
#include "PlatformDynamicLibrary.h"

#include <cstdint>

namespace yarg::audio {

struct BassCoreFunctions {
    int (YARG_BASS_CALL* setDevice)(std::uint32_t) = nullptr;
    std::uint32_t (YARG_BASS_CALL* channelGetData)(
        std::uint32_t, void*, std::uint32_t) = nullptr;
    int (YARG_BASS_CALL* errorGetCode)() = nullptr;
    std::uint32_t (YARG_BASS_CALL* channelSetDsp)(
        std::uint32_t, BassDspProc, void*, int) = nullptr;
    int (YARG_BASS_CALL* channelRemoveDsp)(std::uint32_t, std::uint32_t) = nullptr;
    int (YARG_BASS_CALL* channelLock)(std::uint32_t, int) = nullptr;
    int (YARG_BASS_CALL* channelGetInfo)(std::uint32_t, BassChannelInfo*) = nullptr;
    std::uint32_t (YARG_BASS_CALL* getConfig)(std::uint32_t) = nullptr;
    std::uint32_t (YARG_BASS_CALL* streamCreate)(
        std::uint32_t, std::uint32_t, std::uint32_t, BassStreamProc, void*) = nullptr;
    int (YARG_BASS_CALL* streamFree)(std::uint32_t) = nullptr;
    std::uint64_t (YARG_BASS_CALL* channelGetPosition)(
        std::uint32_t, std::uint32_t) = nullptr;
    double (YARG_BASS_CALL* channelBytes2Seconds)(
        std::uint32_t, std::uint64_t) = nullptr;
};

class BassCoreBindings {
public:
    BassCoreBindings() = default;
    explicit BassCoreBindings(const BassCoreFunctions& functions) noexcept
        : functions_(functions) {}

    bool load() noexcept;
    bool valid() const noexcept;

    bool setDevice(int device) const noexcept;
    int getData(std::uint32_t channel, void* buffer, std::uint32_t bytes) const noexcept;
    int error() const noexcept;
    std::uint32_t setDsp(std::uint32_t channel, BassDspProc proc,
        void* user, int priority) const noexcept;
    bool removeDsp(std::uint32_t channel, std::uint32_t dsp) const noexcept;
    bool lockChannel(std::uint32_t channel, bool lock) const noexcept;
    bool getChannelInfo(std::uint32_t channel, BassChannelInfo& info) const noexcept;
    std::uint32_t getConfig(std::uint32_t option) const noexcept;
    bool oneShotValid() const noexcept;
    bool readAheadValid() const noexcept;

    // Song position support, required by the sine synth DSP.
    bool positionValid() const noexcept;
    std::int64_t getPosition(std::uint32_t channel, std::uint32_t mode) const noexcept;
    double bytesToSeconds(std::uint32_t channel, std::int64_t bytes) const noexcept;

    std::uint32_t createStream(std::uint32_t frequency, std::uint32_t channels,
        std::uint32_t flags, BassStreamProc proc, void* user) const noexcept;
    bool freeStream(std::uint32_t stream) const noexcept;

private:
    PlatformDynamicLibrary module_;
    BassCoreFunctions functions_{};
};

} // namespace yarg::audio
