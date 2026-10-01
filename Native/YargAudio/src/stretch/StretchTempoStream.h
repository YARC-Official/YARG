#pragma once

#include "BassCoreBindings.h"
#include "stretch/YargStretch.h"

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

namespace yarg::audio {

// Adapts YargStretch to BASS: owns the decode stream, feeds the engine fixed
// BLOCK_FRAMES blocks, and records each block's input mapping so getPosition()
// can convert output frames back to song time.
//
// The audio thread produces from the STREAMPROC callback; the game thread queries
// position. Ordering rule: each history entry is fully written under historyMutex_
// before processedBlocks_ is incremented, so readers never observe a torn entry.
class StretchTempoStream final {
public:
    static constexpr int BLOCK_FRAMES = 256;
    static constexpr float MIN_SPEED = 0.05f;
    static constexpr float MAX_SPEED = 51.0f;

    static std::shared_ptr<StretchTempoStream> create(BassCoreBindings& bass,
        std::uint32_t source, int* bassError) noexcept;
    static std::shared_ptr<StretchTempoStream> findByHandle(std::uint32_t handle) noexcept;
    ~StretchTempoStream();

    std::uint32_t streamHandle() const noexcept { return stream_; }
    std::uint32_t sampleRate() const noexcept { return sampleRate_; }
    std::uint32_t channels() const noexcept { return channels_; }
    int latencyFrames() const noexcept { return stretch_.outputLatency() + BLOCK_FRAMES; }
    int protectedTransients() const noexcept {
        return protectedTransients_.load(std::memory_order_relaxed);
    }
    void setSpeed(float speed, float pitch) noexcept;
    bool position(double outputFrame, double& seconds) const noexcept;
    bool getPosition(std::int64_t bytes, double& seconds) noexcept;
    bool flush() noexcept;
    bool destroy() noexcept;

private:
    struct PositionBlock {
        std::uint64_t index = UINT64_MAX;
        double inputFrame = 0;
        int inputFrames = 0;
    };

    StretchTempoStream(BassCoreBindings& bass, std::uint32_t source,
        const BassChannelInfo& info);
    void reset() noexcept;
    bool lookup(double outputFrame, double& seconds) const noexcept;
    static std::uint32_t YARG_BASS_CALLBACK callback(std::uint32_t stream,
        void* buffer, std::uint32_t length, void* user) noexcept;
    std::uint32_t read(float* output, std::uint32_t frames) noexcept;
    bool readInput(int frames) noexcept;
    void deinterleave(int frames, int readFrames) noexcept;
    bool process() noexcept;
    int nextInputFrames(float speed) noexcept;
    bool prime() noexcept;

    BassCoreBindings& bass_;
    const std::uint32_t source_;
    const std::uint32_t sampleRate_;
    const std::uint32_t channels_;
    std::uint32_t stream_ = 0;
    // Engine and per-channel views into engineInput_/engineOutput_.
    YargStretch<float> stretch_{0};
    std::vector<float> fileScratch_;
    std::vector<float> engineInput_;
    std::vector<float*> inputChannels_;
    std::vector<float> engineOutput_;
    std::vector<float*> outputChannels_;
    // Position history: one entry per delivered block.
    std::vector<PositionBlock> history_;
    std::atomic<float> speed_{1.0f};
    std::atomic<float> pitch_{1.0f};
    // Written by the audio thread, read by the game thread via getPosition().
    // Scalars are atomic so queries never block audio. History entries are published
    // under historyMutex_ (fields stored before the entry becomes visible through
    // processedBlocks_).
    mutable std::mutex historyMutex_;
    std::atomic<double> inputRemainder_{0};
    std::atomic<std::uint64_t> processedBlocks_{0};
    std::atomic<std::uint64_t> inputFrames_{0};
    std::atomic<std::uint64_t> sourceFrames_{0};
    std::atomic<std::uint64_t> deliveredFrames_{0};
    std::atomic<int> outputOffset_{BLOCK_FRAMES};
    std::atomic<bool> primed_{false};
    std::atomic<bool> ended_{false};
    std::atomic<int> protectedTransients_{0};
};

}

struct yarg_stretch_stream {
    std::shared_ptr<yarg::audio::StretchTempoStream> value;
};
