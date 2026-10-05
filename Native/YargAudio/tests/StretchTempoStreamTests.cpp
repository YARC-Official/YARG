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

    for (int sampleRate : {44100, 48000}) {
        for (int channels : {1, 2, 8}) {
            Source unity;
            unity.sampleRate = sampleRate;
            unity.channels = channels;
            unity.length = sampleRate * 3;
            unity.pcm.resize(unity.length * channels);
            for (std::uint64_t frame = 0; frame < unity.length; ++frame) {
                for (int channel = 0; channel < channels; ++channel) {
                    const double frequency = channel % 2 == 0 ? 180 : 4000;
                    const int onsetFrame = frame % (sampleRate / 2);
                    const double attack = onsetFrame < 882 ? 0.7 * std::exp(-double(onsetFrame) / 110) : 0;
                    unity.pcm[frame * channels + channel] = static_cast<float>(
                        attack * std::cos(2 * PI * frequency * onsetFrame / sampleRate) +
                        0.02 * std::sin(2 * PI * (220 + channel * 73) * frame / sampleRate));
                }
            }
            auto unityBass = makeBass(unity);
            auto unityStream = StretchTempoStream::create(unityBass, 11, &errorCode);
            REQUIRE(unityStream != nullptr);
            for (float grains : {0.0f, 1.0f}) {
                unity.frame = 0;
                unityStream->setGrains(grains);
                REQUIRE(unityStream->flush());
                const auto output = render(unity, 257);
                REQUIRE(output.size() >= static_cast<std::size_t>(sampleRate * 2 * channels));
                for (int frame = sampleRate / 4; frame < sampleRate * 2; ++frame) {
                    for (int channel = 0; channel < channels; ++channel) {
                        const auto index = frame * channels + channel;
                        REQUIRE(std::abs(output[index] - unity.pcm[index]) < 1e-5f);
                    }
                }
            }
        }
    }

    Source transitions;
    transitions.length = SAMPLE_RATE * 10;
    transitions.pcm.resize(transitions.length * CHANNELS);
    for (std::uint64_t frame = 0; frame < transitions.length; ++frame) {
        const float sample = static_cast<float>(0.2 * std::sin(2 * PI * 440 * frame / SAMPLE_RATE));
        transitions.pcm[frame * CHANNELS] = sample;
        transitions.pcm[frame * CHANNELS + 1] = -sample;
    }
    auto transitionBass = makeBass(transitions);
    auto transitionStream = StretchTempoStream::create(transitionBass, 11, &errorCode);
    REQUIRE(transitionStream != nullptr);
    std::vector<float> block(256 * CHANNELS);
    float previous = 0;
    double minimumRms = 1;
    double maximumRms = 0;
    float largestStep = 0;
    for (float speed : {1.0f, 0.999f, 1.0f, 1.001f, 1.0f, 0.97f, 1.0f, 1.03f,
        1.0f, 0.5f, 1.0f, 2.0f, 1.0f}) {
        transitionStream->setSpeed(speed, 1);
        for (int chunk = 0; chunk < 100; ++chunk) {
            const auto bytes = transitions.callback(19, block.data(), block.size() * sizeof(float), transitions.user);
            REQUIRE(bytes == block.size() * sizeof(float));
            double energy = 0;
            for (int frame = 0; frame < 256; ++frame) {
                const float sample = block[frame * CHANNELS];
                REQUIRE(std::isfinite(sample));
                REQUIRE(std::abs(sample + block[frame * CHANNELS + 1]) < 1e-5f);
                largestStep = std::max(largestStep, std::abs(sample - previous));
                previous = sample;
                energy += double(sample) * sample;
            }
            const double rms = std::sqrt(energy / 256);
            minimumRms = std::min(minimumRms, rms);
            maximumRms = std::max(maximumRms, rms);
        }
    }
    std::cout << "Stretch unity transitions: minRms=" << minimumRms
        << " maxRms=" << maximumRms << " largestStep=" << largestStep << '\n';
    REQUIRE(minimumRms > 0.08);
    REQUIRE(maximumRms < 0.2);
    REQUIRE(largestStep < 0.06f);
}
