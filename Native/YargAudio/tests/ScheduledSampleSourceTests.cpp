#include "one_shot/ScheduledSampleSource.h"
#include "Test.h"

#include <cmath>
#include <limits>
#include <memory>
#include <vector>

using yarg::audio::ScheduledSampleSource;

namespace {

std::unique_ptr<ScheduledSampleSource> create(
    std::uint32_t sampleRate, std::uint32_t channels,
    const std::vector<float>& pcm, const std::vector<double>& schedule,
    double leadTime = 0) {
    return ScheduledSampleSource::create(sampleRate, channels,
        pcm.data(), pcm.size(), schedule.data(), schedule.size(), leadTime);
}

void testInputValidation() {
    const std::vector<float> pcm{1, 2, 3, 4};
    const std::vector<double> schedule{0};
    REQUIRE(!create(0, 1, pcm, schedule));
    REQUIRE(!create(1, 0, pcm, schedule));
    REQUIRE(!ScheduledSampleSource::create(1, 1, nullptr, 1,
        schedule.data(), schedule.size(), 0));
    REQUIRE(!ScheduledSampleSource::create(1, 3, pcm.data(), pcm.size(),
        schedule.data(), schedule.size(), 0));
    REQUIRE(!ScheduledSampleSource::create(1, 1, pcm.data(), pcm.size(),
        nullptr, 1, 0));
    REQUIRE(!ScheduledSampleSource::create(1, 1, pcm.data(), pcm.size(),
        schedule.data(), schedule.size(), -1));

    const std::vector<double> descending{0, -1};
    REQUIRE(!create(1, 1, pcm, descending));
    const std::vector<double> nanSchedule{std::numeric_limits<double>::quiet_NaN()};
    REQUIRE(!create(1, 1, pcm, nanSchedule));
    REQUIRE(!ScheduledSampleSource::create(1, 1, pcm.data(), pcm.size(),
        schedule.data(), schedule.size(), std::numeric_limits<double>::infinity()));
}

void testScheduledMixingAndControls() {
    auto source = create(4, 1, {1, -0.5f}, {0});
    REQUIRE(source);
    REQUIRE(source->reset(0, 1, false, true));

    float output[4] = {9, 9, 9, 9};
    source->render(output, 4);
    REQUIRE(output[0] == 1 && output[1] == -0.5f);
    REQUIRE(output[2] == 0 && output[3] == 0);
    REQUIRE(source->cursorFrame() == 4);

    REQUIRE(source->setGain(0));
    REQUIRE(source->reset(0, 1, false, true));
    output[0] = output[1] = output[2] = output[3] = 9;
    source->render(output, 4);
    REQUIRE(output[0] == 0 && output[1] == 0 && output[2] == 0 && output[3] == 0);

    REQUIRE(source->setGain(1));
    REQUIRE(source->reset(0, 1, true, true));
    source->render(output, 4);
    REQUIRE(output[0] == 0 && source->cursorFrame() == 0);
    source->setPaused(false);
    source->render(output, 2);
    REQUIRE(output[0] == 1 && output[1] == -0.5f);
    REQUIRE(!source->setGain(std::numeric_limits<float>::quiet_NaN()));
    REQUIRE(!source->reset(0, 0, false, true));
}

void testBoundariesSpeedAndSeek() {
    auto source = create(10, 1, {1}, {0.3, 0.8}, 0);
    REQUIRE(source->reset(0, 1, false, true));
    float output[4]{};
    source->render(output, 4);
    REQUIRE(output[0] == 0 && output[1] == 0 && output[2] == 0 && output[3] == 1);

    float second[5]{};
    source->render(second, 5);
    REQUIRE(second[0] == 0 && second[3] == 0 && second[4] == 1);

    auto leadSource = create(10, 1, {1}, {0.1, 0.4}, 0.1);
    REQUIRE(leadSource->reset(0, 1, false, true));
    float leadOutput[4]{};
    leadSource->render(leadOutput, 4);
    REQUIRE(leadOutput[2] == 0 && leadOutput[3] == 1);

    auto speedSource = create(10, 1, {1}, {0.6}, 0.1);
    REQUIRE(speedSource->reset(0, 2, false, true));
    float speedOutput[3]{};
    speedSource->render(speedOutput, 3);
    REQUIRE(speedOutput[0] == 0 && speedOutput[1] == 0 && speedOutput[2] == 1);

    auto midpointSource = create(2, 1, {1}, {0.25});
    REQUIRE(midpointSource->reset(0, 1, false, true));
    float midpointOutput[1]{};
    midpointSource->render(midpointOutput, 1);
    REQUIRE(midpointOutput[0] == 1);

    auto seekSource = create(10, 1, {1, 2}, {0, 1, 1.2});
    REQUIRE(seekSource->reset(0, 1, false, true));
    float seekOutput[2]{};
    seekSource->render(seekOutput, 2);
    REQUIRE(seekOutput[0] == 1 && seekOutput[1] == 2);
    REQUIRE(seekSource->reset(1.2, 1, false, true));
    seekOutput[0] = seekOutput[1] = 0;
    seekSource->render(seekOutput, 2);
    REQUIRE(seekOutput[0] == 1 && seekOutput[1] == 2);
}

void testResetActiveVoiceBehavior() {
    auto source = create(10, 1, {1, 2, 3, 4}, {0, 1});
    REQUIRE(source->reset(0, 1, false, true));

    float first[2]{};
    source->render(first, 2);
    REQUIRE(first[0] == 1 && first[1] == 2);
    REQUIRE(source->activeVoiceCount() == 1);

    // Speed changes re-anchor future events without cutting an active sample.
    REQUIRE(source->reset(0.2, 2, false, false));
    REQUIRE(source->activeVoiceCount() == 1);
    float tail[2]{};
    source->render(tail, 2);
    REQUIRE(tail[0] == 3 && tail[1] == 4);
    REQUIRE(source->activeVoiceCount() == 0);

    // Future events use the new speed and anchor exactly once.
    float future[3]{};
    source->render(future, 3);
    REQUIRE(future[0] == 0 && future[1] == 0 && future[2] == 1);
    float futureTail[3]{};
    source->render(futureTail, 3);
    REQUIRE(futureTail[0] == 2 && futureTail[1] == 3 && futureTail[2] == 4);

    // Seeks re-anchor future events and discard an active sample.
    REQUIRE(source->reset(0, 1, false, true));
    source->render(first, 2);
    REQUIRE(source->activeVoiceCount() == 1);
    REQUIRE(source->reset(0.2, 2, false, true));
    REQUIRE(source->activeVoiceCount() == 0);
    tail[0] = tail[1] = 9;
    source->render(tail, 2);
    REQUIRE(tail[0] == 0 && tail[1] == 0);
}

void testChannelsOverlapAndSaturation() {
    auto stereo = create(4, 2, {1, 10, 2, 20}, {0});
    REQUIRE(stereo->reset(0, 1, false, true));
    float stereoOutput[4]{};
    stereo->render(stereoOutput, 2);
    REQUIRE(stereoOutput[0] == 1 && stereoOutput[1] == 10);
    REQUIRE(stereoOutput[2] == 2 && stereoOutput[3] == 20);

    std::vector<double> schedule(ScheduledSampleSource::MaxActiveVoices + 1, 0);
    auto saturated = create(1, 1, {0.5f, 0.5f}, schedule);
    REQUIRE(saturated->reset(0, 1, false, true));
    float output[1]{};
    saturated->render(output, 1);
    REQUIRE(output[0] == 32);
    REQUIRE(saturated->activeVoiceCount() == ScheduledSampleSource::MaxActiveVoices);
    REQUIRE(saturated->droppedVoiceCount() == 1);
    float nextOutput[1]{};
    saturated->render(nextOutput, 1);
    REQUIRE(nextOutput[0] == 32);
    REQUIRE(saturated->activeVoiceCount() == 0);
}

} // namespace

void runScheduledSampleSourceTests() {
    testInputValidation();
    testScheduledMixingAndControls();
    testBoundariesSpeedAndSeek();
    testResetActiveVoiceBehavior();
    testChannelsOverlapAndSaturation();
}
