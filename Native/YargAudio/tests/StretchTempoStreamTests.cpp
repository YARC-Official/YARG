#include "stretch/StretchTempoStream.h"
#include "Test.h"

#include <cmath>
#include <vector>

namespace {

using namespace yarg::audio;

constexpr int SAMPLE_RATE = 44100;
constexpr int CHANNELS = 2;
constexpr double PI = 3.14159265358979323846;

struct Source {
    BassStreamProc callback = nullptr;
    void* user = nullptr;
    std::uint32_t sampleRate = SAMPLE_RATE;
    std::uint32_t channels = CHANNELS;
    std::uint64_t frame = 0;
    std::uint64_t length = SAMPLE_RATE;
    std::vector<float> pcm;
    bool freed = false;
    int locks = 0;
};

Source* source = nullptr;

std::uint32_t YARG_BASS_CALL getData(std::uint32_t, void* buffer, std::uint32_t bytes) {
    const auto frameBytes = source->channels * sizeof(float);
    if (source->frame >= source->length) {
        return UINT32_MAX;
    }
    const auto frames = std::min<std::uint64_t>(bytes / frameBytes, source->length - source->frame);
    auto* samples = static_cast<float*>(buffer);
    for (std::uint64_t frame = 0; frame < frames; ++frame) {
        const auto inputFrame = source->frame++;
        for (std::uint32_t channel = 0; channel < source->channels; ++channel) {
            samples[frame * source->channels + channel] =
                source->pcm[inputFrame * source->channels + channel];
        }
    }
    return static_cast<std::uint32_t>(frames * frameBytes);
}

int YARG_BASS_CALL error() { return 0; }
int YARG_BASS_CALL lockChannel(std::uint32_t, int lock) {
    source->locks += lock ? 1 : -1;
    return 1;
}
int YARG_BASS_CALL getInfo(std::uint32_t, BassChannelInfo* info) {
    *info = BassChannelInfo{source->sampleRate, source->channels, 0x200100, 0, 0, 0, 0, nullptr};
    return 1;
}
std::uint32_t YARG_BASS_CALL createStream(std::uint32_t,
    std::uint32_t, std::uint32_t, BassStreamProc callback, void* user) {
    source->callback = callback;
    source->user = user;
    return 19;
}
int YARG_BASS_CALL freeStream(std::uint32_t stream) {
    if (stream == 19) {
        source->freed = true;
    }
    return 1;
}

BassCoreBindings makeBass(Source& state) {
    source = &state;
    BassCoreFunctions functions{};
    functions.channelGetData = &getData;
    functions.errorGetCode = &error;
    functions.channelLock = &lockChannel;
    functions.channelGetInfo = &getInfo;
    functions.streamCreate = &createStream;
    functions.streamFree = &freeStream;
    return BassCoreBindings(functions);
}

std::vector<float> render(Source& state, int chunk = 1024) {
    std::vector<float> output;
    std::vector<float> block(chunk * state.channels);
    while (true) {
        const auto result = state.callback(19, block.data(), block.size() * sizeof(float), state.user);
        const auto samples = (result & 0x7fffffff) / sizeof(float);
        output.insert(output.end(), block.begin(), block.begin() + samples);
        if (result & 0x80000000) {
            return output;
        }
        REQUIRE(output.size() < state.sampleRate * 20u * state.channels);
    }
}

} // namespace

void runStretchTempoStreamTests() {
    Source state;
    state.sampleRate = SAMPLE_RATE;
    state.channels = CHANNELS;
    state.length = SAMPLE_RATE; // 1 second
    state.pcm.resize(state.length * state.channels);
    for (std::uint64_t frame = 0; frame < state.length; ++frame) {
        const float sample = static_cast<float>(0.3 * std::sin(2 * PI * 440.0 * frame / state.sampleRate));
        state.pcm[frame * 2] = sample;
        state.pcm[frame * 2 + 1] = -sample;
    }

    auto bass = makeBass(state);
    int errorCode = 0;
    auto stream = StretchTempoStream::create(bass, 11, &errorCode);
    REQUIRE(stream != nullptr);

    // Smoke test various speeds and pitches
    for (float speed : {0.5f, 1.0f, 1.5f}) {
        for (float pitch : {0.75f, 1.0f, 1.25f}) {
            state.frame = 0;
            stream->setSpeed(speed, pitch);
            REQUIRE(stream->flush());
            auto output = render(state, 512);
            REQUIRE(!output.empty());
            for (float s : output) {
                REQUIRE(std::isfinite(s));
            }
            double pos = 0;
            REQUIRE(stream->position(output.size() / CHANNELS / 2, pos));
            REQUIRE(pos >= 0);
        }
    }

    stream.reset();
    REQUIRE(state.freed);
}
