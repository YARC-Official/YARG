#include "stretch/StretchTempoStream.h"
#include "Test.h"

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

using namespace yarg::audio;

namespace {

struct MockBass {
    bool lockSucceeds = true;
    bool freeSucceeds = true;
    std::vector<std::string> events;
};

MockBass* mock = nullptr;

int YARG_BASS_CALL setDevice(std::uint32_t) { return 1; }
std::uint32_t YARG_BASS_CALL getData(std::uint32_t, void* buffer, std::uint32_t bytes) {
    std::memset(buffer, 0, bytes);
    return bytes;
}
int YARG_BASS_CALL error() { return 0; }
std::uint32_t YARG_BASS_CALL setDsp(std::uint32_t, BassDspProc, void*, int) { return 1; }
int YARG_BASS_CALL removeDsp(std::uint32_t, std::uint32_t) { return 1; }
int YARG_BASS_CALL lockChannel(std::uint32_t, int lock) {
    mock->events.emplace_back(lock ? "lock" : "unlock");
    return !lock || mock->lockSucceeds;
}
int YARG_BASS_CALL getInfo(std::uint32_t, BassChannelInfo* info) {
    mock->events.emplace_back("info");
    info->frequency = 44100;
    info->channels = 1;
    info->flags = 0x100 | 0x200000;
    return 1;
}
std::uint32_t YARG_BASS_CALL getConfig(std::uint32_t) { return 0; }
std::uint32_t YARG_BASS_CALL streamCreate(std::uint32_t, std::uint32_t,
    std::uint32_t, BassStreamProc, void*) {
    mock->events.emplace_back("create");
    return 19;
}
int YARG_BASS_CALL streamFree(std::uint32_t) {
    mock->events.emplace_back("free");
    return mock->freeSucceeds;
}

BassCoreBindings makeCore(MockBass& state) {
    mock = &state;
    return BassCoreBindings(BassCoreFunctions{
        &setDevice, &getData, &error, &setDsp, &removeDsp, &lockChannel,
        &getInfo, &getConfig, &streamCreate, &streamFree});
}

void testDestroyLocksBeforeFree() {
    MockBass state;
    auto core = makeCore(state);
    int bassError = -1;
    auto stream = StretchTempoStream::create(core, 7, &bassError);
    REQUIRE(bassError == 0);
    REQUIRE(stream);
    REQUIRE(state.events == std::vector<std::string>({"info", "create"}));

    REQUIRE(stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"info", "create", "lock", "free", "unlock"}));

    REQUIRE(stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"info", "create", "lock", "free", "unlock"}));
}

void testDestroyReleasesLockWhenFreeFails() {
    MockBass state;
    auto core = makeCore(state);
    int bassError = -1;
    auto stream = StretchTempoStream::create(core, 7, &bassError);
    REQUIRE(stream);
    state.events.clear();

    state.freeSucceeds = false;
    REQUIRE(!stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"lock", "free", "unlock"}));

    state.events.clear();
    state.freeSucceeds = true;
    REQUIRE(stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"lock", "free", "unlock"}));
}

void testDestroyFailsWhenLockFails() {
    MockBass state;
    auto core = makeCore(state);
    int bassError = -1;
    auto stream = StretchTempoStream::create(core, 7, &bassError);
    REQUIRE(stream);
    state.events.clear();

    state.lockSucceeds = false;
    REQUIRE(!stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"lock"}));

    state.lockSucceeds = true;
    state.events.clear();
    REQUIRE(stream->destroy());
    REQUIRE(state.events == std::vector<std::string>({"lock", "free", "unlock"}));
}

} // namespace

void runStretchTempoStreamTests() {
    testDestroyLocksBeforeFree();
    testDestroyReleasesLockWhenFreeFails();
    testDestroyFailsWhenLockFails();
}
