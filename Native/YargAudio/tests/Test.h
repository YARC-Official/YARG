#pragma once

#include <cstdlib>
#include <iostream>

#define REQUIRE(condition) do { \
    if (!(condition)) { \
        std::cerr << __FILE__ << ':' << __LINE__ << ": requirement failed: " \
                  << #condition << '\n'; \
        std::abort(); \
    } \
} while (false)

void runRenderAheadMixerTests();
void runReadAheadStreamTests();
void runAudioRingBufferTests();
void runBassBindingTests();
void runGainDspTests();
void runFreeverbDspTests();
void runDattorroReverbDspTests();
void runNoiseGateDspTests();
void runSineSynthDspTests();
void runScheduledSampleSourceTests();
void runNativeOneShotStreamTests();
