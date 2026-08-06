#include "BassMixBindings.h"

namespace yarg::audio {
namespace {

#if defined(_WIN32)
constexpr const char* BassMixModule = "bassmix.dll";
#elif defined(__APPLE__)
constexpr const char* BassMixModule = "libbassmix.dylib";
#else
constexpr const char* BassMixModule = "libbassmix.so";
#endif

} // namespace

bool BassMixBindings::load() noexcept {
    module_ = PlatformDynamicLibrary::findLoaded(BassMixModule);
    if (!module_) module_ = PlatformDynamicLibrary::load(BassMixModule);
    if (!module_) return false;
    functions_.mixerStreamAddChannel = reinterpret_cast<decltype(functions_.mixerStreamAddChannel)>(
        module_.symbol("BASS_Mixer_StreamAddChannel"));
    functions_.mixerChannelRemove = reinterpret_cast<decltype(functions_.mixerChannelRemove)>(
        module_.symbol("BASS_Mixer_ChannelRemove"));
    return valid();
}

bool BassMixBindings::addChannel(std::uint32_t mixer, std::uint32_t channel,
    std::uint32_t flags) const noexcept {
    return functions_.mixerStreamAddChannel &&
        functions_.mixerStreamAddChannel(mixer, channel, flags) != 0;
}

bool BassMixBindings::removeChannel(std::uint32_t channel) const noexcept {
    return functions_.mixerChannelRemove &&
        functions_.mixerChannelRemove(channel) != 0;
}

} // namespace yarg::audio
