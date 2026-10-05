#include "stretch/StretchTempoStream.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <unordered_map>

namespace yarg::audio {
namespace {

std::mutex g_streamRegistryMutex;
std::unordered_map<std::uint32_t, std::weak_ptr<StretchTempoStream>> g_streamRegistry;

constexpr std::uint32_t BASS_SAMPLE_FLOAT = 0x100;
constexpr std::uint32_t BASS_STREAM_DECODE = 0x200000;
constexpr std::uint32_t BASS_STREAMPROC_END = 0x80000000;
constexpr int BASS_ERROR_ENDED = 45;
// Seconds of position history retained.
constexpr int HISTORY_SECONDS = 12;
// STFT window length in seconds.
constexpr double WINDOW_SECONDS = 0.060;
// Window divided into this many processing intervals.
constexpr int INTERVAL_DIVISOR = 8;
// Transient-protection crossover frequency.
constexpr float TRANSIENT_MIN_HZ = 1500.0f;

}

std::shared_ptr<StretchTempoStream> StretchTempoStream::create(
    BassCoreBindings& bass, std::uint32_t source, int* bassError) noexcept {
    *bassError = 0;
    BassChannelInfo info{};
    if (!bass.oneShotValid() || !bass.getChannelInfo(source, info)) {
        *bassError = bass.error();
        return nullptr;
    }
    if (info.frequency < 8000 || info.frequency > 192000 ||
        info.channels == 0 || info.channels > 8 ||
        (info.flags & (BASS_SAMPLE_FLOAT | BASS_STREAM_DECODE)) !=
            (BASS_SAMPLE_FLOAT | BASS_STREAM_DECODE)) {
        return nullptr;
    }
    try {
        auto result = std::shared_ptr<StretchTempoStream>(
            new StretchTempoStream(bass, source, info));
        result->stream_ = bass.createStream(info.frequency, info.channels,
            BASS_SAMPLE_FLOAT | BASS_STREAM_DECODE, &callback, result.get());
        if (result->stream_ == 0) {
            *bassError = bass.error();
            return nullptr;
        }
        {
            std::lock_guard lock(g_streamRegistryMutex);
            g_streamRegistry[result->stream_] = result;
        }
        return result;
    } catch (...) {
        return nullptr;
    }
}

StretchTempoStream::StretchTempoStream(BassCoreBindings& bass,
    std::uint32_t source, const BassChannelInfo& info)
    : bass_(bass), source_(source), sampleRate_(info.frequency),
      channels_(info.channels) {
    const int windowSteps = static_cast<int>(std::ceil(sampleRate_ * WINDOW_SECONDS /
        (2 * BLOCK_FRAMES)));
    const int window = std::max(4 * BLOCK_FRAMES, windowSteps * 2 * BLOCK_FRAMES);
    stretch_.configure(channels_, sampleRate_, window, window / INTERVAL_DIVISOR);
    const float transientCutoff = std::clamp(TRANSIENT_MIN_HZ / sampleRate_, 0.001f, 0.49f);
    stretch_.setTransientFrequency(transientCutoff);
    const int capacity = std::max(stretch_.inputLatency(),
        static_cast<int>(MAX_SPEED * BLOCK_FRAMES) + 1);
    engineInput_.resize(capacity * channels_);
    fileScratch_.resize(engineInput_.size());
    engineOutput_.resize(BLOCK_FRAMES * channels_);
    for (std::uint32_t channel = 0; channel < channels_; ++channel) {
        inputChannels_.push_back(engineInput_.data() + channel * capacity);
        outputChannels_.push_back(engineOutput_.data() + channel * BLOCK_FRAMES);
    }
    history_.resize((sampleRate_ * HISTORY_SECONDS + stretch_.outputLatency()) /
        BLOCK_FRAMES + 2);
}

StretchTempoStream::~StretchTempoStream() {
    destroy();
}

bool StretchTempoStream::destroy() noexcept {
    if (stream_ == 0) {
        return true;
    }
    {
        std::lock_guard lock(g_streamRegistryMutex);
        g_streamRegistry.erase(stream_);
    }
    if (!bass_.freeStream(stream_)) {
        return false;
    }
    stream_ = 0;
    return true;
}

std::shared_ptr<StretchTempoStream> StretchTempoStream::findByHandle(std::uint32_t handle) noexcept {
    if (handle == 0) {
        return nullptr;
    }
    std::lock_guard lock(g_streamRegistryMutex);
    const auto it = g_streamRegistry.find(handle);
    if (it != g_streamRegistry.end()) {
        return it->second.lock();
    }
    return nullptr;
}

void StretchTempoStream::setSpeed(float speed, float pitch) noexcept {
    speed_.store(speed, std::memory_order_relaxed);
    pitch_.store(pitch, std::memory_order_relaxed);
}

void StretchTempoStream::setGrains(float strength) noexcept {
    grains_.store(strength, std::memory_order_relaxed);
}


void StretchTempoStream::reset() noexcept {
    stretch_.reset(0);
    std::lock_guard lock(historyMutex_);
    inputRemainder_.store(0, std::memory_order_relaxed);
    processedBlocks_.store(0, std::memory_order_relaxed);
    inputFrames_.store(0, std::memory_order_relaxed);
    sourceFrames_.store(0, std::memory_order_relaxed);
    deliveredFrames_.store(0, std::memory_order_relaxed);
    outputOffset_.store(BLOCK_FRAMES, std::memory_order_relaxed);
    primed_.store(false, std::memory_order_relaxed);
    ended_.store(false, std::memory_order_relaxed);
    protectedTransients_.store(0, std::memory_order_relaxed);
    std::fill(history_.begin(), history_.end(), PositionBlock{});
}

bool StretchTempoStream::flush() noexcept {
    if (!bass_.lockChannel(stream_, true)) {
        return false;
    }
    reset();
    return bass_.lockChannel(stream_, false);
}

bool StretchTempoStream::position(double outputFrame, double& seconds) const noexcept {
    return lookup(outputFrame, seconds);
}

bool StretchTempoStream::getPosition(std::int64_t bytes, double& seconds) noexcept {
    return lookup(static_cast<double>(bytes) /
        (channels_ * sizeof(float)), seconds);
}

bool StretchTempoStream::lookup(double outputFrame, double& seconds) const noexcept {
    // Maps an output frame to song time from the latest published history entry.
    const auto processed = processedBlocks_.load(std::memory_order_acquire);
    if (outputFrame == 0 && processed == 0) {
        seconds = 0;
        return true;
    }
    if (outputFrame < 0 || outputFrame >= processed * BLOCK_FRAMES) {
        return false;
    }
    const auto index = static_cast<std::uint64_t>(outputFrame / BLOCK_FRAMES);
    std::lock_guard lock(historyMutex_);
    const auto& block = history_[index % history_.size()];
    if (block.index != index) {
        return false;
    }
    const double fraction = (outputFrame - index * BLOCK_FRAMES) / BLOCK_FRAMES;
    seconds = (block.inputFrame + fraction * block.inputFrames) / sampleRate_;
    return true;
}

bool StretchTempoStream::readInput(int frames) noexcept {
    int readFrames = 0;
    while (readFrames < frames && !ended_.load(std::memory_order_relaxed)) {
        const int bytes = bass_.getData(source_,
            fileScratch_.data() + readFrames * channels_,
            (frames - readFrames) * channels_ * sizeof(float));
        if (bytes < 0) {
            if (bass_.error() != BASS_ERROR_ENDED) {
                return false;
            }
            ended_.store(true, std::memory_order_relaxed);
        } else if (bytes == 0) {
            break;
        } else {
            readFrames += bytes / (channels_ * sizeof(float));
        }
    }
    sourceFrames_.fetch_add(static_cast<std::uint64_t>(readFrames), std::memory_order_relaxed);
    deinterleave(frames, readFrames);
    return true;
}

// Splits interleaved file frames into per-channel buffers; zero-fills short reads.
void StretchTempoStream::deinterleave(int frames, int readFrames) noexcept {
    for (std::uint32_t channel = 0; channel < channels_; ++channel) {
        auto* input = inputChannels_[channel];
        for (int frame = 0; frame < readFrames; ++frame) {
            input[frame] = fileScratch_[frame * channels_ + channel];
        }
        std::fill(input + readFrames, input + frames, 0.0f);
    }
}

bool StretchTempoStream::process() noexcept {
    const auto speed = speed_.load(std::memory_order_relaxed);
    const auto pitch = pitch_.load(std::memory_order_relaxed);
    const int frames = nextInputFrames(speed);
    if (!readInput(frames)) {
        return false;
    }
    const auto blockIndex = processedBlocks_.load(std::memory_order_relaxed);
    const auto baseInput = inputFrames_.load(std::memory_order_relaxed);
    {
        std::lock_guard lock(historyMutex_);
        history_[blockIndex % history_.size()] = PositionBlock{blockIndex,
            static_cast<double>(baseInput), frames};
    }
    stretch_.setTransposeFactor(pitch);
    stretch_.setGrainStrength(grains_.load(std::memory_order_relaxed));
    stretch_.process(inputChannels_, frames, outputChannels_, BLOCK_FRAMES);
    protectedTransients_.store(stretch_.transientCount(), std::memory_order_relaxed);
    inputFrames_.fetch_add(static_cast<std::uint64_t>(frames), std::memory_order_relaxed);
    processedBlocks_.fetch_add(1, std::memory_order_release);
    return true;
}

int StretchTempoStream::nextInputFrames(float speed) noexcept {
    const double exact = speed * static_cast<double>(BLOCK_FRAMES) +
        inputRemainder_.load(std::memory_order_relaxed);
    const int frames = static_cast<int>(exact);
    inputRemainder_.store(exact - frames, std::memory_order_relaxed);
    const int capacity = static_cast<int>(engineInput_.size() / channels_);
    return std::max(0, std::min(frames, capacity));
}

bool StretchTempoStream::prime() noexcept {
    // Feeds the engine its input latency, then discards the warm-up blocks.
    const auto speed = speed_.load(std::memory_order_relaxed);
    const auto inputLatency = stretch_.inputLatency();
    const auto outputLatency = stretch_.outputLatency();
    if (!readInput(inputLatency)) {
        return false;
    }
    stretch_.seek(inputChannels_, inputLatency, speed);
    for (int discarded = 0; discarded < outputLatency;
         discarded += BLOCK_FRAMES) {
        if (!process()) {
            return false;
        }
    }
    return true;
}

std::uint32_t YARG_BASS_CALLBACK StretchTempoStream::callback(
    std::uint32_t, void* buffer, std::uint32_t length, void* user) noexcept {
    auto& stream = *static_cast<StretchTempoStream*>(user);
    const auto frameBytes = stream.channels_ * sizeof(float);
    if (length % frameBytes != 0) {
        std::memset(buffer, 0, length);
        return length;
    }
    return stream.read(static_cast<float*>(buffer), length / frameBytes);
}

std::uint32_t StretchTempoStream::read(float* output, std::uint32_t frames) noexcept {
    // Prime once, then serve each request from freshly processed blocks.
    const auto frameBytes = channels_ * sizeof(float);
    if (!primed_.load(std::memory_order_relaxed)) {
        if (!prime()) {
            return BASS_STREAMPROC_END;
        }
        primed_.store(true, std::memory_order_relaxed);
    }
    std::uint32_t written = 0;
    auto delivered = deliveredFrames_.load(std::memory_order_relaxed);
    auto offset = outputOffset_.load(std::memory_order_relaxed);
    while (written < frames) {
        if (offset == BLOCK_FRAMES) {
            if (!process()) {
                break;
            }
            offset = 0;
        }
        if (ended_.load(std::memory_order_relaxed)) {
            // Playback ends once all consumed source audio has been delivered, not at EOF.
            double positionSeconds = 0;
            if (position(static_cast<double>(delivered), positionSeconds) &&
                positionSeconds * sampleRate_ >=
                    sourceFrames_.load(std::memory_order_relaxed)) {
                break;
            }
        }
        for (std::uint32_t channel = 0; channel < channels_; ++channel) {
            output[written * channels_ + channel] = outputChannels_[channel][offset];
        }
        ++offset;
        ++delivered;
        ++written;
    }
    outputOffset_.store(offset, std::memory_order_relaxed);
    deliveredFrames_.store(delivered, std::memory_order_relaxed);
    if (written < frames) {
        return static_cast<std::uint32_t>(written * frameBytes) | BASS_STREAMPROC_END;
    }
    return written * frameBytes;
}

}
