#include "Test.h"

#include <iostream>

int main() {
    runAudioRingBufferTests();
    runBassBindingTests();
    runGainDspTests();
    runFreeverbDspTests();
    runDattorroReverbDspTests();
    runNoiseGateDspTests();
    runPitchShifterTests();
    runNoiseMorphTests();
    runYargStretchTests();
    runSineSynthDspTests();
    runScheduledSampleSourceTests();
    runNativeOneShotStreamTests();
    runStretchTempoStreamTests();
    runRenderAheadMixerTests();
    runReadAheadStreamTests();
    std::cout << "YargAudio native tests passed\n";
    return 0;
}
