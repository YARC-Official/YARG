#include "one_shot/NativeOneShotStream.h"
#include "Test.h"

#include <cstdint>
#include <string>
#include <vector>

using namespace yarg::audio;

namespace {

struct MockBass {
    bool lockSucceeds = true;
    bool addSucceeds = true;
    bool removeSucceeds = true;
    bool freeSucceeds = true;
    BassStreamProc callback = nullptr;
    void* callbackUser = nullptr;
    float callbackFirstSample = 0;
    std::vector<std::string> events;
};

MockBass* mock = nullptr;

int YARG_BASS_CALL setDevice(std::uint32_t) { return 1; }
std::uint32_t YARG_BASS_CALL getData(std::uint32_t, void*, std::uint32_t bytes) {
    return bytes;
}
int YARG_BASS_CALL error() { return 73; }
std::uint32_t YARG_BASS_CALL setDsp(std::uint32_t, BassDspProc, void*, int) { return 1; }
int YARG_BASS_CALL removeDsp(std::uint32_t, std::uint32_t) { return 1; }
int YARG_BASS_CALL lockChannel(std::uint32_t, int lock) {
    mock->events.emplace_back(lock ? "lock" : "unlock");
    return !lock || mock->lockSucceeds;
}
int YARG_BASS_CALL getInfo(std::uint32_t, BassChannelInfo* info) {
    mock->events.emplace_back("info");
    info->frequency = 1;
    info->channels = 1;
    info->flags = 0x100;
    return 1;
}
std::uint32_t YARG_BASS_CALL getConfig(std::uint32_t) { return 0; }
std::uint32_t YARG_BASS_CALL streamCreate(std::uint32_t, std::uint32_t,
    std::uint32_t, BassStreamProc callback, void* user) {
    mock->events.emplace_back("create");
    mock->callback = callback;
    mock->callbackUser = user;
    return 19;
}
int YARG_BASS_CALL streamFree(std::uint32_t) {
    mock->events.emplace_back("free");
    return mock->freeSucceeds;
}

int YARG_BASS_CALL addChannel(std::uint32_t, std::uint32_t, std::uint32_t) {
    mock->events.emplace_back("add");
    if (!mock->addSucceeds) return 0;
    float output = 0;
    mock->callback(19, &output, sizeof(output), mock->callbackUser);
    mock->callbackFirstSample = output;
    return 1;
}
int YARG_BASS_CALL removeChannel(std::uint32_t) {
    mock->events.emplace_back("remove");
    return mock->removeSucceeds;
}

BassCoreBindings makeCore(MockBass& state) {
    mock = &state;
    return BassCoreBindings(BassCoreFunctions{
        &setDevice, &getData, &error, &setDsp, &removeDsp, &lockChannel,
        &getInfo, &getConfig, &streamCreate, &streamFree});
}

BassMixBindings makeMix() {
    return BassMixBindings(BassMixFunctions{nullptr, &addChannel, &removeChannel});
}

std::unique_ptr<NativeOneShotStream> createStream(
    BassCoreBindings& core, BassMixBindings& mix) {
    const float pcm[] = {1};
    const double schedule[] = {0};
    int bassError = -1;
    auto stream = NativeOneShotStream::create(core, mix, 1, 1, pcm, 1,
        schedule, 1, 0, &bassError);
    REQUIRE(bassError == 0);
    REQUIRE(stream);
    return stream;
}

void testAttachPublishesBeforeAddAndDestroysInOrder() {
    MockBass state;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = createStream(core, mix);
    REQUIRE(state.events == std::vector<std::string>{"create"});

    REQUIRE(stream->attach(7, 0, 1, false, nullptr) == YARG_AUDIO_OK);
    REQUIRE(state.events == std::vector<std::string>({
        "create", "lock", "info", "add", "unlock"}));
    REQUIRE(state.callbackFirstSample == 1);

    state.events.clear();
    REQUIRE(stream->detach(nullptr) == YARG_AUDIO_OK);
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));

    state.events.clear();
    REQUIRE(stream->destroy(nullptr));
    REQUIRE(state.events == std::vector<std::string>{"free"});
}

void testFailuresRetainState() {
    MockBass state;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = createStream(core, mix);

    state.lockSucceeds = false;
    REQUIRE(stream->attach(7, 0, 1, false, nullptr) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(state.events == std::vector<std::string>({"create", "lock"}));
    state.lockSucceeds = true;
    state.events.clear();
    REQUIRE(stream->destroy(nullptr));
    REQUIRE(state.events == std::vector<std::string>{"free"});

    state = MockBass{};
    auto core2 = makeCore(state);
    auto mix2 = makeMix();
    auto attached = createStream(core2, mix2);
    REQUIRE(attached->attach(7, 0, 1, false, nullptr) == YARG_AUDIO_OK);
    state.events.clear();
    state.removeSucceeds = false;
    REQUIRE(attached->detach(nullptr) == YARG_AUDIO_ERROR_BASS);
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock"}));
    REQUIRE(!attached->destroy(nullptr));
    REQUIRE(state.events == std::vector<std::string>({
        "lock", "remove", "unlock", "lock", "remove", "unlock"}));

    state.removeSucceeds = true;
    state.events.clear();
    REQUIRE(attached->destroy(nullptr));
    REQUIRE(state.events == std::vector<std::string>({"lock", "remove", "unlock", "free"}));
}

void testReattachAfterDetach() {
    MockBass state;
    auto core = makeCore(state);
    auto mix = makeMix();
    auto stream = createStream(core, mix);
    REQUIRE(stream->attach(7, 0, 1, false, nullptr) == YARG_AUDIO_OK);
    REQUIRE(stream->detach(nullptr) == YARG_AUDIO_OK);
    REQUIRE(stream->attach(8, 0, 1, true, nullptr) == YARG_AUDIO_OK);
    REQUIRE(stream->setPaused(8, false, nullptr) == YARG_AUDIO_OK);
    REQUIRE(stream->destroy(nullptr));
}

void testResyncActiveVoiceBehavior() {
    MockBass state;
    auto core = makeCore(state);
    auto mix = makeMix();
    const float pcm[] = {1, 2, 3};
    const double schedule[] = {0};
    auto stream = NativeOneShotStream::create(core, mix, 1, 1, pcm, 3,
        schedule, 1, 0, nullptr);
    REQUIRE(stream);
    REQUIRE(stream->attach(7, 0, 1, false, nullptr) == YARG_AUDIO_OK);
    REQUIRE(state.callbackFirstSample == 1);

    REQUIRE(stream->resync(7, 0.1, 2, false, nullptr) == YARG_AUDIO_OK);
    float output = 0;
    state.callback(19, &output, sizeof(output), state.callbackUser);
    REQUIRE(output == 2);

    REQUIRE(stream->resync(7, 0.2, 2, true, nullptr) == YARG_AUDIO_OK);
    output = 9;
    state.callback(19, &output, sizeof(output), state.callbackUser);
    REQUIRE(output == 0);
    REQUIRE(stream->destroy(nullptr));
}

} // namespace

void runNativeOneShotStreamTests() {
    testAttachPublishesBeforeAddAndDestroysInOrder();
    testFailuresRetainState();
    testReattachAfterDetach();
    testResyncActiveVoiceBehavior();
}
