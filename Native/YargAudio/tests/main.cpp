#include "Test.h"

#include <iostream>

void runPipeWireSourceListerTests();

int main() {
    runGainDspTests();
    runFreeverbDspTests();
    runScheduledSampleSourceTests();
    runNativeOneShotStreamTests();
    runPipeWireSourceListerTests();
    std::cout << "YargAudio native DSP/one-shot tests passed\n";
    return 0;
}
