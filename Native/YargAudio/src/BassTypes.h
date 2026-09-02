#pragma once

#include <cstdint>

#if defined(_WIN32)
#include <windows.h>
#define YARG_BASS_CALL WINAPI
#define YARG_BASS_CALLBACK CALLBACK
#else
#define YARG_BASS_CALL
#define YARG_BASS_CALLBACK
#endif

namespace yarg::audio {

struct BassChannelInfo {
    std::uint32_t frequency;
    std::uint32_t channels;
    std::uint32_t flags;
    std::uint32_t type;
    std::uint32_t originalResolution;
    std::uint32_t plugin;
    std::uint32_t sample;
    const char* filename;
};

using BassDspProc = void(YARG_BASS_CALLBACK*)(std::uint32_t dsp,
    std::uint32_t channel, void* buffer, std::uint32_t length, void* user);
using BassStreamProc = std::uint32_t(YARG_BASS_CALLBACK*)(
    std::uint32_t stream, void* buffer, std::uint32_t length, void* user);

static_assert(sizeof(BassChannelInfo) == (sizeof(void*) == 8 ? 40 : 32));

} // namespace yarg::audio
