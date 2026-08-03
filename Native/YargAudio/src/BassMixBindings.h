#pragma once

#include "BassTypes.h"
#include "PlatformDynamicLibrary.h"

#include <cstdint>

namespace yarg::audio {

struct BassMixFunctions {
    int (YARG_BASS_CALL* mixerStreamAddChannel)(
        std::uint32_t, std::uint32_t, std::uint32_t) = nullptr;
    int (YARG_BASS_CALL* mixerChannelRemove)(std::uint32_t) = nullptr;
};

class BassMixBindings {
public:
    BassMixBindings() = default;
    explicit BassMixBindings(const BassMixFunctions& functions) noexcept
        : functions_(functions) {}

    bool load() noexcept;
    bool valid() const noexcept { return oneShotValid(); }
    bool oneShotValid() const noexcept {
        return functions_.mixerStreamAddChannel && functions_.mixerChannelRemove;
    }
    bool addChannel(std::uint32_t mixer, std::uint32_t channel,
        std::uint32_t flags) const noexcept;
    bool removeChannel(std::uint32_t channel) const noexcept;

private:
    PlatformDynamicLibrary module_;
    BassMixFunctions functions_{};
};

} // namespace yarg::audio
