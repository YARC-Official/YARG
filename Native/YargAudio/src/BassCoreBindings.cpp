#include "BassCoreBindings.h"

#include <climits>

namespace yarg::audio {
namespace {

#if defined(_WIN32)
constexpr const char* BassModule = "bass.dll";
#elif defined(__APPLE__)
constexpr const char* BassModule = "libbass.dylib";
#else
constexpr const char* BassModule = "libbass.so";
#endif

template <typename T>
bool bind(const PlatformDynamicLibrary& module, const char* name, T& target) noexcept {
    target = reinterpret_cast<T>(module.symbol(name));
    return target != nullptr;
}

} // namespace

bool BassCoreBindings::load() noexcept {
    // Channel handles belong to loaded BASS instance. Never load a second core module.
    module_ = PlatformDynamicLibrary::findLoaded(BassModule);
    if (!module_) return false;

    (void) bind(module_, "BASS_ErrorGetCode", functions_.errorGetCode);
    (void) bind(module_, "BASS_ChannelSetDSP", functions_.channelSetDsp);
    (void) bind(module_, "BASS_ChannelRemoveDSP", functions_.channelRemoveDsp);
    (void) bind(module_, "BASS_ChannelLock", functions_.channelLock);
    (void) bind(module_, "BASS_ChannelGetInfo", functions_.channelGetInfo);
    (void) bind(module_, "BASS_GetConfig", functions_.getConfig);
    (void) bind(module_, "BASS_StreamCreate", functions_.streamCreate);
    (void) bind(module_, "BASS_StreamFree", functions_.streamFree);
    return valid();
}

bool BassCoreBindings::valid() const noexcept {
    return functions_.errorGetCode && functions_.channelSetDsp &&
        functions_.channelRemoveDsp && functions_.channelLock &&
        functions_.channelGetInfo && functions_.getConfig;
}

int BassCoreBindings::error() const noexcept {
    return functions_.errorGetCode ? functions_.errorGetCode() : -1;
}

std::uint32_t BassCoreBindings::setDsp(std::uint32_t channel, BassDspProc proc,
    void* user, int priority) const noexcept {
    return functions_.channelSetDsp
        ? functions_.channelSetDsp(channel, proc, user, priority) : 0;
}

bool BassCoreBindings::removeDsp(std::uint32_t channel,
    std::uint32_t dsp) const noexcept {
    return functions_.channelRemoveDsp &&
        functions_.channelRemoveDsp(channel, dsp) != 0;
}

bool BassCoreBindings::lockChannel(std::uint32_t channel, bool lock) const noexcept {
    return functions_.channelLock && functions_.channelLock(channel, lock ? 1 : 0) != 0;
}

bool BassCoreBindings::getChannelInfo(std::uint32_t channel,
    BassChannelInfo& info) const noexcept {
    return functions_.channelGetInfo &&
        functions_.channelGetInfo(channel, &info) != 0;
}

std::uint32_t BassCoreBindings::getConfig(std::uint32_t option) const noexcept {
    return functions_.getConfig ? functions_.getConfig(option) : 0;
}

bool BassCoreBindings::oneShotValid() const noexcept {
    return functions_.errorGetCode && functions_.channelLock &&
        functions_.channelGetInfo && functions_.streamCreate &&
        functions_.streamFree;
}

std::uint32_t BassCoreBindings::createStream(std::uint32_t frequency,
    std::uint32_t channels, std::uint32_t flags, BassStreamProc proc,
    void* user) const noexcept {
    return functions_.streamCreate
        ? functions_.streamCreate(frequency, channels, flags, proc, user) : 0;
}

bool BassCoreBindings::freeStream(std::uint32_t stream) const noexcept {
    return functions_.streamFree && functions_.streamFree(stream) != 0;
}

} // namespace yarg::audio
