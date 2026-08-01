#include "Test.h"

#include <iostream>

int main() {
    runGainDspTests();
    runFreeverbDspTests();
    std::cout << "YargAudio native DSP tests passed\n";
    return 0;
}
