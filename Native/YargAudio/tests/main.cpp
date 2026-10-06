#include "Test.h"

#include <iostream>

int main() {
    runAudioRingBufferTests();
    runBassBindingTests();
    runGainDspTests();
    runFreeverbDspTests();
    runDattorroReverbDspTests();
    runNoiseGateDspTests();
    runNoiseMorphTests();
    runYargStretchTests();
    runSineSynthDspTests();
    runScheduledSampleSourceTests();
    runNativeOneShotStreamTests();
    runRenderAheadMixerTests();
    runReadAheadStreamTests();
    std::cout << "YargAudio native tests passed\n";
    return 0;
}
