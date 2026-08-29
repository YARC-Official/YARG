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

    (void) bind(module_, "BASS_SetDevice", functions_.setDevice);
    (void) bind(module_, "BASS_ChannelGetData", functions_.channelGetData);
    (void) bind(module_, "BASS_ErrorGetCode", functions_.errorGetCode);
    (void) bind(module_, "BASS_ChannelSetDSP", functions_.channelSetDsp);
    (void) bind(module_, "BASS_ChannelRemoveDSP", functions_.channelRemoveDsp);
    (void) bind(module_, "BASS_ChannelLock", functions_.channelLock);
    (void) bind(module_, "BASS_ChannelGetInfo", functions_.channelGetInfo);
    (void) bind(module_, "BASS_GetConfig", functions_.getConfig);
    (void) bind(module_, "BASS_StreamCreate", functions_.streamCreate);
    (void) bind(module_, "BASS_StreamFree", functions_.streamFree);
    (void) bind(module_, "BASS_ChannelGetPosition", functions_.channelGetPosition);
    (void) bind(module_, "BASS_ChannelBytes2Seconds", functions_.channelBytes2Seconds);
    return valid();
}

bool BassCoreBindings::valid() const noexcept {
    return functions_.setDevice && functions_.channelGetData &&
        functions_.errorGetCode && functions_.channelSetDsp &&
        functions_.channelRemoveDsp && functions_.channelLock &&
        functions_.channelGetInfo && functions_.getConfig;
}

bool BassCoreBindings::setDevice(int device) const noexcept {
    return functions_.setDevice &&
        functions_.setDevice(static_cast<std::uint32_t>(device)) != 0;
}

int BassCoreBindings::getData(std::uint32_t channel, void* buffer,
    std::uint32_t bytes) const noexcept {
    if (!functions_.channelGetData) return -1;
    const auto result = functions_.channelGetData(channel, buffer, bytes);
    return result == UINT32_MAX ? -1 : static_cast<int>(result);
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

bool BassCoreBindings::readAheadValid() const noexcept {
    return functions_.setDevice && functions_.channelGetData &&
        functions_.errorGetCode && functions_.streamCreate &&
        functions_.streamFree;
}

bool BassCoreBindings::positionValid() const noexcept {
    return functions_.channelGetPosition && functions_.channelBytes2Seconds;
}

std::int64_t BassCoreBindings::getPosition(std::uint32_t channel,
    std::uint32_t mode) const noexcept {
    if (!functions_.channelGetPosition) return -1;
    const std::uint64_t position = functions_.channelGetPosition(channel, mode);
    return position == UINT64_MAX ? -1 : static_cast<std::int64_t>(position);
}

double BassCoreBindings::bytesToSeconds(std::uint32_t channel,
    std::int64_t bytes) const noexcept {
    if (!functions_.channelBytes2Seconds || bytes < 0) return -1;
    return functions_.channelBytes2Seconds(channel, static_cast<std::uint64_t>(bytes));
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
