#include "RenderAheadMixer.h"
#include "Test.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstddef>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

using namespace std::chrono_literals;
using yarg::audio::IAudioSource;
using yarg::audio::SourcePositionSnapshot;
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
    std::int64_t position(std::uint32_t) noexcept override {
        return static_cast<std::int64_t>(nextFrame_ * channels_ * sizeof(float));
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
    std::int64_t position(std::uint32_t) noexcept override { return -1; }
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
    std::int64_t position(std::uint32_t) noexcept override {
        return static_cast<std::int64_t>(produced_ * sizeof(float));
    }
    void block() noexcept { blocked_.store(true); }
    void release() noexcept { blocked_.store(false); }
    bool reading() const noexcept { return reading_.load(); }
private:
    std::atomic<bool> blocked_{false};
    std::atomic<bool> reading_{false};
    std::size_t produced_ = 0;
};

class SlowSource final : public IAudioSource {
public:
    bool prepareThread() noexcept override { return true; }
    int read(float* samples, std::size_t frames) noexcept override {
        std::this_thread::sleep_for(500us);
        std::fill_n(samples, frames, 0.0f);
        produced_ += frames;
        return static_cast<int>(frames);
    }
    int lastError() const noexcept override { return 0; }
    std::int64_t position(std::uint32_t) noexcept override {
        return static_cast<std::int64_t>(produced_ * sizeof(float));
    }
private:
    std::size_t produced_ = 0;
};

class AttributeSource final : public IAudioSource {
public:
    struct Write {
        std::uint32_t channel;
        std::uint32_t attribute;
        float value;
        bool duringRead;
    };

    bool prepareThread() noexcept override { return true; }
    int read(float* samples, std::size_t frames) noexcept override {
        reading_.store(true);
        while (blocked_.load()) {
            std::this_thread::yield();
        }
        reading_.store(false);
        std::fill_n(samples, frames, 0.0f);
        return static_cast<int>(frames);
    }
    int lastError() const noexcept override { return 0; }
    std::int64_t position(std::uint32_t) noexcept override { return 0; }
    bool setAttribute(std::uint32_t channel, std::uint32_t attribute,
        float value) noexcept override {
        std::lock_guard lock(writesMutex_);
        writes_.push_back(Write{channel, attribute, value, reading_.load()});
        return true;
    }
    std::vector<Write> writes() {
        std::lock_guard lock(writesMutex_);
        return writes_;
    }
    void block() noexcept { blocked_.store(true); }
    void release() noexcept { blocked_.store(false); }
    bool reading() const noexcept { return reading_.load(); }
private:
    std::atomic<bool> blocked_{false};
    std::atomic<bool> reading_{false};
    std::mutex writesMutex_;
    std::vector<Write> writes_;
};

} // namespace

void runRenderAheadMixerTests() {
    {
        auto source = std::make_unique<SequenceSource>(2);
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 2, 64, 10, 1);
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
        SourcePositionSnapshot filled{};
        REQUIRE(renderer.sourcePositionSnapshot(0, filled));
        REQUIRE(filled.heardPosition == 192 * 2 * sizeof(float));

        REQUIRE(renderer.clear());
        REQUIRE(renderer.queuedFrames() == 0);
        std::this_thread::sleep_for(5ms);
        REQUIRE(renderer.queuedFrames() == 0);
        REQUIRE(renderer.prefill(2s));
        REQUIRE(renderer.queuedFrames() >= renderer.targetFrames());
        renderer.stop();
    }

    {
        RenderAheadMixer renderer(std::make_unique<SequenceSource>(2), 48000, 2, 64, 0, 1);
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
        RenderAheadMixer renderer(std::make_unique<FailingSource>(), 48000, 2, 64, 10, 1);
        REQUIRE(renderer.start());
        REQUIRE(!renderer.prefill(2s));
        REQUIRE(renderer.failed());
        REQUIRE(renderer.lastError() == 37);
    }

    {
        auto source = std::make_unique<BlockingSource>();
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 1, 64, 0, 1);
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

        SourcePositionSnapshot snapshot{};
        const auto readWhileBlocked = renderer.sourcePositionSnapshot(0, snapshot);
        const auto stillReading = sourcePointer->reading();
        sourcePointer->release();
        renderer.stop();

        REQUIRE(readWhileBlocked);
        REQUIRE(stillReading);
        REQUIRE(snapshot.heardPosition ==
            static_cast<std::int64_t>(renderer.targetFrames() * sizeof(float)));
    }

    {
        RenderAheadMixer renderer(std::make_unique<SlowSource>(), 48000, 1, 64, 5000, 1);
        REQUIRE(renderer.start());

        auto slowest = std::chrono::steady_clock::duration::zero();
        for (int read = 0; read < 50; ++read) {
            const auto started = std::chrono::steady_clock::now();
            SourcePositionSnapshot snapshot{};
            const auto succeeded = renderer.sourcePositionSnapshot(0, snapshot);
            slowest = std::max(slowest, std::chrono::steady_clock::now() - started);
            REQUIRE(succeeded && snapshot.heardPosition >= 0);
            std::this_thread::sleep_for(2ms);
        }
        const auto stillFilling = renderer.queuedFrames() < renderer.targetFrames();
        renderer.stop();

        REQUIRE(stillFilling);
        REQUIRE(slowest < 50ms);
    }

    {
        RenderAheadMixer renderer(std::make_unique<SlowSource>(), 48000, 1, 64, 5000, 1);
        REQUIRE(renderer.start());
        std::this_thread::sleep_for(5ms);

        renderer.holdNextRender();
        // A read already in flight may finish after holdNextRender returns. Wait longer than the
        // slowest timer slice (Windows can sleep ~15 ms) before sampling the held frame count.
        std::this_thread::sleep_for(100ms);
        const auto heldFrames = renderer.producedFrames();
        std::this_thread::sleep_for(20ms);
        const auto framesWhileHeld = renderer.producedFrames();
        renderer.releaseNextRender();

        const auto deadline = std::chrono::steady_clock::now() + 2s;
        while (renderer.producedFrames() == framesWhileHeld &&
               std::chrono::steady_clock::now() < deadline) {
            std::this_thread::sleep_for(1ms);
        }
        const auto resumed = renderer.producedFrames() > framesWhileHeld;
        renderer.stop();

        REQUIRE(heldFrames > 0);
        REQUIRE(framesWhileHeld == heldFrames);
        REQUIRE(resumed);
    }

    {
        auto source = std::make_unique<BlockingSource>();
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 1, 64, 10, 1);
        SourcePositionSnapshot snapshot{};
        REQUIRE(renderer.sourcePositionSnapshot(0, snapshot));
        REQUIRE(snapshot.heardPosition == 0 && snapshot.decodePosition == 0);
        REQUIRE(renderer.prefill(2s));
        REQUIRE(renderer.producedFrames() == 512);

        sourcePointer->block();
        std::vector<float> output(200);
        const auto consumed = renderer.consume(output.data(), output.size());

        const auto readDeadline = std::chrono::steady_clock::now() + 2s;
        while (!sourcePointer->reading() && std::chrono::steady_clock::now() < readDeadline) {
            std::this_thread::yield();
        }
        const auto startedReading = sourcePointer->reading();

        SourcePositionSnapshot busySnapshot{};
        const auto busyRead = renderer.sourcePositionSnapshot(0, busySnapshot);
        SourcePositionSnapshot originSnapshot{};
        const auto originRead = renderer.sourcePositionSnapshot(400, originSnapshot);
        sourcePointer->release();
        renderer.stop();

        REQUIRE(consumed == 200);
        REQUIRE(startedReading);
        REQUIRE(busyRead);
        REQUIRE(busySnapshot.heardPosition == 200 * sizeof(float));
        REQUIRE(busySnapshot.decodePosition == 512 * sizeof(float));
        REQUIRE(busySnapshot.totalDelayFrames == 312);
        REQUIRE(originRead);
        REQUIRE(originSnapshot.heardPosition == 0);
        REQUIRE(originSnapshot.totalDelayFrames == 712);
    }

    {
        auto source = std::make_unique<AttributeSource>();
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 1, 64, 10, 1);
        REQUIRE(renderer.prefill(2s));

        sourcePointer->block();
        std::vector<float> output(200);
        renderer.consume(output.data(), output.size());
        const auto readDeadline = std::chrono::steady_clock::now() + 2s;
        while (!sourcePointer->reading() && std::chrono::steady_clock::now() < readDeadline) {
            std::this_thread::yield();
        }
        const auto startedReading = sourcePointer->reading();

        const auto queueStarted = std::chrono::steady_clock::now();
        const auto queuedTempo = renderer.queueSourceAttribute(5, 0x10000, 10.0f);
        const auto replacedTempo = renderer.queueSourceAttribute(5, 0x10000, 20.0f);
        const auto queuedPitch = renderer.queueSourceAttribute(5, 0x10001, 3.0f);
        const auto queueElapsed = std::chrono::steady_clock::now() - queueStarted;
        std::this_thread::sleep_for(5ms);
        const auto writesWhileBlocked = sourcePointer->writes().size();

        sourcePointer->release();
        const auto applyDeadline = std::chrono::steady_clock::now() + 2s;
        while (sourcePointer->writes().size() < 2 && std::chrono::steady_clock::now() < applyDeadline) {
            std::this_thread::sleep_for(1ms);
        }
        renderer.stop();
        const auto writes = sourcePointer->writes();

        REQUIRE(startedReading);
        REQUIRE(queuedTempo && replacedTempo && queuedPitch);
        REQUIRE(queueElapsed < 5ms);
        REQUIRE(writesWhileBlocked == 0);
        REQUIRE(writes.size() == 2);
        REQUIRE(writes[0].channel == 5 && writes[0].attribute == 0x10000 && writes[0].value == 20.0f);
        REQUIRE(writes[1].attribute == 0x10001 && writes[1].value == 3.0f);
        REQUIRE(!writes[0].duringRead && !writes[1].duringRead);
    }

    {
        auto source = std::make_unique<AttributeSource>();
        auto* sourcePointer = source.get();
        RenderAheadMixer renderer(std::move(source), 48000, 1, 64, 10, 1);
        for (std::uint32_t attribute = 0; attribute < RenderAheadMixer::MaximumQueuedAttributes; ++attribute) {
            REQUIRE(renderer.queueSourceAttribute(1, attribute, 1.0f));
        }
        REQUIRE(!renderer.queueSourceAttribute(1, 99, 1.0f));
        REQUIRE(renderer.queueSourceAttribute(1, 0, 2.0f));
        renderer.stop();
        const auto writes = sourcePointer->writes();
        REQUIRE(writes.size() == RenderAheadMixer::MaximumQueuedAttributes);
        REQUIRE(writes[0].value == 2.0f);
        REQUIRE(renderer.queueSourceAttribute(1, 99, 1.0f));
    }
}
