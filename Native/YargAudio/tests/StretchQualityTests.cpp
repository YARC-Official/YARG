#include "stretch/YargStretch.h"
#include "Test.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <complex>
#include <random>
#include <vector>

namespace yarg::audio {

struct StretchDiagnostics {
    static auto &stft(YargStretch<float> &stretch) {
        return stretch.stft;
    }
    static float textureEnergy(YargStretch<float> &stretch) {
        return stretch.noiseMorph.textureEnergyRatio();
    }
};

}

namespace {

constexpr int RATE = 44100;
constexpr int WINDOW = 3072;
constexpr int HOP = 384;
constexpr int OUTPUT_SAMPLES = RATE * 2;
constexpr double PI = 3.14159265358979323846;
using Stretch = yarg::audio::YargStretch<float>;
using DiagnosticSTFT = signalsmith::linear::DynamicSTFT<float, false, true>;
struct Spectrogram {
    std::vector<float> analysisWindow;
    std::vector<float> synthesisWindow;
    int analysisOffset = 0;
    int synthesisOffset = 0;
    std::vector<std::vector<std::complex<float>>> frames;
};
using Stereo = std::array<std::vector<float>, 2>;

Stereo render(Stretch &stretch, const std::vector<float> &source, double speed,
    float pitch, float rightGain, Spectrogram *spectrogram = nullptr) {
    stretch.reset(0);
    stretch.setTransposeFactor(pitch);
    std::vector<float> right(source.size());
    for (int i = 0; i < int(source.size()); ++i) {
        right[i] = source[i] * rightGain;
    }
    const float *inputs[] = {source.data(), right.data()};
    int position = stretch.inputLatency();
    stretch.seek(inputs, position, speed);
    if (spectrogram) {
        auto &transform = yarg::audio::StretchDiagnostics::stft(stretch);
        spectrogram->analysisWindow.assign(transform.analysisWindow(), transform.analysisWindow() + transform.blockSamples());
        spectrogram->synthesisWindow.assign(transform.synthesisWindow(), transform.synthesisWindow() + transform.blockSamples());
        spectrogram->analysisOffset = int(transform.analysisOffset());
        spectrogram->synthesisOffset = int(transform.synthesisOffset());
        spectrogram->frames.clear();
    }
    Stereo output{std::vector<float>(OUTPUT_SAMPLES), std::vector<float>(OUTPUT_SAMPLES)};
    std::array<float, HOP> leftBlock{}, rightBlock{};
    float *outputs[] = {leftBlock.data(), rightBlock.data()};
    double remainder = 0;
    for (int frame = 0; frame < OUTPUT_SAMPLES; frame += HOP) {
        const double exact = speed * HOP + remainder;
        const int count = int(exact);
        remainder = exact - count;
        REQUIRE(position + count <= int(source.size()));
        inputs[0] = source.data() + position;
        inputs[1] = right.data() + position;
        stretch.process(inputs, count, outputs, HOP);
        if (spectrogram) {
            auto &transform = yarg::audio::StretchDiagnostics::stft(stretch);
            spectrogram->frames.emplace_back(transform.spectrum(0), transform.spectrum(0) + transform.bands());
        }
        position += count;
        const int copied = std::min(HOP, OUTPUT_SAMPLES - frame);
        std::copy_n(leftBlock.begin(), copied, output[0].begin() + frame);
        std::copy_n(rightBlock.begin(), copied, output[1].begin() + frame);
    }
    return output;
}

double power(const std::vector<float> &signal) {
    double result = 0;
    for (int i = RATE; i < OUTPUT_SAMPLES; ++i) {
        REQUIRE(std::isfinite(signal[i]));
        result += double(signal[i]) * signal[i];
    }
    return result / RATE;
}

double tonePower(const std::vector<float> &signal, double frequency) {
    const auto rotation = std::polar(1.0, -2 * PI * frequency / RATE);
    std::complex<double> phase{1, 0};
    std::complex<double> sum{};
    for (int i = RATE; i < OUTPUT_SAMPLES; ++i) {
        sum += double(signal[i]) * phase;
        phase *= rotation;
    }
    return 2 * std::norm(sum / double(RATE));
}

struct SpectralError {
    double convergence = 0;
    double kl = 0;
    double is = 0;
};

SpectralError spectralError(const std::vector<double> &reference, const std::vector<double> &actual) {
    constexpr double RELATIVE_POWER_FLOOR = 1e-8;
    const double floor = *std::max_element(reference.begin(), reference.end()) * RELATIVE_POWER_FLOOR;
    double referencePower = 0;
    double squaredError = 0;
    SpectralError error;
    for (int i = 0; i < int(reference.size()); ++i) {
        referencePower += reference[i];
        const double difference = std::sqrt(actual[i]) - std::sqrt(reference[i]);
        squaredError += difference * difference;
        const double target = reference[i] + floor;
        const double observed = actual[i] + floor;
        const double ratio = target / observed;
        error.kl += target * std::log(ratio) - target + observed;
        error.is += ratio - std::log(ratio) - 1;
    }
    error.convergence = std::sqrt(squaredError / referencePower);
    error.kl /= referencePower;
    error.is /= reference.size();
    return error;
}

std::vector<double> averageSpectrum(const std::vector<float> &signal, int window) {
    signalsmith::linear::DynamicSTFT<float, false, true> analysis;
    analysis.configure(1, 0, window);
    analysis.setInterval(window / 4, analysis.kaiser);
    std::vector<double> spectrum(analysis.bands());
    int frames = 0;
    for (int first = RATE; first + window <= OUTPUT_SAMPLES; first += window / 4) {
        analysis.writeInput(0, window, signal.data() + first);
        analysis.moveInput(window);
        analysis.analyse();
        for (int bin = 0; bin < int(spectrum.size()); ++bin) {
            const std::complex<double> value = analysis.spectrum(0)[bin];
            spectrum[bin] += std::norm(value);
        }
        ++frames;
    }
    for (auto &value : spectrum) {
        value /= frames;
    }
    return spectrum;
}

void requireSpectralMetrics() {
    const std::vector<double> reference{1, 1e-4};
    const auto exact = spectralError(reference, reference);
    REQUIRE(exact.convergence == 0);
    REQUIRE(exact.kl == 0);
    REQUIRE(exact.is == 0);
    const auto amplified = spectralError(reference, std::vector<double>{4, 4e-4});
    REQUIRE(std::abs(amplified.convergence - 1) < 1e-12);
    REQUIRE(std::abs(amplified.kl - (3 - std::log(4.0))) < 1e-6);
    REQUIRE(std::abs(amplified.is - (0.25 - std::log(0.25) - 1)) < 1e-4);
    const auto quietLoss = spectralError(reference, std::vector<double>{1, 1e-4 / 16});
    REQUIRE(quietLoss.convergence < 0.01);
    REQUIRE(quietLoss.is > 6);
    const auto scaledLoss = spectralError(std::vector<double>{100, 0.01}, std::vector<double>{100, 0.01 / 16});
    REQUIRE(std::abs(quietLoss.kl - scaledLoss.kl) < 1e-12);
    REQUIRE(std::abs(quietLoss.is - scaledLoss.is) < 1e-12);
}

void configureDiagnostic(DiagnosticSTFT &transform, const Spectrogram &spectrogram) {
    transform.configure(1, 1, spectrogram.analysisWindow.size(), 0, HOP);
    transform.analysisOffset(spectrogram.analysisOffset);
    transform.synthesisOffset(spectrogram.synthesisOffset);
    std::copy(spectrogram.analysisWindow.begin(), spectrogram.analysisWindow.end(), transform.analysisWindow());
    std::copy(spectrogram.synthesisWindow.begin(), spectrogram.synthesisWindow.end(), transform.synthesisWindow());
    transform.reset(0);
}

double consistencyError(const Spectrogram &spectrogram, bool scramble) {
    DiagnosticSTFT synthesis;
    configureDiagnostic(synthesis, spectrogram);
    const int window = int(synthesis.blockSamples());
    std::vector<float> signal(spectrogram.frames.size() * HOP + window);
    int position = 0;
    for (const auto &frame : spectrogram.frames) {
        for (int bin = 0; bin < int(frame.size()); ++bin) {
            synthesis.spectrum(0)[bin] = frame[bin];
            if (scramble) {
                synthesis.spectrum(0)[bin] *= std::polar(1.0f, float(bin * bin % 31));
            }
        }
        synthesis.synthesise();
        synthesis.readOutput(0, HOP, signal.data() + position);
        synthesis.moveOutput(HOP);
        position += HOP;
    }
    synthesis.readOutput(0, window, signal.data() + position);
    DiagnosticSTFT analysis;
    configureDiagnostic(analysis, spectrogram);
    double difference = 0;
    double energy = 0;
    for (int first = window; first + window < position; first += HOP) {
        analysis.writeInput(0, window, signal.data() + first);
        analysis.moveInput(window);
        analysis.analyse();
        const auto &frame = spectrogram.frames[first / HOP];
        for (int bin = 0; bin < int(frame.size()); ++bin) {
            auto target = frame[bin];
            if (scramble) {
                target *= std::polar(1.0f, float(bin * bin % 31));
            }
            difference += std::norm(std::complex<double>(analysis.spectrum(0)[bin] - target));
            energy += std::norm(std::complex<double>(target));
        }
    }
    return std::sqrt(difference / energy);
}

int attackPosition(const std::vector<float> &signal, int first, int last) {
    constexpr int ATTACK_WINDOW = 44;
    double largest = -1;
    int position = first;
    for (int i = first; i < last; ++i) {
        double energy = 0;
        for (int j = 0; j < ATTACK_WINDOW; ++j) {
            energy += double(signal[i + j]) * signal[i + j];
        }
        if (energy > largest) {
            largest = energy;
            position = i;
        }
    }
    return position;
}

void reportOnsets(Stretch &stretch, int sourceSamples) {
    constexpr int SEARCH_RADIUS = WINDOW;
    for (double speed : {0.1, 0.25, 0.5, 0.75, 1.0, 2.0}) {
        std::vector<float> source(sourceSamples);
        for (int i = 0; i < sourceSamples; ++i) {
            source[i] = float(1e-4 * std::sin(2 * PI * 440 * i / RATE));
        }
        const std::array<int, 3> onsets{
            int(std::lround(speed * RATE / 2)), int(std::lround(speed * RATE)), int(std::lround(speed * RATE * 1.5))};
        for (int onset : onsets) {
            for (int i = 0; i < 882; ++i) {
                source[onset + i] += float(0.7 * std::exp(-double(i) / 110) * std::cos(2 * PI * 4000 * i / RATE));
            }
        }
        const auto output = render(stretch, source, speed, 1, 1);
        for (int onset : onsets) {
            const int expected = int(std::lround(onset / speed)) + stretch.outputLatency();
            const int measured = attackPosition(output[0], expected - SEARCH_RADIUS, expected + SEARCH_RADIUS);
            const int reference = attackPosition(source, std::max(0, onset - 882), onset + 882);
            REQUIRE(reference == onset);
            REQUIRE(measured > expected - SEARCH_RADIUS);
            REQUIRE(measured < expected + SEARCH_RADIUS - 1);
            const double errorMs = 1000.0 * (measured - expected) / RATE;
            double earlyEnergy = 0;
            double attackEnergy = 0;
            for (int i = expected - SEARCH_RADIUS; i < expected - 44; ++i) {
                earlyEnergy += double(output[0][i]) * output[0][i];
            }
            for (int i = expected; i < expected + SEARCH_RADIUS; ++i) {
                attackEnergy += double(output[0][i]) * output[0][i];
            }
            REQUIRE(std::isfinite(errorMs));
            REQUIRE(attackEnergy > 0);
            std::cout << "Stretch onset: speed=" << speed << " source=" << onset
                << " errorMs=" << errorMs << " preEchoRatio=" << earlyEnergy / attackEnergy << '\n';
        }
    }
}

double highFrequencyModulation(const std::vector<float> &signal) {
    const double coefficient = 1 - std::exp(-2 * PI * 6000 / RATE);
    double lowpass = 0;
    std::vector<double> energy;
    double block = 0;
    int count = 0;
    for (int i = RATE; i < OUTPUT_SAMPLES; ++i) {
        lowpass += coefficient * (signal[i] - lowpass);
        const double high = signal[i] - lowpass;
        block += high * high;
        if (++count == HOP) {
            energy.push_back(block / HOP);
            block = 0;
            count = 0;
        }
    }
    double mean = 0;
    for (double value : energy) {
        mean += value;
    }
    mean /= energy.size();
    double variance = 0;
    for (double value : energy) {
        variance += (value - mean) * (value - mean);
    }
    return std::sqrt(variance / energy.size()) / (mean + 1e-12);
}

void requireStereo(const Stereo &output, float rightGain) {
    double error = 0;
    double energy = 0;
    for (int i = 0; i < OUTPUT_SAMPLES; ++i) {
        REQUIRE(std::isfinite(output[0][i]));
        REQUIRE(std::isfinite(output[1][i]));
        const double difference = output[1][i] - double(rightGain) * output[0][i];
        error += difference * difference;
        energy += double(output[0][i]) * output[0][i];
    }
    REQUIRE(error < energy * 1e-10 + 1e-20);
}

}

void runStretchQualityTests() {
    requireSpectralMetrics();
    Stretch stretch(0);
    stretch.configure(2, RATE, WINDOW, HOP);
    stretch.setTransientFrequency(1500.0f / RATE);
    std::vector<float> harmonics(OUTPUT_SAMPLES * 2 + WINDOW + HOP);
    std::vector<float> vibrato(harmonics.size());
    std::vector<float> movingTone(harmonics.size());
    std::vector<float> sine(harmonics.size());
    std::vector<float> noise(harmonics.size());
    std::minstd_rand random(1);
    std::normal_distribution<float> distribution(0, 0.1f);
    double phase = 0;
    double movingPhase = 0;
    for (int i = 0; i < int(harmonics.size()); ++i) {
        for (int harmonic = 1; harmonic <= 4; ++harmonic) {
            harmonics[i] += float(0.1 * std::sin(2 * PI * 220 * harmonic * i / RATE + 0.3 * harmonic * harmonic));
        }
        phase += 2 * PI * (3000 + 25 * std::sin(2 * PI * 5 * i / RATE)) / RATE;
        vibrato[i] = float(0.2 * std::sin(phase));
        movingPhase += 2 * PI * (440 + 40 * std::sin(2 * PI * 5 * i / RATE)) / RATE;
        movingTone[i] = float(0.2 * std::sin(movingPhase));
        sine[i] = float(0.2 * std::sin(2 * PI * 440 * i / RATE));
        noise[i] = distribution(random);
    }
    for (double speed : {0.1, 0.25, 0.35, 0.5, 0.75, 1.0, 2.0}) {
        auto output = render(stretch, harmonics, speed, 1, 0.1f);
        double harmonicPower = 0;
        for (int harmonic = 1; harmonic <= 4; ++harmonic) {
            harmonicPower += tonePower(output[0], 220 * harmonic);
        }
        REQUIRE(harmonicPower / power(output[0]) > 0.995);
        requireStereo(output, 0.1f);
        output = render(stretch, vibrato, speed, 1, 0.1f);
        REQUIRE(std::abs(power(output[0]) / 0.02 - 1) < 0.04);
        requireStereo(output, 0.1f);
    }
    for (double speed : {0.25, 0.35, 0.5}) {
        const auto output = render(stretch, movingTone, speed, 1, 0.1f);
        REQUIRE(std::abs(power(output[0]) / 0.02 - 1) < 0.04);
        requireStereo(output, 0.1f);
    }
    for (float gain : {0.0f, 0.1f, 1.0f, -1.0f, 10.0f}) {
        const auto output = render(stretch, noise, 0.1, 1, gain);
        requireStereo(output, gain);
        REQUIRE(power(output[0]) > 0.003);
        REQUIRE(power(output[0]) < 0.02);
    }
    for (float pitch : {0.75f, 1.25f}) {
        const auto output = render(stretch, sine, 0.5, pitch, 0.1f);
        REQUIRE(tonePower(output[0], 440 * pitch) / power(output[0]) > 0.98);
        requireStereo(output, 0.1f);
    }
    auto mixture = harmonics;
    for (int i = 0; i < int(mixture.size()); ++i) {
        mixture[i] += float(0.002 * std::sin(2 * PI * 6000 * i / RATE));
    }
    std::array<std::vector<double>, 2> references{
        averageSpectrum(mixture, 1024), averageSpectrum(mixture, 4096)};
    for (double speed : {0.1, 0.25, 0.5, 0.75, 1.0, 2.0}) {
        const auto output = render(stretch, mixture, speed, 1, 1);
        for (int scale = 0; scale < int(references.size()); ++scale) {
            const int window = scale == 0 ? 1024 : 4096;
            const auto error = spectralError(references[scale], averageSpectrum(output[0], window));
            REQUIRE(std::isfinite(error.convergence));
            REQUIRE(std::isfinite(error.kl));
            REQUIRE(std::isfinite(error.is));
            std::cout << "Stretch spectrum: speed=" << speed << " window=" << window
                << " convergence=" << error.convergence << " KL=" << error.kl << " IS=" << error.is << '\n';
        }
    }
    Spectrogram spectrogram;
    for (double speed : {0.1, 0.25, 0.5, 0.75, 1.0, 2.0}) {
        render(stretch, harmonics, speed, 1, 1, &spectrogram);
        const double error = consistencyError(spectrogram, false);
        REQUIRE(std::isfinite(error));
        if (speed == 1.0) {
            REQUIRE(error < 0.01);
            REQUIRE(consistencyError(spectrogram, true) > 0.1);
        }
        std::cout << "Stretch consistency: speed=" << speed << " error=" << error << '\n';
    }
    DiagnosticSTFT referenceAnalysis;
    configureDiagnostic(referenceAnalysis, spectrogram);
    spectrogram.frames.clear();
    for (int first = 0; first + WINDOW <= OUTPUT_SAMPLES; first += HOP) {
        referenceAnalysis.writeInput(0, WINDOW, noise.data() + first);
        referenceAnalysis.moveInput(WINDOW);
        referenceAnalysis.analyse();
        spectrogram.frames.emplace_back(referenceAnalysis.spectrum(0), referenceAnalysis.spectrum(0) + referenceAnalysis.bands());
    }
    REQUIRE(consistencyError(spectrogram, false) < 1e-6);
    REQUIRE(consistencyError(spectrogram, true) > 0.1);
    reportOnsets(stretch, int(harmonics.size()));
    const double lowFrequency = RATE * 29.5 / WINDOW;
    const double highFrequency = RATE * 31.5 / WINDOW;
    std::vector<float> cancellations(harmonics.size());
    for (int i = 0; i < int(cancellations.size()); ++i) {
        cancellations[i] = float(0.1 * std::sin(2 * PI * lowFrequency * i / RATE) -
            0.1 * std::sin(2 * PI * highFrequency * i / RATE));
    }
    for (double speed : {0.1, 0.25, 0.5, 0.75, 1.0, 2.0}) {
        const auto output = render(stretch, cancellations, speed, 1, 0.1f, &spectrogram);
        requireStereo(output, 0.1f);
        const double outputPower = power(output[0]);
        REQUIRE(outputPower > 0.004);
        REQUIRE(outputPower < 0.011);
        const double retained = (tonePower(output[0], lowFrequency) + tonePower(output[0], highFrequency)) / outputPower;
        std::cout << "Stretch cancellation: speed=" << speed << " toneFraction=" << retained
            << " power=" << power(output[0]) << " consistency=" << consistencyError(spectrogram, false) << '\n';
    }
    for (float pitch : {0.75f, 1.25f}) {
        const auto output = render(stretch, cancellations, 0.5, pitch, 0.1f);
        requireStereo(output, 0.1f);
        REQUIRE(power(output[0]) > 0.001);
    }
    Stretch splitStretch(0);
    splitStretch.configure(2, RATE, WINDOW, HOP, true);
    const auto splitOutput = render(splitStretch, cancellations, 0.5, 1, 0.1f);
    requireStereo(splitOutput, 0.1f);
    REQUIRE(power(splitOutput[0]) > 0.001);
    const std::vector<float> silence(harmonics.size());
    const auto silentOutput = render(stretch, silence, 0.5, 1, 0.1f);
    for (const auto &channel : silentOutput) {
        REQUIRE(std::all_of(channel.begin(), channel.end(), [](float value) { return value == 0; }));
    }
    render(stretch, noise, 0.5, 1, 1.0f);
    const float noiseTexture = yarg::audio::StretchDiagnostics::textureEnergy(stretch);
    render(stretch, harmonics, 0.5, 1, 1.0f);
    const float toneTexture = yarg::audio::StretchDiagnostics::textureEnergy(stretch);
    std::cout << "Stretch texture: noise=" << noiseTexture << " tone=" << toneTexture << '\n';
    REQUIRE(noiseTexture > 0.5);
    REQUIRE(toneTexture < 0.05);
    std::vector<float> cymbal(harmonics.size());
    std::minstd_rand cymbalRandom(7);
    std::normal_distribution<float> cymbalDistribution(0, 1.0f);
    const float highpass = float(1 - std::exp(-2 * PI * 5000 / RATE));
    float state = 0;
    for (int i = 0; i < int(cymbal.size()); ++i) {
        const double strike = double(i % (RATE / 2)) / (RATE / 2);
        const double envelope = std::exp(-6 * strike);
        const float raw = cymbalDistribution(cymbalRandom);
        state += highpass * (raw - state);
        cymbal[i] = float(0.3 * envelope * (raw - state) +
            0.03 * std::sin(2 * PI * 5220 * i / RATE) + 0.03 * std::sin(2 * PI * 7390 * i / RATE));
    }
    const auto cymbalOutput = render(stretch, cymbal, 0.5, 1, 0.1f);
    double cymbalError = 0;
    double cymbalEnergy = 0;
    for (int i = 0; i < OUTPUT_SAMPLES; ++i) {
        const double difference = cymbalOutput[1][i] - 0.1 * cymbalOutput[0][i];
        cymbalError += difference * difference;
        cymbalEnergy += double(cymbalOutput[0][i]) * cymbalOutput[0][i];
    }
    REQUIRE(cymbalError < cymbalEnergy * 1e-9 + 1e-20);
    const double cymbalPower = power(cymbalOutput[0]);
    const double cymbalModulation = highFrequencyModulation(cymbalOutput[0]);
    const double cymbalReference = highFrequencyModulation(cymbal);
    std::cout << "Stretch cymbal: power=" << cymbalPower
        << " hfModulation=" << cymbalModulation << " reference=" << cymbalReference << '\n';
    REQUIRE(cymbalPower > 0.0015);
    REQUIRE(cymbalPower < 0.05);
    REQUIRE(cymbalModulation < cymbalReference * 1.1);
    const auto first = render(stretch, noise, 0.5, 1, 0.1f);
    render(stretch, vibrato, 0.25, 1, -1);
    REQUIRE(render(stretch, noise, 0.5, 1, 0.1f) == first);
    std::cout << "Stretch quality: harmonics, vibrato, stereo, pitch and reset passed\n";
}
