#include "RenderAheadMixer.h"
#include "Test.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstddef>
#include <memory>
#include <thread>
#include <vector>

using namespace std::chrono_literals;
using yarg::audio::IAudioSource;
using yarg::audio::RenderAheadMixer;

namespace {

class SequenceSource final : public IAudioSource {
public:
    explicit SequenceSource(std::size_t channels) : channels_(channels) {}
    bool prepareThread() noexcept override { prepared_.store(true); return true; }
    int read(float* samples, std::size_t frames) noexcept override {
        for (std::size_t frame = 0; frame < frames; ++frame) {
            for (std::size_t channel = 0; channel < channels_; ++channel) {
                samples[frame * channels_ + channel] =
                    static_cast<float>(nextFrame_ * 10 + channel);
            }
            ++nextFrame_;
        }
        return static_cast<int>(frames);
    }
    int lastError() const noexcept override { return 0; }
    std::int64_t position(std::uint32_t, std::uint32_t delayBytes) noexcept override {
        const auto currentBytes = nextFrame_ * channels_ * sizeof(float);
        return static_cast<std::int64_t>(currentBytes - std::min(currentBytes,
            static_cast<std::size_t>(delayBytes)));
    }
    bool prepared() const noexcept { return prepared_.load(); }
private:
    std::size_t channels_;
    std::size_t nextFrame_ = 0;
    std::atomic<bool> prepared_{false};
};

class FailingSource final : public IAudioSource {
public:
    bool prepareThread() noexcept override { return true; }
    int read(float*, std::size_t) noexcept override { return -1; }
    int lastError() const noexcept override { return 37; }
    std::int64_t position(std::uint32_t, std::uint32_t) noexcept override { return -1; }
};

class BlockingSource final : public IAudioSource {
public:
    bool prepareThread() noexcept override { return true; }
    int read(float* samples, std::size_t frames) noexcept override {
        reading_.store(true);
        while (blocked_.load()) {
            std::this_thread::yield();
        }
        reading_.store(false);
        std::fill_n(samples, frames, 0.0f);
        produced_ += frames;
        return static_cast<int>(frames);
    }
    int lastError() const noexcept override { return 0; }
    std::int64_t position(std::uint32_t, std::uint32_t delayBytes) noexcept override {
        return static_cast<std::int64_t>(produced_ * sizeof(float) - delayBytes);
    }
    void block() noexcept { blocked_.store(true); }
    void release() noexcept { blocked_.store(false); }
    bool reading() const noexcept { return reading_.load(); }
private:
    std::atomic<bool> blocked_{false};
    std::atomic<bool> reading_{false};
    std::size_t produced_ = 0;
};

} // namespace

void runRenderAheadMixerTests() {
    {
        auto source = std::make_unique<SequenceSource>(2);
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 2, 64, 10);
        REQUIRE(renderer.start());
        REQUIRE(renderer.prefill(2s));
        REQUIRE(sourcePointer->prepared());
        REQUIRE(renderer.queuedFrames() >= renderer.targetFrames());

        std::vector<float> output(192 * 2);
        REQUIRE(renderer.consume(output.data(), 192) == 192);
        REQUIRE(output[0] == 0 && output[1] == 1);
        REQUIRE(output[2] == 10 && output[3] == 11);

        const auto deadline = std::chrono::steady_clock::now() + 2s;
        while (renderer.queuedFrames() < renderer.targetFrames() &&
               std::chrono::steady_clock::now() < deadline) {
            std::this_thread::sleep_for(1ms);
        }
        REQUIRE(renderer.queuedFrames() >= renderer.targetFrames());
        REQUIRE(renderer.bufferedSourcePosition(1, 0) == 192 * 2 * sizeof(float));

        REQUIRE(renderer.clear());
        REQUIRE(renderer.queuedFrames() == 0);
        std::this_thread::sleep_for(5ms);
        REQUIRE(renderer.queuedFrames() == 0);
        REQUIRE(renderer.prefill(2s));
        REQUIRE(renderer.queuedFrames() >= renderer.targetFrames());
        renderer.stop();
    }

    {
        RenderAheadMixer renderer(std::make_unique<SequenceSource>(2), 48000, 2, 64, 0);
        REQUIRE(renderer.prefill(2s));
        REQUIRE(renderer.targetFrames() == 128);

        std::vector<float> output(renderer.targetFrames() * 2);
        REQUIRE(renderer.consume(output.data(), renderer.targetFrames()) ==
            renderer.targetFrames());

        const auto deadline = std::chrono::steady_clock::now() + 2s;
        while (renderer.queuedFrames() < renderer.targetFrames() &&
               std::chrono::steady_clock::now() < deadline) {
            std::this_thread::sleep_for(1ms);
        }
        REQUIRE(renderer.queuedFrames() >= renderer.targetFrames());
        renderer.stop();
    }

    {
        RenderAheadMixer renderer(std::make_unique<FailingSource>(), 48000, 2, 64, 10);
        REQUIRE(renderer.start());
        REQUIRE(!renderer.prefill(2s));
        REQUIRE(renderer.failed());
        REQUIRE(renderer.lastError() == 37);
    }

    {
        auto source = std::make_unique<BlockingSource>();
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 1, 64, 0);
        REQUIRE(renderer.prefill(2s));

        sourcePointer->block();
        std::vector<float> output(renderer.targetFrames());
        const auto consumed = renderer.consume(output.data(), renderer.targetFrames());

        const auto readDeadline = std::chrono::steady_clock::now() + 2s;
        while (!sourcePointer->reading() && std::chrono::steady_clock::now() < readDeadline) {
            std::this_thread::yield();
        }
        const auto startedReading = sourcePointer->reading();
        if (consumed != renderer.targetFrames() || !startedReading) {
            sourcePointer->release();
            renderer.stop();
            REQUIRE(consumed == renderer.targetFrames());
            REQUIRE(startedReading);
        }

        std::atomic<bool> positionStarted{false};
        std::atomic<bool> delayRequested{false};
        std::int64_t position = -1;
        std::thread positionThread([&] {
            positionStarted.store(true);
            position = renderer.bufferedSourcePositionAfterLock(1, [&] {
                delayRequested.store(true);
                return 0u;
            });
        });

        const auto positionDeadline = std::chrono::steady_clock::now() + 2s;
        while (!positionStarted.load() && std::chrono::steady_clock::now() < positionDeadline) {
            std::this_thread::yield();
        }
        std::this_thread::sleep_for(10ms);
        const auto requestedWhileBlocked = delayRequested.load();
        sourcePointer->release();
        positionThread.join();
        renderer.stop();

        REQUIRE(!requestedWhileBlocked);
        REQUIRE(delayRequested.load());
        REQUIRE(position >= 0);
    }
}
