#pragma once

#include "PlatformDynamicLibrary.h"

#include <cstdint>
#include <string>
#include <vector>

namespace yarg::audio {

/**
 * One PipeWire Audio/Source node that is backed by ALSA hardware.
 *
 * captureChannel >= 0 identifies a channel-split source (PipeWire materialized
 * one mono source per channel of a multi-channel hw PCM, e.g. a USB interface's
 * "Input 1"/"Input 2"): the caller records the parent hw PCM with
 * captureChannels channels and extracts captureChannel. -1 means the source is
 * not split and records mono as usual.
 */
struct InputSourceInfo {
    int32_t alsaCard = -1;
    int32_t alsaDevice = -1;
    int32_t alsaSubdevice = -1;
    int32_t captureChannel = -1;
    int32_t captureChannels = 1;
    std::string nodeName;
    std::string description;
    std::string alsaPath;
};

/**
 * Snapshots PipeWire input sources without linking libpipewire: the library is
 * dlopen'd on demand, so a machine without PipeWire degrades gracefully.
 *
 * Returns 0 on success, YARG_AUDIO_ERROR_DEPENDENCY when PipeWire is absent
 * (not dlopen-able or no server reachable), YARG_AUDIO_ERROR_INTERNAL on
 * failure. On success `sources` holds the snapshot, possibly partial when the
 * registry round-trip exceeded the timeout.
 */
class PipeWireSourceLister {
public:
    PipeWireSourceLister() = default;
    ~PipeWireSourceLister();
    PipeWireSourceLister(PipeWireSourceLister&&) noexcept;
    PipeWireSourceLister& operator=(PipeWireSourceLister&&) noexcept;

    int list(std::vector<InputSourceInfo>& sources);

    // dlopen binding table; defined in the implementation TU.
    struct Functions;

private:
    // Binds every required symbol; keeps the module handle for the process so
    // later calls skip the dlopen. Failed loads leave the handle null and are
    // retried on the next call.
    bool load() noexcept;
    void unload() noexcept;

    PlatformDynamicLibrary module_;
    Functions* functions_ = nullptr;
};

} // namespace yarg::audio
