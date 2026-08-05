#include "PipeWireSourceLister.h"
#include "Test.h"

#include <iostream>

// Smoke test: the snapshot must either degrade gracefully when PipeWire is
// absent (YARG_AUDIO_ERROR_DEPENDENCY = -4) or return a sane snapshot.
void runPipeWireSourceListerTests() {
    yarg::audio::PipeWireSourceLister lister;
    std::vector<yarg::audio::InputSourceInfo> sources;
    const int result = lister.list(sources);

    REQUIRE(result == 0 || result == -4);
    if (result == -4) {
        std::cout << "PipeWire unavailable; skipped source snapshot checks\n";
        return;
    }

    for (const auto& source : sources) {
        REQUIRE(source.alsaCard >= -1);
        REQUIRE(source.alsaDevice >= -1);
        REQUIRE(source.alsaSubdevice >= -1);
        REQUIRE(source.captureChannels >= 1);
        REQUIRE(source.captureChannel >= -1);
        if (source.captureChannel >= 0) {
            REQUIRE(source.captureChannel < source.captureChannels);
        }
        REQUIRE(!source.nodeName.empty());
        REQUIRE(!source.alsaPath.empty());
        if (source.captureChannel >= 0) {
            std::cout << "split source: " << source.description
                      << " ch=" << source.captureChannel
                      << "/" << source.captureChannels
                      << " path=" << source.alsaPath << '\n';
        }
    }
    std::cout << "PipeWire snapshot: " << sources.size() << " source(s)\n";
}
