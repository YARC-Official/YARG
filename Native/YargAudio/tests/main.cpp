#include "Test.h"

#include <iostream>

int main() {
    runGainDspTests();
    runFreeverbDspTests();
    runScheduledSampleSourceTests();
    runNativeOneShotStreamTests();
    std::cout << "YargAudio native DSP/one-shot tests passed\n";
    return 0;
}
