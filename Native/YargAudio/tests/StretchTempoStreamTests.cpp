#include "stretch/StretchTempoStream.h"
#include "Test.h"

#include <cmath>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

using namespace yarg::audio;

namespace {

struct MockBass {
    bool lockSucceeds = true;
    bool freeSucceeds = true;
    std::uint32_t sampleRate = 44100;
    BassStreamProc callback = nullptr;
    void* callbackUser = nullptr;
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
    info->frequency = mock->sampleRate;
    info->channels = 1;
    info->flags = 0x100 | 0x200000;
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

void testLatencyBoundsSpeedChangeResponse() {
    for (std::uint32_t sampleRate : {8000u, 44100u, 48000u, 96000u, 192000u}) {
        for (float speed : {0.5f, 1.0f, 2.0f}) {
            for (float adjustment : {-0.25f, 0.25f}) {
                for (int offset : {0, 1, 127, 255}) {
                    MockBass state;
                    state.sampleRate = sampleRate;
                    auto core = makeCore(state);
                    int bassError = -1;
                    auto stream = StretchTempoStream::create(core, 7, &bassError);
                    REQUIRE(stream);

                    for (int pass = 0; pass < 2; ++pass) {
                        stream->setSpeed(speed, 1.0f);
                        const int requestFrame = 20 * StretchTempoStream::BLOCK_FRAMES + offset;
                        std::vector<float> warmup(requestFrame);
                        const auto bytes = static_cast<std::uint32_t>(warmup.size() * sizeof(float));
                        REQUIRE(state.callback(19, warmup.data(), bytes, state.callbackUser) == bytes);

                        stream->setSpeed(speed + adjustment, 1.0f);
                        int responseFrames = -1;
                        for (int frame = 0; frame <= stream->latencyFrames(); ++frame) {
                            float output;
                            REQUIRE(state.callback(19, &output, sizeof(output), state.callbackUser) == sizeof(output));
                            double startSeconds = 0;
                            double endSeconds = 0;
                            REQUIRE(stream->position(requestFrame + frame, startSeconds));
                            REQUIRE(stream->position(requestFrame + frame + 1, endSeconds));
                            const double measuredSpeed = (endSeconds - startSeconds) * sampleRate;
                            if (std::abs(measuredSpeed - speed) > 0.01) {
                                REQUIRE(std::abs(measuredSpeed - speed - adjustment) < 0.01);
                                responseFrames = frame;
                                break;
                            }
                        }

                        REQUIRE(responseFrames >= 0);
                        REQUIRE(responseFrames <= stream->latencyFrames());
                        REQUIRE(stream->latencyFrames() - responseFrames <= StretchTempoStream::BLOCK_FRAMES);
                        REQUIRE(stream->flush());
                    }
                }
            }
        }
    }
}

} // namespace

void runStretchTempoStreamTests() {
    testDestroyLocksBeforeFree();
    testDestroyReleasesLockWhenFreeFails();
    testDestroyFailsWhenLockFails();
    testLatencyBoundsSpeedChangeResponse();
}
