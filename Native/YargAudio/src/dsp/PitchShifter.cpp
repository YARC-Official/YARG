#include "dsp/PitchShifter.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <new>

namespace yarg::audio {
namespace {

constexpr double FADE_SECONDS = 0.012;
constexpr double HALF_JUMP_SECONDS = 0.0125;
constexpr double HALF_SEARCH_SECONDS = 0.010;
constexpr double RETURN_SEARCH_SECONDS = 0.0025;
constexpr double SMOOTHING_SECONDS = 0.005;
constexpr double RETRY_SECONDS = 0.0025;
constexpr float GOOD_CORRELATION = 0.8f;
constexpr double DECIMATED_RATE = 12000.0;
constexpr double GLIDE_PER_FRAME = 0.004;
constexpr double RATIO_SNAP = 1e-7;
constexpr std::uint32_t MARGIN_FRAMES = 8;
constexpr std::size_t MIN_CORRELATION_FRAMES = 32;
constexpr float SILENCE_ENERGY_PER_FRAME = 1e-10f;
constexpr std::uint32_t MIN_SAMPLE_RATE = 8000;
constexpr std::uint32_t MAX_SAMPLE_RATE = 384000;
constexpr double PI = 3.14159265358979323846;

std::uint32_t framesFor(double seconds, std::uint32_t sampleRate) noexcept {
    return std::max<std::uint32_t>(1, static_cast<std::uint32_t>(std::lround(seconds * sampleRate)));
}

std::uint32_t maximumDelay(std::uint32_t sampleRate) noexcept {
    return PitchShifter::latencyFrames(sampleRate) + framesFor(HALF_JUMP_SECONDS, sampleRate) +
        framesFor(HALF_SEARCH_SECONDS, sampleRate) + framesFor(FADE_SECONDS, sampleRate) + MARGIN_FRAMES;
}

std::size_t nextPowerOfTwo(std::size_t value) noexcept {
    std::size_t result = 1;
    while (result < value) result <<= 1;
    return result;
}

} // namespace

std::uint32_t PitchShifter::latencyFrames(std::uint32_t sampleRate) noexcept {
    if (sampleRate < MIN_SAMPLE_RATE || sampleRate > MAX_SAMPLE_RATE) return 0;
    return framesFor(HALF_JUMP_SECONDS, sampleRate) + framesFor(HALF_SEARCH_SECONDS, sampleRate) +
        framesFor(FADE_SECONDS, sampleRate) + MARGIN_FRAMES;
}

bool PitchShifter::configure(std::uint32_t channels, std::uint32_t sampleRate) noexcept {
    if (channels == 0 || channels > MAX_CHANNELS || latencyFrames(sampleRate) == 0) return false;

    channels_ = channels;
    sampleRate_ = sampleRate;
    nominalDelay_ = latencyFrames(sampleRate);
    fadeFrames_ = framesFor(FADE_SECONDS, sampleRate);
    halfJump_ = framesFor(HALF_JUMP_SECONDS, sampleRate);
    halfSearch_ = framesFor(HALF_SEARCH_SECONDS, sampleRate);
    returnSearch_ = framesFor(RETURN_SEARCH_SECONDS, sampleRate);
    retryFrames_ = framesFor(RETRY_SECONDS, sampleRate);
    retryWindow_ = halfSearch_ / 2;
    decimation_ = std::max<std::uint32_t>(1, static_cast<std::uint32_t>(std::lround(sampleRate / DECIMATED_RATE)));
    smoothing_ = 1.0 - std::exp(-1.0 / (SMOOTHING_SECONDS * sampleRate));

    const std::uint32_t maxDelay = maximumDelay(sampleRate);
    const std::size_t ringFrames = nextPowerOfTwo(2 * static_cast<std::size_t>(maxDelay) + 64);
    ringMask_ = ringFrames - 1;
    chunkFrames_ = ringFrames - maxDelay - MARGIN_FRAMES;

    const std::size_t maxSpan = 2 * static_cast<std::size_t>(std::max(halfSearch_, returnSearch_));
    try {
        ring_.assign(ringFrames * channels, 0.0f);
        fadeLinear_.resize(fadeFrames_);
        fadePower_.resize(fadeFrames_);
        scratchA_.resize(fadeFrames_ + 1);
        scratchB_.resize((maxSpan + fadeFrames_) / decimation_ + 4);
    }
    catch (const std::bad_alloc&) {
        channels_ = 0;
        return false;
    }

    for (std::uint32_t i = 0; i < fadeFrames_; ++i) {
        const double x = (i + 0.5) / fadeFrames_;
        fadeLinear_[i] = static_cast<float>(0.5 - 0.5 * std::cos(PI * x));
        fadePower_[i] = static_cast<float>(std::sin(0.5 * PI * x));
    }

    target_ = 1.0;
    reset();
    return true;
}

void PitchShifter::reset() noexcept {
    std::fill(ring_.begin(), ring_.end(), 0.0f);
    written_ = ringMask_ + 1;
    ratio_ = target_;
    delay_ = nominalDelay_;
    nextDelay_ = nominalDelay_;
    fading_ = false;
    fadePosition_ = 0;
    retryCountdown_ = 0;
}

void PitchShifter::setRatio(float ratio) noexcept {
    if (!std::isfinite(ratio)) ratio = 1.0f;
    target_ = std::clamp(ratio, MIN_RATIO, MAX_RATIO);
}

bool PitchShifter::settled() const noexcept {
    return !fading_ && target_ == 1.0 && ratio_ == 1.0 && delay_ == nominalDelay_;
}

void PitchShifter::process(float* interleaved, std::size_t frames) noexcept {
    if (channels_ == 0 || !interleaved) return;

    while (frames > 0) {
        const std::size_t count = std::min(frames, chunkFrames_);
        writeRing(interleaved, count);
        if (settled()) {
            copyDelayed(interleaved, count);
        }
        else {
            processShifting(interleaved, count);
        }
        interleaved += count * channels_;
        frames -= count;
    }
}

void PitchShifter::writeRing(const float* input, std::size_t frames) noexcept {
    const std::size_t ringFrames = ringMask_ + 1;
    const std::size_t start = written_ & ringMask_;
    const std::size_t first = std::min(frames, ringFrames - start);
    std::memcpy(ring_.data() + start * channels_, input, first * channels_ * sizeof(float));
    std::memcpy(ring_.data(), input + first * channels_, (frames - first) * channels_ * sizeof(float));
    written_ += frames;
}

void PitchShifter::copyDelayed(float* output, std::size_t frames) noexcept {
    const std::size_t ringFrames = ringMask_ + 1;
    const std::size_t start = (written_ - frames - nominalDelay_) & ringMask_;
    const std::size_t first = std::min(frames, ringFrames - start);
    std::memcpy(output, ring_.data() + start * channels_, first * channels_ * sizeof(float));
    std::memcpy(output + first * channels_, ring_.data(), (frames - first) * channels_ * sizeof(float));
}

void PitchShifter::readTap(std::uint64_t frame, double delay, float gain, float* output) const noexcept {
    const double position = static_cast<double>(frame) - delay;
    const double whole = std::floor(position);
    const float t = static_cast<float>(position - whole);
    const auto base = static_cast<std::uint64_t>(whole);
    const float* p0 = ring_.data() + ((base - 1) & ringMask_) * channels_;
    const float* p1 = ring_.data() + (base & ringMask_) * channels_;
    const float* p2 = ring_.data() + ((base + 1) & ringMask_) * channels_;
    const float* p3 = ring_.data() + ((base + 2) & ringMask_) * channels_;
    for (std::uint32_t c = 0; c < channels_; ++c) {
        const float ym1 = p0[c];
        const float y0 = p1[c];
        const float y1 = p2[c];
        const float y2 = p3[c];
        const float c1 = 0.5f * (y1 - ym1);
        const float c2 = ym1 - 2.5f * y0 + 2.0f * y1 - 0.5f * y2;
        const float c3 = 0.5f * (y2 - ym1) + 1.5f * (y0 - y1);
        output[c] += gain * (((c3 * t + c2) * t + c1) * t + y0);
    }
}

void PitchShifter::processShifting(float* output, std::size_t frames) noexcept {
    const std::uint64_t base = written_ - frames;
    for (std::size_t i = 0; i < frames; ++i) {
        const std::uint64_t frame = base + i;
        ratio_ += (target_ - ratio_) * smoothing_;
        if (std::abs(target_ - ratio_) < RATIO_SNAP) ratio_ = target_;

        float* out = output + i * channels_;
        std::fill(out, out + channels_, 0.0f);
        if (fading_) {
            const std::uint32_t in = fadePosition_;
            const std::uint32_t outIndex = fadeFrames_ - 1 - in;
            const float gainIn = fadeLinear_[in] + (fadePower_[in] - fadeLinear_[in]) * fadeShape_;
            const float gainOut = fadeLinear_[outIndex] +
                (fadePower_[outIndex] - fadeLinear_[outIndex]) * fadeShape_;
            readTap(frame, delay_, gainOut, out);
            readTap(frame, nextDelay_, gainIn, out);
        }
        else {
            readTap(frame, delay_, 1.0f, out);
        }

        const double step = 1.0 - ratio_;
        delay_ += step;
        if (fading_) {
            nextDelay_ += step;
            if (++fadePosition_ >= fadeFrames_) {
                delay_ = nextDelay_;
                fading_ = false;
            }
        }
        if (!fading_) maybeStartSplice(frame + 1);
    }
}

void PitchShifter::maybeStartSplice(std::uint64_t nextFrame) noexcept {
    const auto jump = static_cast<std::int64_t>(2 * halfJump_);
    const auto search = static_cast<std::int64_t>(halfSearch_);
    const double offset = delay_ - nominalDelay_;
    if (ratio_ < 1.0) {
        if (offset >= halfJump_) {
            startSplice(nextFrame, jump - search, jump + search, jump, offset >= halfJump_ + retryWindow_);
        }
        return;
    }
    if (ratio_ > 1.0) {
        if (-offset >= halfJump_) {
            startSplice(nextFrame, -jump - search, -jump + search, -jump, -offset >= halfJump_ + retryWindow_);
        }
        return;
    }
    if (target_ != 1.0 || delay_ == nominalDelay_) return;

    if (std::abs(offset) > returnSearch_) {
        const auto centre = static_cast<std::int64_t>(std::llround(offset));
        const auto range = static_cast<std::int64_t>(returnSearch_);
        startSplice(nextFrame, centre - range, centre + range, centre, true);
        return;
    }
    delay_ = std::abs(offset) <= GLIDE_PER_FRAME ? nominalDelay_
        : delay_ - std::copysign(GLIDE_PER_FRAME, offset);
}

void PitchShifter::startSplice(std::uint64_t nextFrame, std::int64_t lagLow, std::int64_t lagHigh,
    std::int64_t fallbackLag, bool force) noexcept {
    if (retryCountdown_ > 0 && !force) {
        --retryCountdown_;
        return;
    }

    const auto start = static_cast<std::int64_t>(std::floor(static_cast<double>(nextFrame) - delay_));
    const std::int64_t available = static_cast<std::int64_t>(nextFrame) - 2 - start - std::max<std::int64_t>(lagHigh, 0);
    const auto length = static_cast<std::size_t>(std::clamp<std::int64_t>(available, 0, fadeFrames_));

    float correlation = 0.0f;
    std::int64_t lag = fallbackLag;
    if (length >= MIN_CORRELATION_FRAMES) {
        lag = findBestLag(start, length, lagLow, lagHigh, fallbackLag, correlation);
    }

    if (!force && length >= MIN_CORRELATION_FRAMES && correlation < GOOD_CORRELATION) {
        retryCountdown_ = retryFrames_;
        return;
    }

    retryCountdown_ = 0;
    nextDelay_ = delay_ - static_cast<double>(lag);
    fadeShape_ = std::clamp(1.0f - correlation, 0.0f, 1.0f);
    fadePosition_ = 0;
    fading_ = true;
    ++spliceCount_;
}

float PitchShifter::mono(std::int64_t frame) const noexcept {
    const float* p = ring_.data() + (static_cast<std::uint64_t>(frame) & ringMask_) * channels_;
    float sum = 0.0f;
    for (std::uint32_t c = 0; c < channels_; ++c) sum += p[c];
    return sum;
}

std::int64_t PitchShifter::findBestLag(std::int64_t start, std::size_t length, std::int64_t lagLow,
    std::int64_t lagHigh, std::int64_t fallbackLag, float& correlation) noexcept {
    const std::int64_t q = decimation_;
    const std::size_t coarseLength = length / decimation_;
    float energyA = 0.0f;
    for (std::size_t k = 0; k < coarseLength; ++k) {
        const float value = mono(start + static_cast<std::int64_t>(k) * q);
        scratchA_[k] = value;
        energyA += value * value;
    }
    if (coarseLength == 0 || energyA < SILENCE_ENERGY_PER_FRAME * coarseLength) {
        correlation = 1.0f;
        return fallbackLag;
    }

    const auto coarseSteps = static_cast<std::size_t>((lagHigh - lagLow) / q);
    for (std::size_t m = 0; m < coarseSteps + coarseLength; ++m) {
        scratchB_[m] = mono(start + lagLow + static_cast<std::int64_t>(m) * q);
    }

    double energyB = 0.0;
    for (std::size_t k = 0; k < coarseLength; ++k) energyB += scratchB_[k] * scratchB_[k];

    double bestScore = -2.0;
    std::int64_t coarseLag = fallbackLag;
    for (std::size_t j = 0; j <= coarseSteps; ++j) {
        if (j > 0) {
            const float leaving = scratchB_[j - 1];
            const float entering = scratchB_[j + coarseLength - 1];
            energyB = std::max(0.0, energyB - leaving * leaving + entering * entering);
        }
        double dot = 0.0;
        for (std::size_t k = 0; k < coarseLength; ++k) dot += scratchA_[k] * scratchB_[j + k];
        const double score = dot / std::sqrt(energyA * energyB + 1e-20);
        if (score > bestScore) {
            bestScore = score;
            coarseLag = lagLow + static_cast<std::int64_t>(j) * q;
        }
    }

    for (std::size_t k = 0; k < length; ++k) scratchA_[k] = mono(start + static_cast<std::int64_t>(k));
    double fullEnergyA = 0.0;
    for (std::size_t k = 0; k < length; ++k) fullEnergyA += scratchA_[k] * scratchA_[k];

    const std::int64_t refineLow = std::max(lagLow, coarseLag - q + 1);
    const std::int64_t refineHigh = std::min(lagHigh, coarseLag + q - 1);
    bestScore = -2.0;
    std::int64_t bestLag = coarseLag;
    for (std::int64_t lag = refineLow; lag <= refineHigh; ++lag) {
        double dot = 0.0;
        double energyLag = 0.0;
        for (std::size_t k = 0; k < length; ++k) {
            const float value = mono(start + lag + static_cast<std::int64_t>(k));
            dot += scratchA_[k] * value;
            energyLag += value * value;
        }
        const double score = dot / std::sqrt(fullEnergyA * energyLag + 1e-20);
        if (score > bestScore) {
            bestScore = score;
            bestLag = lag;
        }
    }

    correlation = static_cast<float>(bestScore);
    return bestLag;
}

} // namespace yarg::audio
