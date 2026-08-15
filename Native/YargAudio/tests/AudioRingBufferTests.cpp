#include "AudioRingBuffer.h"
#include "Test.h"

#include <atomic>
#include <cstddef>
#include <thread>

using yarg::audio::AudioRingBuffer;

static void testEmptyFullAndPartial() {
    AudioRingBuffer ring(4, 2);
    float input[] = { 0, 10, 1, 11, 2, 12, 3, 13, 4, 14 };
    float output[10]{};

    REQUIRE(ring.available() == 0);
    REQUIRE(ring.write(input, 5) == 4);
    REQUIRE(ring.freeSpace() == 0);
    REQUIRE(ring.read(output, 2) == 2);
    REQUIRE(output[0] == 0 && output[1] == 10);
    REQUIRE(output[2] == 1 && output[3] == 11);
    REQUIRE(ring.read(output, 4) == 2);
    REQUIRE(ring.available() == 0);
}

static void testWraparound() {
    AudioRingBuffer ring(4, 1);
    float first[] = { 0, 1, 2 };
    float second[] = { 3, 4, 5 };
    float output[4]{};

    REQUIRE(ring.write(first, 3) == 3);
    REQUIRE(ring.read(output, 2) == 2);
    REQUIRE(ring.write(second, 3) == 3);
    REQUIRE(ring.read(output, 4) == 4);
    REQUIRE(output[0] == 2 && output[1] == 3 && output[2] == 4 && output[3] == 5);
}

static void testConcurrentSpsc() {
    constexpr std::size_t count = 200000;
    AudioRingBuffer ring(257, 1);
    std::atomic<bool> producerDone{false};

    std::thread producer([&] {
        std::size_t value = 0;
        while (value < count) {
            float sample = static_cast<float>(value);
            if (ring.write(&sample, 1) == 1) ++value;
            else std::this_thread::yield();
        }
        producerDone.store(true);
    });

    std::size_t expected = 0;
    while (!producerDone.load() || ring.available() != 0) {
        float sample = 0;
        if (ring.read(&sample, 1) == 1) {
            REQUIRE(sample == static_cast<float>(expected));
            ++expected;
        } else {
            std::this_thread::yield();
        }
    }
    producer.join();
    REQUIRE(expected == count);
}

void runAudioRingBufferTests() {
    testEmptyFullAndPartial();
    testWraparound();
    testConcurrentSpsc();
}
