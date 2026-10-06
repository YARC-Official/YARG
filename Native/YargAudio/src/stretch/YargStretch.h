#ifndef YARG_AUDIO_STRETCH_YARG_STRETCH_H
#define YARG_AUDIO_STRETCH_YARG_STRETCH_H

// YargStretch - time stretcher: changes playback speed without changing pitch.
//
// Based on Signalsmith Stretch 1.3.2 by Geraint Luff (Signalsmith Audio):
//   https://github.com/Signalsmith-Audio/signalsmith-stretch
//   Licensed under the MIT License (see third_party/signalsmith-stretch-LICENSE.txt).
//
// YARG changes on top of upstream:
//   - Transient detection with phase snapping, so drum hits stay sharp.
//   - Split low/high handling that keeps bass solid at extreme slowdowns.
//   - Complex phase integration with temporal and neighboring-bin constraints.
//   - Reassigned STFT gradients for sharper pitch/timing estimates.
//
// One output block goes through process() in this order:
//   1. Map the block to its input position for the current rate.
//   2. STFT analysis of the new input, plus gradient spectra for reassignment.
//   3. Complex phase integration and source-phase recovery at unity speed.
//   5. NoiseMorph texture resynthesis (stereo only; skipped for pitch shifts and transients).
//   6. Inverse-STFT overlap-add into the output block.
//
// Lifecycle: configure() once, seek() to prime after a jump, process() per block,
// flush() at end of input. inputLatency is the required priming; outputLatency is
// the output lag.

#include <cstring>
#include <cmath>
#include "signalsmith-linear/stft.h"
#include "stretch/NoiseMorph.h"
#include "stretch/EnvelopeEq.h"

#include <vector>
#include <array>
#include <algorithm>
#include <functional>
#include <random>
#include <limits>
#include <type_traits>

namespace yarg::audio {

namespace _impl {
	// Fast libm approximations used by the DSP core.
	template<bool conjugateSecond=false, typename V>
	static std::complex<V> mul(const std::complex<V> &a, const std::complex<V> &b) {
		return conjugateSecond ? std::complex<V>{
			b.real()*a.real() + b.imag()*a.imag(),
				b.real()*a.imag() - b.imag()*a.real()
		} : std::complex<V>{
			a.real()*b.real() - a.imag()*b.imag(),
			a.real()*b.imag() + a.imag()*b.real()
		};
	}
	template<typename V>
	static V norm(const std::complex<V> &a) {
		V r = a.real(), i = a.imag();
		return r*r + i*i;
	}
	template<typename V>
	static std::complex<V> fastPolar(V r, V angle) {
#if defined(__APPLE__)
		float s, c;
		__sincosf(static_cast<float>(angle), &s, &c);
		return std::complex<V>(static_cast<V>(c)*r, static_cast<V>(s)*r);
#elif defined(__GNUC__) || defined(__clang__)
		float s, c;
		sincosf(static_cast<float>(angle), &s, &c);
		return std::complex<V>(static_cast<V>(c)*r, static_cast<V>(s)*r);
#else
		return std::polar(r, angle);
#endif
	}
	template<typename V>
	static V fastAtan2(V y, V x) {
		V ax = std::abs(x);
		V ay = std::abs(y);
		V mn = std::min(ax, ay);
		V mx = std::max(ax, ay);
		if (mx <= V(1e-12)) {
			return V(0);
		}
		V c = mn / mx;
		V s = c * c;
		V p = (((V(-0.0464964749) * s + V(0.15931422)) * s - V(0.327622764)) * s * c) + c;
		if (ay > ax) {
			p = V(1.5707963267948966) - p;
		}
		if (x < V(0)) {
			p = V(3.1415926535897932) - p;
		}
		if (y < V(0)) {
			p = -p;
		}
		return p;
	}
}

template<typename Sample=float, class RandomEngine=void>
struct YargStretch {
	static constexpr size_t version[3] = {1, 3, 2};

	YargStretch() : randomEngine(std::random_device{}()) {}
	YargStretch(long seed) : randomEngine(seed) {}
		
	// The difference between the internal position (centre of a block) and the input samples you're supplying
	int inputLatency() const {
		return int(stft.analysisLatency());
	}
	int outputLatency() const {
		return int(stft.synthesisLatency() + _splitComputation*stft.defaultInterval());
	}
	
	void setTransientFrequency(Sample minimumFrequency) {
		transientMinFreq_ = minimumFrequency;
	}

	void reset(long seed) {
		randomEngine.seed(seed);
		reset();
	}

	void reset() {
		stft.reset(0.1);
		noiseMorph.clearHistory();
		envelopeEq.reset();
		stashedInput = stft.input;
		stashedOutput = stft.output;
		
		prevInputOffset = -1;
		_channelBands.assign(_channelBands.size(), Band());
		channelPredictions.assign(channelPredictions.size(), Prediction());
		silenceCounter = 0;
		didSeek = false;
		blockProcess = {};
		smoothTimeFactor_ = 1;
		sourcePhaseWeight_ = 0;
		freqEstimateWeighted = freqEstimateWeight = 0;
		transientSamples_ = int(stft.blockSamples());
		transientCooldown_ = 0;
		transientState_ = TransientState::IDLE;
		transientInputSamples_ = 0;
		transientBg_[0] = transientBg_[1] = transientBg_[2] = 0;
		transientBgReady_ = false;
		for (int b = 0; b < bands; ++b) {
			phaseCurrentEnergy[b] = 0;
			phasePreviousEnergy[b] = 0;
		}
		std::fill(transientHold_.begin(), transientHold_.end(), 0);
		tonalReferenceHistory_ = false;
		std::fill(phaseTemporalConfidence.begin(), phaseTemporalConfidence.end(), Sample(0));
	}

	// One-time setup: channel count, sample rate, block and hop sizes.
	void configure(int nChannels, Sample sampleRate, int blockSamples, int intervalSamples, bool splitComputation=false) {
		_splitComputation = splitComputation;
		channels = nChannels;
		sampleRate_ = sampleRate;
		stft.configure(channels, channels, blockSamples, intervalSamples + 1);
		stft.setInterval(intervalSamples, stft.kaiser);
		const Sample sigma = Sample(blockSamples)/GAUSSIAN_WIDTH;
		magnitudeGradientScale_ = Sample(stft.fftSamples())/(Sample(8*M_PI)*sigma*sigma);
		for (int i = 0; i < blockSamples; ++i) {
			const Sample position = (Sample(i) - Sample(stft.analysisOffset()))/sigma;
			const Sample window = std::exp(Sample(-0.5)*position*position);
			stft.analysisWindow()[i] = window;
			stft.synthesisWindow()[i] = window;
		}
		stft.reset(Sample(0.1));
		const bool useGrains = channels <= 2 && !splitComputation &&
			intervalSamples * 3 <= blockSamples;
		noiseMorph.configure(stft, channels, sampleRate, useGrains);
		if (useGrains) {
			envelopeEq.configure(stft, channels, sampleRate);
		}
		logPower.resize(stft.bands());
		timeWindow.resize(blockSamples);
		const auto *window = stft.analysisWindow();
		Sample rampPower = 0;
		Sample rampMoment = 0;
		for (int i = 0; i < blockSamples; ++i) {
			timeWindow[i] = window[i]*(i - int(stft.analysisOffset()));
			Sample ramp = window[i]*Sample(i);
			Sample power = ramp*ramp;
			rampPower += power;
			rampMoment += power*Sample(i - int(stft.analysisOffset()));
		}
		transientCenter_ = rampMoment/rampPower;
		transientSamples_ = int(stft.blockSamples());
		transientCooldown_ = 0;
		transientState_ = TransientState::IDLE;
		transientInputSamples_ = 0;
		transientBg_[0] = transientBg_[1] = transientBg_[2] = 0;
		transientBgReady_ = false;
		stashedInput = stft.input;
		stashedOutput = stft.output;

		bands = int(stft.bands());
		_channelBands.assign(bands*channels, Band());
		
		peaks.reserve(bands/2);
		energy.resize(bands);
		smoothedEnergy.resize(bands);
		outputMap.resize(bands);
		channelPredictions.assign(channels*bands, Prediction());
		phaseChannels.resize(bands);
		phaseSignificant.resize(bands);
		phaseReset.resize(bands);
		phaseReferences.assign(bands, 0);
		phaseReferenceFrequency.assign(bands, Sample(0));
		phaseDiagonal.resize(bands);
		phaseUpper.resize(bands);
		phaseSolution.resize(bands);
		phaseCurrentEnergy.resize(bands);
		phasePreviousEnergy.resize(bands);
		phaseAnchorWeight.assign(bands, Sample(1));
		phaseTemporalConfidence.assign(bands, Sample(0));
		tonalReferenceHistory_ = false;
		transientHold_.assign(bands, 0);

		for (int b = 0; b < bands; ++b) {
			phaseCurrentEnergy[b] = 0;
		}

		blockProcess = {};
		formantMetric.resize(bands + 2);
		sourcePhaseWeight_ = 0;

		tmpProcessBuffer.resize(blockSamples + intervalSamples);
		tmpPreRollBuffer.resize(outputLatency()*channels);
	}
	// For querying the existing config
	int blockSamples() const {
		return int(stft.blockSamples());
	}
	int intervalSamples() const {
		return int(stft.defaultInterval());
	}
	bool splitComputation() const {
		return _splitComputation;
	}

	/// Frequency multiplier, and optional tonality limit (as multiple of sample-rate)
	void setTransposeFactor(Sample multiplier, Sample tonalityLimit=0) {
		freqMultiplier = multiplier;
		if (tonalityLimit > 0) {
			freqTonalityLimit = tonalityLimit/std::sqrt(multiplier); // compromise between input and output limits
		} else {
			freqTonalityLimit = 1;
		}
		customFreqMap = nullptr;
	}
	
	// Reposition without interrupting output; hands over already-read input.
	// Provide previous input ("pre-roll") to smoothly change the input location without interrupting the output.  This doesn't do any calculation, just copies intput to a buffer.
	// You should ideally feed it `seekLength()` frames of input, unless it's directly after a `.reset()` (in which case `.outputSeek()` might be a better choice)
	template<class Inputs>
	void seek(Inputs &&inputs, int inputSamples, double playbackRate) {
		noiseMorph.clearHistory();
		envelopeEq.reset();
		tonalReferenceHistory_ = false;
		std::fill(phaseTemporalConfidence.begin(), phaseTemporalConfidence.end(), Sample(0));
		transientState_ = TransientState::IDLE;
		transientInputSamples_ = 0;
		std::fill(transientHold_.begin(), transientHold_.end(), 0);
		tmpProcessBuffer.resize(0);
		tmpProcessBuffer.resize(stft.blockSamples() + stft.defaultInterval());

		int startIndex = std::max<int>(0, inputSamples - int(tmpProcessBuffer.size())); // start position in input
		int padStart = int(tmpProcessBuffer.size() + startIndex) - inputSamples; // start position in tmpProcessBuffer

		Sample totalEnergy = 0;
		for (int c = 0; c < channels; ++c) {
			auto &&inputChannel = inputs[c];
			for (int i = startIndex; i < inputSamples; ++i) {
				Sample s = inputChannel[i];
				totalEnergy += s*s;
				tmpProcessBuffer[i - startIndex + padStart] = s;
			}
			
			stft.writeInput(c, tmpProcessBuffer.size(), tmpProcessBuffer.data());
		}
		stft.moveInput(tmpProcessBuffer.size());
		if (totalEnergy >= noiseFloor) {
			silenceCounter = 0;
			silenceFirst = true;
		}
		didSeek = true;
		seekTimeFactor = (playbackRate*stft.defaultInterval() > 1) ? 1/playbackRate : stft.defaultInterval();
	}
	int seekLength() const {
		return int(stft.blockSamples() + stft.defaultInterval());
	}
	
	// Reposition from silence and pre-render output so playback starts aligned.
	// Moves the input position *and* pre-calculates some output, so that the next samples returned from `.process()` are aligned to the beginning of the sample.
	// The time-stretch rate is inferred from `inputLength`, so use `.outputSeekLength()` to get a correct value for that.
	template<class Inputs>
	void outputSeek(Inputs &&inputs, int inputLength) {
		// TODO: add fade-out parameter to avoid clicks, instead of doing a full reset
		reset();
		// Assume we've been handed enough surplus input to produce `outputLatency()` samples of pre-roll
		int surplusInput = std::max<int>(inputLength - inputLatency(), 0);
		Sample playbackRate = surplusInput/Sample(outputLatency());

		// Move the input position to the start of the sound
		int seekSamples = inputLength - surplusInput;
		seek(inputs, seekSamples, playbackRate);
		
		tmpPreRollBuffer.resize(outputLatency()*channels);
		struct BufferOutput {
			Sample *samples;
			int length;
			
			Sample * operator[](int c) {
				return samples + c*length;
			}
		} preRollOutput{tmpPreRollBuffer.data(), outputLatency()};
		
		// Use the surplus input to produce pre-roll output
		OffsetIO<Inputs> offsetInput{inputs, seekSamples};
		process(offsetInput, surplusInput, preRollOutput, preRollOutput.length);
		
		// put the thing down, flip it and reverse it
		for (auto &v : tmpPreRollBuffer) v = -v;
		for (int c = 0; c < channels; ++c) {
			std::reverse(preRollOutput[c], preRollOutput[c] + preRollOutput.length);
			stft.addOutput(c, preRollOutput.length, preRollOutput[c]);
		}
	}
	int outputSeekLength(Sample playbackRate) const {
		return inputLatency() + playbackRate*outputLatency();
	}

	// Main work: render outputSamples from inputSamples.
	template<class Inputs, class Outputs>
	void process(Inputs &&inputs, int inputSamples, Outputs &&outputs, int outputSamples) {
		int prevCopiedInput = 0;
		auto copyInput = [&](int toIndex){

			int length = std::min<int>(int(stft.blockSamples() + stft.defaultInterval()), toIndex - prevCopiedInput);
			tmpProcessBuffer.resize(length);
			int offset = toIndex - length;
			for (int c = 0; c < channels; ++c) {
				auto &&inputBuffer = inputs[c];
				for (int i = 0; i < length; ++i) {
					tmpProcessBuffer[i] = inputBuffer[i + offset];
				}
				stft.writeInput(c, length, tmpProcessBuffer.data());
			}
			stft.moveInput(length);
			prevCopiedInput = toIndex;
		};

		Sample totalEnergy = 0;
		for (int c = 0; c < channels; ++c) {
			auto &&inputChannel = inputs[c];
			for (int i = 0; i < inputSamples; ++i) {
				Sample s = inputChannel[i];
				totalEnergy += s*s;
			}
		}

		if (totalEnergy < noiseFloor) {
			if (silenceCounter >= 2*stft.blockSamples()) {
				if (silenceFirst) { // first block of silence processing
					silenceFirst = false;
					if (noiseMorph.usesGrains()) {
						noiseMorph.clearHistory();
						envelopeEq.reset();
					}
					//stft.reset();
					blockProcess = {};
					tonalReferenceHistory_ = false;
					std::fill(phaseTemporalConfidence.begin(), phaseTemporalConfidence.end(), Sample(0));
					transientState_ = TransientState::IDLE;
					transientInputSamples_ = 0;
					std::fill(transientHold_.begin(), transientHold_.end(), 0);
					for (auto &b : _channelBands) {
						b.input = b.prevInput = b.output = b.prevOutput = 0;
						b.inputEnergy = 0;
					}
				}
			
				if (inputSamples > 0) {
					// copy from the input, wrapping around if needed
					for (int outputIndex = 0; outputIndex < outputSamples; ++outputIndex) {
						int inputIndex = outputIndex%inputSamples;
						for (int c = 0; c < channels; ++c) {
							outputs[c][outputIndex] = inputs[c][inputIndex];
						}
					}
				} else {
					for (int c = 0; c < channels; ++c) {
						auto &&outputChannel = outputs[c];
						for (int outputIndex = 0; outputIndex < outputSamples; ++outputIndex) {
							outputChannel[outputIndex] = 0;
						}
					}
				}

				// Store input in history buffer
				copyInput(inputSamples);
				if (inputSamples > 0 && outputSamples > 0) {
					trackTimeFactor(Sample(outputSamples)/Sample(inputSamples));
				}
				return;
			} else {
				silenceCounter += inputSamples;
			}
		} else {
			silenceCounter = 0;
			silenceFirst = true;
		}
		
		for (int outputIndex = 0; outputIndex < outputSamples; ++outputIndex) {
			bool newBlock = blockProcess.samplesSinceLast >= stft.defaultInterval();
			if (newBlock) {
				blockProcess.step = 0;
				blockProcess.steps = 0; // how many processing steps this block will have
				blockProcess.samplesSinceLast = 0;
				
				// Time to process a spectrum!  Where should it come from in the input?
				int inputOffset = static_cast<int>(std::round(outputIndex*Sample(inputSamples)/outputSamples));
				int inputInterval = inputOffset - prevInputOffset;
				int intervalStep = int(stft.defaultInterval());
				transientSamples_ = std::max(0, transientSamples_ - intervalStep);
				transientCooldown_ = std::max(0, transientCooldown_ - intervalStep);
				if (transientState_ != TransientState::IDLE) {
					transientInputSamples_ = std::max(0, transientInputSamples_ - std::max(0, inputInterval));
					if (transientInputSamples_ == 0) {
						transientState_ = TransientState::IDLE;
						std::fill(transientHold_.begin(), transientHold_.end(), 0);
					}
				}
				prevInputOffset = inputOffset;
				
				copyInput(inputOffset);
				stashedInput = stft.input; // save the input state, since that's what we'll analyse later
				if (_splitComputation) {
					stashedOutput = stft.output; // save the current output, and read from it
					stft.moveOutput(stft.defaultInterval()); // the actual input jumps forward in time by one interval, ready for the synthesis
				}

				blockProcess.newSpectrum = didSeek || (inputInterval > 0);
				blockProcess.sourceInterval = inputInterval;
				blockProcess.mappedFrequencies = customFreqMap || freqMultiplier != 1;
				if (blockProcess.newSpectrum) {
					// make sure the previous input is the correct distance in the past (give or take 1 sample)
					blockProcess.reanalysePrev = didSeek || std::abs(inputInterval - int(stft.defaultInterval())) > 1;
					blockProcess.phaseInterval = blockProcess.reanalysePrev ? int(stft.defaultInterval()) : inputInterval;
					if (blockProcess.reanalysePrev) blockProcess.steps += stft.analyseSteps() + 1;

					// analyse a new input
					blockProcess.steps += 2*(stft.analyseSteps() + 1);
				}
				
				blockProcess.processFormants = formantMultiplier != 1 || (formantCompensation && blockProcess.mappedFrequencies);

				blockProcess.timeFactor = didSeek ? seekTimeFactor : stft.defaultInterval()/static_cast<Sample>(std::max(1, inputInterval));
				const bool sourcePhase = blockProcess.timeFactor == Sample(1) &&
					!blockProcess.mappedFrequencies && !blockProcess.processFormants;
				if (!sourcePhase) {
					sourcePhaseWeight_ = 0;
				} else if (didSeek) {
					sourcePhaseWeight_ = 1;
				} else {
					sourcePhaseWeight_ = std::min(Sample(1), sourcePhaseWeight_ +
						Sample(stft.defaultInterval()) / stft.blockSamples());
				}
				if (blockProcess.newSpectrum) {
					trackTimeFactor(blockProcess.timeFactor);
				}
				if (didSeek) {
					for (auto &bin : _channelBands) {
						bin.prevOutput = 0;
					}
				}
				didSeek = false;

				updateProcessSpectrumSteps();
				blockProcess.steps += processSpectrumSteps;

				blockProcess.steps += stft.synthesiseSteps() + 1;
			}
			
			size_t processToStep = newBlock ? blockProcess.steps : 0;
			if (_splitComputation) {
				Sample processRatio = Sample(blockProcess.samplesSinceLast + 1)/stft.defaultInterval();
				processToStep = std::min(blockProcess.steps, static_cast<size_t>((blockProcess.steps + 0.999f)*processRatio));
			}
			
			while (blockProcess.step < processToStep) {
				size_t step = blockProcess.step++;
				if (blockProcess.newSpectrum) {
					if (blockProcess.reanalysePrev) {
						// analyse past input
						if (step < stft.analyseSteps()) {
							stashedInput.swap(stft.input);
							stft.analyseStep(step, stft.defaultInterval());
							stashedInput.swap(stft.input);
							continue;
						}
						step -= stft.analyseSteps();
						if (step < 1) {
							// Copy previous analysis to our band objects
							for (int c = 0; c < channels; ++c) {
								auto channelBands = bandsForChannel(c);
								auto *spectrumBands = stft.spectrum(c);
								for (int b = 0; b < bands; ++b) {
									channelBands[b].prevInput = spectrumBands[b];
								}
							}
							continue;
						}
						step -= 1;
					}

					// Analyse latest (stashed) input
					if (step < stft.analyseSteps()) {
						stashedInput.swap(stft.input);
						stft.analyseStep(step);
						stashedInput.swap(stft.input);
						continue;
					}
					step -= stft.analyseSteps();
					if (step < 1) {
						// Copy analysed spectrum into our band objects
						const Sample minimumLogRatio = std::log(GRADIENT_MIN_RELATIVE_POWER);
						for (int c = 0; c < channels; ++c) {
							auto channelBands = bandsForChannel(c);
							auto *spectrumBands = stft.spectrum(c);
							for (int b = 0; b < bands; ++b) {
								channelBands[b].input = spectrumBands[b];
								logPower[b] = std::log(_impl::norm(spectrumBands[b]) + tinyFloor);
							}
							for (int b = 0; b < bands; ++b) {
								const Sample left = logPower[b > 0 ? b - 1 : b];
								const Sample right = logPower[b + 1 < bands ? b + 1 : b];
								Sample gradient = (right - left)*magnitudeGradientScale_;
								if (std::min(left, right) < logPower[b] + minimumLogRatio) {
									const Sample centerPower = _impl::norm(channelBands[b].input);
									const Sample threshold = centerPower*GRADIENT_MIN_RELATIVE_POWER + tinyFloor;
									const Sample previousPower = _impl::norm(channelBands[b].prevInput);
									if (previousPower > threshold) {
										const Complex progression = _impl::mul<true>(channelBands[b].input, channelBands[b].prevInput);
										const Sample interval = Sample(blockProcess.phaseInterval);
										Sample phase = std::atan2(progression.imag(), progression.real()) -
											Sample(2*M_PI)*bandToFreq(Sample(b))*interval;
										phase -= Sample(2*M_PI)*std::round(phase/Sample(2*M_PI));
										const Sample leftPower = _impl::norm(channelBands[b > 0 ? b - 1 : b].input);
										const Sample rightPower = _impl::norm(channelBands[b + 1 < bands ? b + 1 : b].input);
										const Sample confidence = std::min(centerPower, previousPower)/std::max(centerPower, previousPower);
										const Sample reliability = std::min(Sample(1), std::min(leftPower, rightPower)/threshold);
										gradient += (phase/interval - gradient)*(Sample(1) - reliability)*confidence;
									}
								}
								channelBands[b].derivative = _impl::mul(channelBands[b].input, Complex{Sample(0), -gradient});
							}
						}
						continue;
					}
					step -= 1;

					const size_t gradientSteps = stft.analyseSteps() + 1;
					if (step < gradientSteps) {
						if (step < stft.analyseSteps()) {
							auto &window = timeWindow;
							std::swap_ranges(window.begin(), window.end(), stft.analysisWindow());
							stashedInput.swap(stft.input);
							stft.analyseStep(step);
							stashedInput.swap(stft.input);
							std::swap_ranges(window.begin(), window.end(), stft.analysisWindow());
						} else {
							for (int c = 0; c < channels; ++c) {
								auto *bins = bandsForChannel(c);
								auto *spectrum = stft.spectrum(c);
								for (int b = 0; b < bands; ++b) {
									bins[b].timed = spectrum[b];
								}
							}
						}
						continue;
					}
					step -= gradientSteps;
				}

				if (step < processSpectrumSteps) {
					processSpectrum(step);
					continue;
				}
				step -= processSpectrumSteps;

				if (step < 1) {
					// Copy band objects into spectrum
					for (int c = 0; c < channels; ++c) {
						auto channelBands = bandsForChannel(c);
						auto *spectrumBands = stft.spectrum(c);
						for (int b = 0; b < bands; ++b) {
							spectrumBands[b] = channelBands[b].output;
							if (blockProcess.newSpectrum) {
								channelBands[b].prevInput = channelBands[b].input; // .input -> .prevInput
							}
							channelBands[b].prevOutput = channelBands[b].output;
						}
					}
					if (noiseMorph.usesGrains()) {
						Sample strength = std::clamp(NOISE_MORPH_STRENGTH *
							(smoothTimeFactor_ - Sample(1)) / smoothTimeFactor_, Sample(0), Sample(1));
						if (blockProcess.mappedFrequencies || blockProcess.processFormants || transientSamples_ > 0) {
							strength = 0;
						}
						noiseMorph.apply(stft, [&](int c, int b) { return bandsForChannel(c)[b].input; },
							strength, transientMinFreq_);
						envelopeEq.setReference([&](int c, int b) { return bandsForChannel(c)[b].input; },
							!blockProcess.mappedFrequencies && !blockProcess.processFormants && smoothTimeFactor_ > Sample(1.01));
					}
					continue;
				}
				step -= 1;
				
				if (step < stft.synthesiseSteps()) {
					stft.synthesiseStep(step);
					continue;
				}
			}

			++blockProcess.samplesSinceLast;
			if (_splitComputation) stashedOutput.swap(stft.output);
			for (int c = 0; c < channels; ++c) {
				auto &&outputChannel = outputs[c];
				Sample v = 0;
				stft.readOutput(c, 1, &v);
				if (noiseMorph.usesGrains()) {
					v += noiseMorph.readGrain(c);
					v = envelopeEq.filter(c, v);
				}
				outputChannel[outputIndex] = v;
			}
			if (noiseMorph.usesGrains()) {
				noiseMorph.advanceGrain();
				envelopeEq.advance();
			}
			stft.moveOutput(1);
			if (_splitComputation) stashedOutput.swap(stft.output);
		}
		
		copyInput(inputSamples);
		prevInputOffset -= inputSamples;
	}

	// Drain remaining output once input is exhausted.
	// Read the remaining output, providing no further input.  If `outputSamples` is more than one interval, it will compute additional blocks assuming a zero-valued input
	template<class Outputs>
	void flush(Outputs &&outputs, int outputSamples, Sample playbackRate=0) {
		struct Zeros {
			struct Channel {
				Sample operator[](int) {
					return 0;
				}
			};
			Channel operator[](int) {
				return {};
			}
		} zeros;
		// If we're asked for more than an interval of extra output, then zero-pad the input
		int outputBlock = std::max<int>(0, outputSamples - stft.defaultInterval());
		if (outputBlock > 0) process(zeros, outputBlock*playbackRate, outputs, outputBlock);

		int tailSamples = outputSamples - outputBlock; // at most one interval
		tmpProcessBuffer.resize(tailSamples);
		stft.finishOutput(1);
		if (noiseMorph.usesGrains()) {
			for (int i = 0; i < tailSamples; ++i) {
				for (int c = 0; c < channels; ++c) {
					const Sample value = noiseMorph.readGrain(c);
					stft.addOutput(c, i, 1, &value);
				}
				noiseMorph.advanceGrain();
			}
		}
		for (int c = 0; c < channels; ++c) {
			stft.readOutput(c, tailSamples, tmpProcessBuffer.data());
			auto &&outputChannel = outputs[c];
			for (int i = 0; i < tailSamples; ++i) {
				outputChannel[outputBlock + i] = tmpProcessBuffer[i];
			}
			stft.readOutput(c, tailSamples, tailSamples, tmpProcessBuffer.data());
			for (int i = 0; i < tailSamples; ++i) {
				outputChannel[outputBlock + tailSamples - 1 - i] -= tmpProcessBuffer[i];
			}
		}
		if (noiseMorph.usesGrains()) {
			for (int i = 0; i < tailSamples; ++i) {
				for (int c = 0; c < channels; ++c) {
					outputs[c][outputBlock + i] = envelopeEq.filter(c, outputs[c][outputBlock + i]);
				}
				envelopeEq.advance(false);
			}
		}
		stft.reset(0.1f);
		sourcePhaseWeight_ = 0;
		noiseMorph.clearHistory();
		envelopeEq.reset();
		tonalReferenceHistory_ = false;
		std::fill(phaseTemporalConfidence.begin(), phaseTemporalConfidence.end(), Sample(0));
		transientState_ = TransientState::IDLE;
		transientInputSamples_ = 0;
		std::fill(transientHold_.begin(), transientHold_.end(), 0);
		// Reset the phase-vocoder stuff, so the next block gets a fresh start
		for (int c = 0; c < channels; ++c) {
			auto channelBands = bandsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				channelBands[b].prevInput = channelBands[b].output = channelBands[b].prevOutput = 0;
			}
		}
	}

	// Process a complete audio buffer all in one go
	template<class Inputs, class Outputs>
	bool exact(Inputs &&inputs, int inputSamples, Outputs &&outputs, int outputSamples) {
		Sample playbackRate = inputSamples/Sample(outputSamples);
		auto seekLength = outputSeekLength(playbackRate);
		if (inputSamples < seekLength) {
			// to short for this - zero the output just to be polite
			for (int c = 0; c < channels; ++c) {
				auto &&channel = outputs[c];
				for (int i = 0; i < outputSamples; ++i) {
					channel[i] = 0;
				}
			}
			return false;
		}

		outputSeek(inputs, seekLength);

		int outputIndex = outputSamples - seekLength/playbackRate;
		OffsetIO<Inputs> offsetInput{inputs, seekLength};
		process(offsetInput, inputSamples - seekLength, outputs, outputIndex);
		
		OffsetIO<Outputs> offsetOutput{outputs, outputIndex};
		flush(offsetOutput, outputSamples - outputIndex, playbackRate);
		return true;
	}

private:
	bool _splitComputation = false;
	struct {
		size_t samplesSinceLast = std::numeric_limits<size_t>::max();
		size_t steps = 0;
		size_t step = 0;
		
		bool newSpectrum = false;
		bool reanalysePrev = false;
		bool mappedFrequencies = false;
		bool processFormants = false;
		int phaseInterval = 0;
		int sourceInterval = 0;
		Sample timeFactor;
	} blockProcess;

	using Complex = std::complex<Sample>;
	static constexpr Sample noiseFloor{Sample(1e-15)};
	static constexpr Sample tinyFloor{Sample(1e-30)};
	static constexpr Sample maxCleanLow{6}; // YARG local patch: lows stay clean (upstream maxCleanStretch 2)
	static constexpr Sample GRADIENT_TWIST_MAX{Sample(1)};
	static constexpr Sample VERTICAL_AGREEMENT_MIN{Sample(0.5)};
	static constexpr Sample splitFreq{Sample(0.11)}; // YARG local patch: ~4.8kHz @44.1k, normalized
	static constexpr Sample TRANSIENT_MIN_STRETCH{Sample(1.1)};
	static constexpr Sample TRANSIENT_MIN_POWER{Sample(1e-6)};
	static constexpr Sample TRANSIENT_POWER_FRACTION{Sample(0.003)};
	static constexpr Sample TRANSIENT_FLUX_RATIO{Sample(0.45)};
	static constexpr Sample TRANSIENT_ONSET_FLUX_RATIO{Sample(0.25)};
	static constexpr Sample TRANSIENT_BG_RATIO{Sample(3)};
	static constexpr Sample TRANSIENT_MEDIUM_ONSET_SCALE{Sample(0.6)};
	static constexpr Sample TRANSIENT_MEDIUM_BG_SCALE{Sample(0.6)};
	static constexpr Sample TRANSIENT_BASS_POWER_FRACTION{Sample(0.01)};
	static constexpr int TRANSIENT_NEIGHBOR_BINS = 3;
	static constexpr Sample TRANSIENT_READY_POWER_FRACTION = Sample(0.5);
	Sample transientMinFreq_{Sample(0.03)};
	int transientSamples_ = 0;
	int transientCooldown_ = 0;
	enum class TransientState { IDLE, COLLECTING, RESET, COOLDOWN };
	TransientState transientState_ = TransientState::IDLE;
	int transientInputSamples_ = 0;
	Sample transientCenter_ = 0;
	static constexpr Sample NOISE_MORPH_STRENGTH = Sample(2);

	Sample transientBg_[3] = {};
	bool transientBgReady_ = false;
	size_t silenceCounter = 0;
	bool silenceFirst = true;

	Sample freqMultiplier = 1, freqTonalityLimit = 0.5;
	std::function<Sample(Sample)> customFreqMap = nullptr;
	
	bool formantCompensation = false; // compensate for pitch/freq change
	Sample formantMultiplier = 1, invFormantMultiplier = 1;

	using STFT = signalsmith::linear::DynamicSTFT<Sample, false, true>;
	STFT stft;
	NoiseMorph<Sample> noiseMorph;
	EnvelopeEq<Sample> envelopeEq;
	typename STFT::Input stashedInput;
	typename STFT::Output stashedOutput;
	
	std::vector<Sample> tmpProcessBuffer, tmpPreRollBuffer;
	static constexpr Sample GAUSSIAN_WIDTH{Sample(8)};
	static constexpr Sample GRADIENT_MIN_RELATIVE_POWER = Sample(1e-2);
	Sample magnitudeGradientScale_ = 0;
	std::vector<Sample> logPower, timeWindow;

	int channels = 0, bands = 0;
	Sample sampleRate_ = 0;
	int prevInputOffset = -1;
	bool didSeek = false;
	Sample seekTimeFactor = 1;

	Sample bandToFreq(Sample b) const {
		return stft.binToFreq(b);
	}
	Sample freqToBand(Sample f) const {
		return stft.freqToBin(f);
	}
	Sample bandToHz(Sample b) const {
		return bandToFreq(b)*sampleRate_;
	}
	Sample hzToBand(Sample hz) const {
		return freqToBand(hz/sampleRate_);
	}
	
	struct Band {
		Complex input, prevInput{0};
		Complex output{0}, prevOutput{0};
		Complex derivative{0}, timed{0};
		Sample inputEnergy;
	};
	std::vector<Band> _channelBands;
	Band * bandsForChannel(int channel) {
		return _channelBands.data() + channel*bands;
	}
	template<Complex Band::*member>
	Complex getBand(int channel, int index) {
		if (index < 0 || index >= bands) return 0;
		return _channelBands[index + channel*bands].*member;
	}
	template<Complex Band::*member>
	Complex getFractional(int channel, int lowIndex, Sample fractional) {
		Complex low = getBand<member>(channel, lowIndex);
		Complex high = getBand<member>(channel, lowIndex + 1);
		return low + (high - low)*fractional;
	}
	template<Complex Band::*member>
	Complex getFractional(int channel, Sample inputIndex) {
		int lowIndex = static_cast<int>(std::floor(inputIndex));
		Sample fracIndex = inputIndex - lowIndex;
		return getFractional<member>(channel, lowIndex, fracIndex);
	}
	template<Sample Band::*member>
	Sample getBand(int channel, int index) {
		if (index < 0 || index >= bands) return 0;
		return _channelBands[index + channel*bands].*member;
	}
	template<Sample Band::*member>
	Sample getFractional(int channel, int lowIndex, Sample fractional) {
		Sample low = getBand<member>(channel, lowIndex);
		Sample high = getBand<member>(channel, lowIndex + 1);
		return low + (high - low)*fractional;
	}
	template<Sample Band::*member>
	Sample getFractional(int channel, Sample inputIndex) {
		int lowIndex = std::floor(inputIndex);
		Sample fracIndex = inputIndex - lowIndex;
		return getFractional<member>(channel, lowIndex, fracIndex);
	}

	struct Peak {
		Sample input, output;
	};
	std::vector<Peak> peaks;
	std::vector<Sample> energy, smoothedEnergy;
	struct PitchMapPoint {
		Sample inputBin, freqGrad;
	};
	std::vector<PitchMapPoint> outputMap;
	
	struct Prediction {
		Sample energy = 0;
		Sample timeGradient = 0;
		Sample timeAdvance = 0;
		Sample frequencyGradient = 0;
		Complex input;

		Complex makeOutput(Complex phase, Sample scale = Sample(1)) {
			Sample phaseNorm = _impl::norm(phase);
			if (phaseNorm <= Sample(0)) {
				phase = input; // prediction is too weak, fall back to the input
				phaseNorm = _impl::norm(input) + tinyFloor;
			}
			return phase*(std::sqrt(energy/phaseNorm)*scale);
		}
	};
	std::vector<Prediction> channelPredictions;
	std::vector<int> phaseChannels;
	std::vector<char> phaseSignificant, phaseReset, phaseReferences;
	std::vector<Sample> phaseReferenceFrequency;
	std::vector<double> phaseDiagonal;
	std::vector<std::complex<double>> phaseUpper, phaseSolution;
	std::vector<Sample> phaseCurrentEnergy;
	std::vector<Sample> phasePreviousEnergy;
	std::vector<Sample> phaseAnchorWeight;
	std::vector<Sample> phaseTemporalConfidence;
	bool tonalReferenceHistory_ = false;
	std::vector<char> transientHold_;
	static constexpr double PHASE_FREQUENCY_WEIGHT = 16;
	static constexpr double PHASE_CONFLICT_WEIGHT = 2;
	static constexpr Sample PHASE_ENERGY_TOLERANCE = Sample(1e-12);
	static constexpr Sample PEAK_SHELTER_RATIO = Sample(4);
	static constexpr Sample TONAL_REFERENCE_PRIORITY = Sample(1.5);
	static constexpr Sample TONAL_REFERENCE_PROMINENCE = Sample(2.5);
	static constexpr Sample TONAL_REFERENCE_POWER_RATIO = Sample(0.5);
	static constexpr Sample TONAL_REFERENCE_MAX_BIN_DRIFT = Sample(0.5);
	static constexpr Sample TONAL_REFERENCE_MIN_POWER = Sample(0.001);
	static constexpr Sample TONAL_PHASE_MAX_HZ = Sample(2500);
	static constexpr Sample TONAL_PHASE_MAX_WEIGHT = Sample(0.5);
	static constexpr Sample TONAL_PHASE_ERROR_START = Sample(0.025);
	static constexpr Sample TONAL_PHASE_ERROR_RANGE = Sample(0.25);
	static constexpr Sample TONAL_PHASE_MAX_CORRECTION = Sample(0.12);
	static constexpr Sample TRANSIENT_SNAP_MIN_DISAGREEMENT = Sample(0.5);
	static constexpr int TONAL_AGREEMENT_RADIUS = 2;
	static constexpr Sample TONAL_AGREEMENT_MIN_POWER = Sample(0.1);
	static constexpr Sample TONAL_AGREEMENT_TOLERANCE = Sample(0.1);
	static constexpr Sample TONAL_AGREEMENT_MIN_CONFIDENCE = Sample(0.25);
	static constexpr Sample TIMEFACTOR_SMOOTHING = Sample(0.05);
	static constexpr Sample TIMEFACTOR_SNAP_RATIO = Sample(0.1);
	Prediction * predictionsForChannel(int c) {
		return channelPredictions.data() + c*bands;
	}

	Sample getTonalPeakAgreement(int channel, int b, Sample sourcePhase) {
		const auto *bins = bandsForChannel(channel);
		const Sample currentPower = _impl::norm(bins[b].input);
		const Sample previousPower = _impl::norm(bins[b].prevInput);
		Sample weightedAgreement = tinyFloor;
		Sample totalWeight = tinyFloor;
		const Sample phaseScale = Sample(stft.defaultInterval()) / blockProcess.phaseInterval;
		for (int direction : {-1, 1}) {
			Sample lastCurrent = currentPower;
			Sample lastPrevious = previousPower;
			for (int k = b + direction; k >= 0 && k < bands && std::abs(k - b) <= TONAL_AGREEMENT_RADIUS; k += direction) {
				const Sample current = _impl::norm(bins[k].input);
				const Sample previous = _impl::norm(bins[k].prevInput);
				if (current >= lastCurrent || previous >= lastPrevious ||
					current < currentPower * TONAL_AGREEMENT_MIN_POWER ||
					previous < previousPower * TONAL_AGREEMENT_MIN_POWER) {
					break;
				}
				lastCurrent = current;
				lastPrevious = previous;
				const Complex progression = _impl::mul<true>(bins[k].input, bins[k].prevInput);
				Sample difference = std::atan2(progression.imag(), progression.real()) - sourcePhase;
				difference -= Sample(2*M_PI) * std::round(difference / Sample(2*M_PI));
				const Sample deviation = difference * phaseScale / TONAL_AGREEMENT_TOLERANCE;
				const Sample weight = std::sqrt(current * previous);
				weightedAgreement += weight / (Sample(1) + deviation * deviation);
				totalWeight += weight;
			}
		}
		return TONAL_AGREEMENT_MIN_CONFIDENCE + (Sample(1) - TONAL_AGREEMENT_MIN_CONFIDENCE) *
			weightedAgreement / totalWeight;
	}

	// Complex phase integration with temporal anchors and frequency constraints.
	void solvePhaseIntegration(Sample twistTimeFactor) {
		const bool trackReferences = blockProcess.newSpectrum && !blockProcess.mappedFrequencies &&
			!blockProcess.processFormants && blockProcess.timeFactor > TRANSIENT_MIN_STRETCH &&
			transientSamples_ == 0 && transientState_ != TransientState::COLLECTING;
		const bool continueReferences = trackReferences && tonalReferenceHistory_;
		tonalReferenceHistory_ = trackReferences;
		Sample maximumEnergy = 0;
		for (int b = 0; b < bands; ++b) {
			maximumEnergy = std::max(maximumEnergy, std::max(phaseCurrentEnergy[b], phasePreviousEnergy[b]));
		}
		const Sample tolerance = maximumEnergy*PHASE_ENERGY_TOLERANCE;
		const int splitBin = int(freqToBand(splitFreq));
		const int firstTransientBand = int(freqToBand(transientMinFreq_));
		std::uniform_real_distribution<Sample> phaseDist(Sample(-M_PI), Sample(M_PI));
		const Sample frequencyScale = Sample(stft.fftSamples())/(Sample(2*M_PI)*stft.defaultInterval());
		bool previousLeftReference = false;
		Sample previousLeftFrequency = 0;
		for (int b = 0; b < bands; ++b) {
			int channel = 0;
			for (int c = 1; c < channels; ++c) {
				if (predictionsForChannel(c)[b].energy > predictionsForChannel(channel)[b].energy) {
					channel = c;
				}
			}
			phaseChannels[b] = channel;
			phaseSignificant[b] = phaseCurrentEnergy[b] > tolerance;
			phaseAnchorWeight[b] = Sample(1);
			const bool peak = b > 0 && b + 1 < bands &&
				phaseCurrentEnergy[b] > phaseCurrentEnergy[b - 1] &&
				phaseCurrentEnergy[b] >= phaseCurrentEnergy[b + 1];
			const bool previousReference = phaseReferences[b];
			const Sample previousFrequency = phaseReferenceFrequency[b];
			const Sample frequency = Sample(b) + predictionsForChannel(channel)[b].timeAdvance*frequencyScale;
			bool continuingPeak = previousReference &&
				phaseCurrentEnergy[b] >= phasePreviousEnergy[b]*TONAL_REFERENCE_POWER_RATIO;
			if (continueReferences && !continuingPeak && b < splitBin && peak) {
				for (int direction : {-1, 1}) {
					const int k = b + direction;
					const bool reference = direction < 0 ? previousLeftReference : bool(phaseReferences[k]);
					const Sample referenceFrequency = direction < 0 ? previousLeftFrequency : phaseReferenceFrequency[k];
					if (reference && phaseCurrentEnergy[b] >= phasePreviousEnergy[k]*TONAL_REFERENCE_POWER_RATIO &&
						phaseCurrentEnergy[b]*TONAL_REFERENCE_POWER_RATIO <= phasePreviousEnergy[k] &&
						std::abs(frequency - referenceFrequency) <= TONAL_REFERENCE_MAX_BIN_DRIFT) {
						continuingPeak = true;
						break;
					}
				}
			}
			if (continueReferences && continuingPeak && b < splitBin && peak &&
				phaseCurrentEnergy[b] > maximumEnergy*TONAL_REFERENCE_MIN_POWER &&
				energy[b] > smoothedEnergy[b]*TONAL_REFERENCE_PROMINENCE) {
				phaseAnchorWeight[b] = TONAL_REFERENCE_PRIORITY;
			}
			phaseReferences[b] = trackReferences && peak && phaseSignificant[b];
			phaseReferenceFrequency[b] = frequency;
			previousLeftReference = previousReference;
			previousLeftFrequency = previousFrequency;
			if (b >= splitBin && energy[b] > PEAK_SHELTER_RATIO*smoothedEnergy[b]) {
				phaseAnchorWeight[b] = TONAL_REFERENCE_PRIORITY;
			}
			auto &prediction = predictionsForChannel(channel)[b];
			auto &bin = bandsForChannel(channel)[b];
			const bool mappedReset = (blockProcess.mappedFrequencies || blockProcess.processFormants) &&
				b >= firstTransientBand;
			bool snap = transientState_ == TransientState::RESET && (transientHold_[b] || mappedReset);
			const Sample currentPower = _impl::norm(prediction.input);
			const Sample previousPower = _impl::norm(bin.prevInput);
			const Complex progression = _impl::mul<true>(prediction.input, bin.prevInput);
			const Sample interval = Sample(blockProcess.phaseInterval);
			const Sample hop = Sample(stft.defaultInterval());
			Sample correction = 0;
			if (blockProcess.newSpectrum) {
				phaseTemporalConfidence[b] = Sample(0);
			}
			if (blockProcess.newSpectrum && currentPower + previousPower > tinyFloor) {
				const Sample expected = prediction.timeAdvance*interval/hop;
				const Sample sourcePhase = std::atan2(progression.imag(), progression.real());
				Sample measured = sourcePhase - Sample(2*M_PI)*bandToFreq(Sample(b))*interval;
				measured -= Sample(2*M_PI)*std::round((measured - expected)/Sample(2*M_PI));
				const Sample error = measured*hop/interval - prediction.timeAdvance;
				const Sample disagreement = std::clamp((std::abs(error) - TONAL_PHASE_ERROR_START)/
					TONAL_PHASE_ERROR_RANGE, Sample(0), Sample(1));
				if (snap && !mappedReset && phaseSignificant[b]) {
					snap = disagreement >= TRANSIENT_SNAP_MIN_DISAGREEMENT;
				}
				if (phaseAnchorWeight[b] > Sample(1) && !blockProcess.mappedFrequencies &&
					!blockProcess.processFormants && bandToHz(Sample(b)) < TONAL_PHASE_MAX_HZ) {
					const Sample confidence = Sample(2)*std::sqrt(currentPower*previousPower)/
						(currentPower + previousPower + tinyFloor)*getTonalPeakAgreement(channel, b, sourcePhase);
					correction = std::clamp(error*confidence*disagreement*TONAL_PHASE_MAX_WEIGHT,
						-TONAL_PHASE_MAX_CORRECTION, TONAL_PHASE_MAX_CORRECTION);
				}
				const double sourceBalance = 2*std::sqrt(double(currentPower)*previousPower)/
					(double(currentPower) + previousPower + double(tinyFloor));
				const double residual = double(error - correction)/double(TONAL_PHASE_ERROR_RANGE);
				phaseTemporalConfidence[b] = Sample(sourceBalance/(1 + residual*residual));
			}
			phaseReset[b] = snap;
			Complex target = prediction.makeOutput(_impl::mul(bin.output, std::polar(Sample(1), correction)));
			if (snap) {
				target = prediction.makeOutput(prediction.input);
			} else if (!phaseSignificant[b]) {
				target = prediction.makeOutput(std::polar(Sample(1), phaseDist(randomEngine)));
			}
			const double balance = 2*std::sqrt(double(phaseCurrentEnergy[b])*phasePreviousEnergy[b])/
				(double(phaseCurrentEnergy[b]) + phasePreviousEnergy[b] + double(tinyFloor));
			const double temporalWeight = blockProcess.mappedFrequencies || blockProcess.processFormants ?
				balance : double(phaseTemporalConfidence[b]);
			phaseDiagonal[b] = 1 + temporalWeight*phaseAnchorWeight[b];
			phaseSolution[b] = std::complex<double>(target)*phaseDiagonal[b];
			phaseUpper[b] = 0;
		}
		for (int b = 1; b < bands; ++b) {
			if (!phaseSignificant[b - 1] || !phaseSignificant[b] || phaseReset[b - 1] || phaseReset[b] ||
				blockProcess.mappedFrequencies || blockProcess.processFormants) {
				continue;
			}
			const auto &leftPrediction = predictionsForChannel(phaseChannels[b - 1])[b - 1];
			const auto &rightPrediction = predictionsForChannel(phaseChannels[b])[b];
			const double leftEnergy = leftPrediction.energy;
			const double rightEnergy = rightPrediction.energy;
			int channel = phaseChannels[b];
			double overlap = 1;
			if (phaseChannels[b - 1] != phaseChannels[b]) {
				double strongestOverlap = 0;
				for (int c = 0; c < channels; ++c) {
					const auto *candidate = predictionsForChannel(c);
					const double sharedEnergy = double(candidate[b - 1].energy)*candidate[b].energy;
					if (sharedEnergy > strongestOverlap) {
						strongestOverlap = sharedEnergy;
						channel = c;
					}
				}
				overlap = std::sqrt((double(predictionsForChannel(channel)[b - 1].energy)/leftEnergy)*
					(double(predictionsForChannel(channel)[b].energy)/rightEnergy));
			}
			const auto *predictions = predictionsForChannel(channel);
			const auto *bins = bandsForChannel(channel);
			const std::complex<double> current = std::complex<double>(predictions[b].input)*
				std::conj(std::complex<double>(predictions[b - 1].input));
			const std::complex<double> previous = std::complex<double>(bins[b].prevInput)*
				std::conj(std::complex<double>(bins[b - 1].prevInput));
			const double currentNorm = std::norm(current);
			const double previousNorm = std::norm(previous);
			const double crossMagnitude = std::sqrt(currentNorm*previousNorm);
			const double agreement = (current*std::conj(previous)).real()/
				(crossMagnitude + double(tinyFloor));
			const double leftPower = std::norm(std::complex<double>(predictions[b - 1].input));
			const double rightPower = std::norm(std::complex<double>(predictions[b].input));
			const double previousLeftPower = std::norm(std::complex<double>(bins[b - 1].prevInput));
			const double previousRightPower = std::norm(std::complex<double>(bins[b].prevInput));
			const double energyConfidence = 4*crossMagnitude/
				((leftPower + previousLeftPower + double(tinyFloor))*
				(rightPower + previousRightPower + double(tinyFloor)));
			const Sample gradient = (predictions[b - 1].frequencyGradient + predictions[b].frequencyGradient)*Sample(0.5);
			Sample angle = (twistTimeFactor - Sample(1))*gradient;
			angle -= Sample(2*M_PI)*std::round(angle/Sample(2*M_PI));
			angle = std::clamp(angle, -GRADIENT_TWIST_MAX, GRADIENT_TWIST_MAX);
			std::complex<double> rotation = current/(std::sqrt(currentNorm) + double(tinyFloor));
			if (phaseChannels[b - 1] != phaseChannels[b]) {
				const std::complex<double> sourceRelation = std::complex<double>(rightPrediction.input)*
					std::conj(std::complex<double>(leftPrediction.input));
				rotation = sourceRelation/(std::sqrt(std::norm(sourceRelation)) + double(tinyFloor));
			}
			rotation *= std::polar(1.0, double(angle));
			const std::complex<double> temporalRelation = phaseSolution[b]*std::conj(phaseSolution[b - 1]);
			const double temporalAgreement = std::clamp((temporalRelation*std::conj(rotation)).real()/
				(std::sqrt(std::norm(temporalRelation)*std::norm(rotation)) + double(tinyFloor)), -1.0, 1.0);
			const double temporalConfidence = std::min(phaseTemporalConfidence[b - 1], phaseTemporalConfidence[b]);
			const double conflictConfidence = 1/(1 + PHASE_CONFLICT_WEIGHT*temporalConfidence*(1 - temporalAgreement));
			const double weight = overlap*energyConfidence*conflictConfidence*PHASE_FREQUENCY_WEIGHT*
				std::clamp((agreement - double(VERTICAL_AGREEMENT_MIN))/
					(1 - double(VERTICAL_AGREEMENT_MIN)), 0.0, 1.0);
			const double totalEnergy = leftEnergy + rightEnergy;
			const double leftCoefficient = std::sqrt(rightEnergy/totalEnergy);
			const double rightCoefficient = std::sqrt(leftEnergy/totalEnergy);
			phaseDiagonal[b - 1] += weight*leftCoefficient*leftCoefficient*std::norm(rotation);
			phaseDiagonal[b] += weight*rightCoefficient*rightCoefficient;
			phaseUpper[b - 1] = -weight*leftCoefficient*rightCoefficient*std::conj(rotation);
		}
		for (int b = 1; b < bands; ++b) {
			const auto lower = std::conj(phaseUpper[b - 1]);
			phaseDiagonal[b] -= (lower*phaseUpper[b - 1]).real()/phaseDiagonal[b - 1];
			phaseSolution[b] -= lower*phaseSolution[b - 1]/phaseDiagonal[b - 1];
		}
		for (int b = bands - 1; b >= 0; --b) {
			if (b + 1 < bands) {
				phaseSolution[b] -= phaseUpper[b]*phaseSolution[b + 1];
			}
			phaseSolution[b] /= phaseDiagonal[b];
		}
	}

	// If RandomEngine=void, use std::default_random_engine;
	using RandomEngineImpl = typename std::conditional<
		std::is_void<RandomEngine>::value,
		std::default_random_engine,
		RandomEngine
	>::type;
	RandomEngineImpl randomEngine;

	size_t processSpectrumSteps = 0;
	static constexpr size_t splitMainPrediction = 8; // it's just heavy, since we're blending up to 4 different phase predictions
	void updateProcessSpectrumSteps() {
		processSpectrumSteps = 0;
		if (blockProcess.newSpectrum) processSpectrumSteps += channels;
		processSpectrumSteps += smoothEnergySteps;
		if (blockProcess.mappedFrequencies) {
			processSpectrumSteps += 1; // findPeaks
		}
		processSpectrumSteps += 1; // updating the output map
		processSpectrumSteps += channels; // preliminary phase-vocoder prediction
		processSpectrumSteps += splitMainPrediction;
		if (blockProcess.processFormants) processSpectrumSteps += 3;
	}
	void protectTransients() {
		Sample fullPower = 0;
		Sample highPower[3] = {};
		Sample risingEnergy[3] = {};
		Sample totalEnergy[3] = {};
		const int firstBand = int(freqToBand(transientMinFreq_));
		for (int c = 0; c < channels; ++c) {
			auto *bins = bandsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				Sample power = _impl::norm(bins[b].input);
				fullPower += power;
				if (b < firstBand) {
					continue;
				}
				const int region = b < firstBand*3 ? 0 : (b < firstBand*6 ? 1 : 2);
				highPower[region] += power;
				Sample current = std::sqrt(power);
				Sample previousPower = 0;
				const int firstNeighbor = std::max(0, b - TRANSIENT_NEIGHBOR_BINS);
				const int lastNeighbor = std::min(bands - 1, b + TRANSIENT_NEIGHBOR_BINS);
				for (int neighbor = firstNeighbor; neighbor <= lastNeighbor; ++neighbor) {
					previousPower = std::max(previousPower, _impl::norm(bins[neighbor].prevInput));
				}
				Sample previous = std::sqrt(previousPower);
				risingEnergy[region] += std::max<Sample>(0, current - previous);
				totalEnergy[region] += current;
			}
		}
		bool transientTripped = false;
		const bool timedReset = !blockProcess.mappedFrequencies && !blockProcess.processFormants;
		Sample stretchDose = std::clamp((smoothTimeFactor_ - Sample(1)) / smoothTimeFactor_, Sample(0), Sample(1));
		Sample onsetNeed = TRANSIENT_ONSET_FLUX_RATIO * (Sample(1) - Sample(0.3) * stretchDose);
		Sample bgNeed = TRANSIENT_BG_RATIO * (Sample(1) - Sample(0.25) * stretchDose);
		for (int region = 0; region < 3; ++region) {
			if (transientCooldown_ > 0 || transientState_ != TransientState::IDLE) {
				break;
			}
			if (highPower[region] > TRANSIENT_MIN_POWER &&
				highPower[region] > fullPower * TRANSIENT_POWER_FRACTION &&
				highPower[region] > transientBg_[region] * bgNeed &&
				risingEnergy[region] > totalEnergy[region] * onsetNeed) {
				if (transientSamples_ == 0) {
				}
				transientSamples_ = int(stft.blockSamples()/2);
				transientCooldown_ = int(stft.blockSamples()*3/2);
				transientState_ = timedReset ? TransientState::COLLECTING : TransientState::RESET;
				transientTripped = true;
				break;
			}
		}
		if (!transientTripped) {
			Sample mediumOnsetNeed = onsetNeed * TRANSIENT_MEDIUM_ONSET_SCALE;
			Sample mediumBgNeed = bgNeed * TRANSIENT_MEDIUM_BG_SCALE;
			for (int region = 0; region < 3; ++region) {
				if (transientCooldown_ > 0 || transientState_ != TransientState::IDLE) {
					break;
				}
				if (highPower[region] > TRANSIENT_MIN_POWER &&
					highPower[region] > fullPower * TRANSIENT_POWER_FRACTION &&
					highPower[region] > transientBg_[region] * mediumBgNeed &&
					risingEnergy[region] > totalEnergy[region] * mediumOnsetNeed) {
					transientSamples_ = int(stft.blockSamples()/4);
					transientCooldown_ = int(stft.blockSamples()/2);
					transientState_ = timedReset ? TransientState::COLLECTING : TransientState::RESET;
					transientTripped = true;
					break;
				}
			}
		}
		if (!transientTripped && transientCooldown_ == 0 && transientState_ == TransientState::IDLE && firstBand > 1) {
			Sample lowPower = 0;
			Sample lowRising = 0;
			Sample lowTotal = 0;
			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				for (int b = 0; b < firstBand; ++b) {
					Sample power = _impl::norm(bins[b].input);
					lowPower += power;
					Sample current = std::sqrt(power);
					Sample previousPower = 0;
					const int firstNeighbor = std::max(0, b - TRANSIENT_NEIGHBOR_BINS);
					const int lastNeighbor = std::min(bands - 1, b + TRANSIENT_NEIGHBOR_BINS);
					for (int neighbor = firstNeighbor; neighbor <= lastNeighbor; ++neighbor) {
						previousPower = std::max(previousPower, _impl::norm(bins[neighbor].prevInput));
					}
					Sample previous = std::sqrt(previousPower);
					lowRising += std::max<Sample>(0, current - previous);
					lowTotal += current;
				}
			}
			Sample bassOnsetNeed = onsetNeed * TRANSIENT_MEDIUM_ONSET_SCALE;
			if (lowPower > TRANSIENT_MIN_POWER &&
				lowPower > fullPower * TRANSIENT_BASS_POWER_FRACTION &&
				lowRising > lowTotal * bassOnsetNeed) {
				transientSamples_ = int(stft.blockSamples()/4);
				transientCooldown_ = int(stft.blockSamples()/2);
				transientState_ = timedReset ? TransientState::COLLECTING : TransientState::RESET;
				transientTripped = true;
			}
		}
		if (!transientBgReady_) {
			if (!transientTripped && transientSamples_ == 0 && transientCooldown_ == 0 && transientState_ == TransientState::IDLE) {
				for (int region = 0; region < 3; ++region) {
					transientBg_[region] = highPower[region];
				}
				transientBgReady_ = true;
			}
		} else if (transientSamples_ == 0 && transientCooldown_ == 0 && transientState_ == TransientState::IDLE) {
			Sample upRate = Sample(1)/(Sample(8)*blockProcess.timeFactor);
			Sample downRate = upRate*Sample(0.125);
			for (int region = 0; region < 3; ++region) {
				Sample rate = highPower[region] > transientBg_[region] ? upRate : downRate;
				transientBg_[region] += (highPower[region] - transientBg_[region])*rate;
			}
		}
		if (!timedReset && transientTripped) {
			Sample holdNeed = TRANSIENT_FLUX_RATIO * (Sample(1) - Sample(0.25) * stretchDose);
			for (int b = 0; b < bands; ++b) {
				Sample curPower = Sample(0);
				Sample prevPower = Sample(0);
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					curPower += _impl::norm(bins[b].input);
					prevPower += _impl::norm(bins[b].prevInput);
				}
				if (curPower > Sample(0)) {
					Sample curMag = std::sqrt(curPower);
					Sample prevMag = Sample(0);
					if (prevPower > Sample(0)) {
						prevMag = std::sqrt(prevPower);
					}
					if (curMag - prevMag > curMag * holdNeed) {
						transientHold_[b] = 1;
					}
				}
			}
			return;
		}
		if (transientTripped) {
			transientInputSamples_ = int(stft.blockSamples());
		}
		if (transientState_ != TransientState::COLLECTING) {
			return;
		}

		Sample markedPower = 0;
		Sample futurePower = 0;
		int first = 0;
		while (first < bands) {
			int peak = first;
			while (peak + 1 < bands && energy[peak + 1] >= energy[peak]) {
				++peak;
			}
			int last = peak;
			while (last + 1 < bands && energy[last + 1] < energy[last]) {
				++last;
			}
			Sample power = 0;
			Sample moment = 0;
			Sample rising = 0;
			Sample magnitude = 0;
			for (int b = first; b <= last; ++b) {
				Sample previousPower = 0;
				const int firstNeighbor = std::max(0, b - TRANSIENT_NEIGHBOR_BINS);
				const int lastNeighbor = std::min(bands - 1, b + TRANSIENT_NEIGHBOR_BINS);
				for (int c = 0; c < channels; ++c) {
					const auto *bins = bandsForChannel(c);
					moment += _impl::mul<true>(bins[b].timed, bins[b].input).real();
					Sample previous = 0;
					for (int neighbor = firstNeighbor; neighbor <= lastNeighbor; ++neighbor) {
						previous = std::max(previous, _impl::norm(bins[neighbor].prevInput));
					}
					previousPower += previous;
				}
				Sample current = std::sqrt(energy[b]);
				power += energy[b];
				rising += std::max(Sample(0), current - std::sqrt(previousPower));
				magnitude += current;
			}
			Sample collectPosition = transientTripped ? Sample(0) : transientCenter_;
			if (power > TRANSIENT_MIN_POWER && rising > magnitude*onsetNeed && moment > power*collectPosition) {
				std::fill(transientHold_.begin() + first, transientHold_.begin() + last + 1, 1);
			}
			Sample selectedPower = 0;
			for (int b = first; b <= last; ++b) {
				if (transientHold_[b] != 0) {
					selectedPower += energy[b];
				}
			}
			markedPower += selectedPower;
			if (moment > power*transientCenter_) {
				futurePower += selectedPower;
			}
			first = last + 1;
		}
		if (markedPower > TRANSIENT_MIN_POWER && futurePower <= markedPower*TRANSIENT_READY_POWER_FRACTION) {
			transientState_ = TransientState::RESET;
			transientSamples_ = std::max(transientSamples_, int(stft.blockSamples()/4));
		}
	}

	void processSpectrum(size_t step) {
		const bool sourcePhase = sourcePhaseWeight_ == Sample(1);
		Sample clampedTimeFactor = std::max<Sample>(blockProcess.timeFactor, 1/maxCleanLow);
		Sample twistTimeFactor = clampedTimeFactor > TRANSIENT_MIN_STRETCH ?
			std::max<Sample>(smoothTimeFactor_, 1/maxCleanLow) : clampedTimeFactor;

		Sample smoothingBins = Sample(stft.fftSamples())/stft.defaultInterval();

		if (blockProcess.newSpectrum) {
			if (step < size_t(channels)) {
				int channel = int(step);
				auto bins = bandsForChannel(channel);

				Complex rot = std::polar(Sample(1), bandToFreq(0)*stft.defaultInterval()*Sample(2*M_PI));
				Sample freqStep = bandToFreq(1) - bandToFreq(0);
				Complex rotStep = std::polar(Sample(1), freqStep*stft.defaultInterval()*Sample(2*M_PI));

				for (int b = 0; b < bands; ++b) {
					auto &bin = bins[b];
					bin.output = _impl::mul(bin.output, rot);
					rot = _impl::mul(rot, rotStep);
				}
				return;
			}
			step -= channels;
		}
		if (step < smoothEnergySteps) {
			smoothEnergy(step, smoothingBins);
			return;
		}
		step -= smoothEnergySteps;
		if (blockProcess.mappedFrequencies) {
			if (step-- == 0) {
				findPeaks();
				return;
			}
		}
		if (step-- == 0) {
			if (transientState_ != TransientState::IDLE &&
				(blockProcess.timeFactor <= TRANSIENT_MIN_STRETCH || blockProcess.mappedFrequencies || blockProcess.processFormants)) {
				transientState_ = TransientState::IDLE;
				transientInputSamples_ = 0;
				std::fill(transientHold_.begin(), transientHold_.end(), 0);
			}
			if (blockProcess.newSpectrum && blockProcess.timeFactor > TRANSIENT_MIN_STRETCH) {
				protectTransients();
			}
			if (blockProcess.mappedFrequencies) {
				updateOutputMap();
			} else {
				for (int b = 0; b < bands; ++b) {
					outputMap[b] = {Sample(b), 1};
				}
			}
			return;
		}
		if (blockProcess.processFormants) {
			if (step < 3) {
				updateFormants(step);
				return;
			}
			step -= 3;
		}
		// Preliminary output prediction from phase-vocoder
		if (step < size_t(channels)) {
			int c = int(step);
			Band *bins = bandsForChannel(c);
			auto *predictions = predictionsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				auto mapPoint = outputMap[b];
				int lowIndex = static_cast<int>(std::floor(mapPoint.inputBin));
				Sample fracIndex = mapPoint.inputBin - lowIndex;

				Prediction &prediction = predictions[b];
				Sample prevEnergy = prediction.energy;
				{
					Sample lowEnergy = getBand<&Band::inputEnergy>(c, lowIndex);
					Sample highEnergy = getBand<&Band::inputEnergy>(c, lowIndex + 1);
					Sample logLow = std::log(lowEnergy + tinyFloor);
					Sample logHigh = std::log(highEnergy + tinyFloor);
					prediction.energy = std::exp(logLow + (logHigh - logLow) * fracIndex) - tinyFloor;
					if (prediction.energy < Sample(0)) {
						prediction.energy = Sample(0);
					}
				}
				prediction.energy *= std::max<Sample>(0, mapPoint.freqGrad); // scale the energy according to local stretch factor
				prediction.input = getFractional<&Band::input>(c, lowIndex, fracIndex);
				if (c == 0) {
					phasePreviousEnergy[b] = prevEnergy;
					phaseCurrentEnergy[b] = prediction.energy;
				} else {
					phasePreviousEnergy[b] = std::max(phasePreviousEnergy[b], prevEnergy);
					phaseCurrentEnergy[b] = std::max(phaseCurrentEnergy[b], prediction.energy);
				}

				auto &outputBin = bins[b];
				Complex derivative = getFractional<&Band::derivative>(c, lowIndex, fracIndex);
				Complex timed = getFractional<&Band::timed>(c, lowIndex, fracIndex);
				Sample inputPower = _impl::norm(prediction.input) + tinyFloor;
				Sample timeGradient = -_impl::mul<true>(derivative, prediction.input).imag()
					/inputPower*stft.defaultInterval();
				if (blockProcess.mappedFrequencies) {
					const Sample phaseScale = Sample(2*M_PI)*stft.defaultInterval();
					const Complex lowInput = getBand<&Band::input>(c, lowIndex);
					const Complex highInput = getBand<&Band::input>(c, lowIndex + 1);
					const Complex weightedFrequency = lowInput*((Sample(1) - fracIndex)*bandToFreq(Sample(lowIndex))) +
						highInput*(fracIndex*bandToFreq(Sample(lowIndex + 1)));
					const Sample sourceFrequency = _impl::mul<true>(weightedFrequency, prediction.input).real()/inputPower +
						timeGradient/phaseScale;
					timeGradient = (mapFreq(sourceFrequency) - bandToFreq(Sample(b)))*phaseScale;
				}
				prediction.frequencyGradient = -Sample(2*M_PI)/stft.fftSamples()
					*_impl::mul<true>(timed, prediction.input).real()/inputPower;
				timeGradient = std::clamp(timeGradient, Sample(-M_PI), Sample(M_PI));
				prediction.frequencyGradient = std::clamp(prediction.frequencyGradient, Sample(-M_PI), Sample(M_PI));
				Sample horizontalAngle = (prediction.timeGradient + timeGradient)*Sample(0.5);
				prediction.timeAdvance = horizontalAngle;
				Complex timeTwist = std::polar(Sample(1), horizontalAngle);
				prediction.timeGradient = timeGradient;
				outputBin.output = _impl::mul(outputBin.output, timeTwist);
			}
			return;
		}
		step -= channels;

		if (step < splitMainPrediction) {
			// Re-predict using phase differences between frequencies
			size_t chunk = step;
			if (chunk == 0) { // YARG phase integration, shared by later chunks
				solvePhaseIntegration(twistTimeFactor);
			}
			int startI = int(bands*chunk/splitMainPrediction);
			int endI = int(bands*(chunk + 1)/splitMainPrediction);
			for (int b = startI; b < endI; ++b) {
				// Find maximum-energy channel and calculate that
				const int maxChannel = phaseChannels[b];
				auto &prediction = predictionsForChannel(maxChannel)[b];
				auto &outputBin = bandsForChannel(maxChannel)[b];
				const bool snapTransient = phaseReset[b];
				Complex phase = Complex(phaseSolution[b]);
				if (sourcePhaseWeight_ > Sample(0) && !sourcePhase) {
					const Complex rotation = _impl::mul<true>(prediction.input, phase);
					const Sample angle = std::atan2(rotation.imag(), rotation.real());
					phase = _impl::mul(phase, std::polar(Sample(1), angle * sourcePhaseWeight_));
				}
				outputBin.output = sourcePhase ? outputBin.input :
					prediction.makeOutput(phase);
				
				// All other bins are locked in phase
				for (int c = 0; c < channels; ++c) {
					if (c != maxChannel) {
						auto &channelBin = bandsForChannel(c)[b];
						auto &channelPrediction = predictionsForChannel(c)[b];
						if (sourcePhase) {
							channelBin.output = channelBin.input;
						} else if (snapTransient) {
							channelBin.output = channelPrediction.makeOutput(channelPrediction.input);
						} else {
							Complex channelTwist = _impl::mul<true>(channelPrediction.input, prediction.input);
							Sample twistNorm = _impl::norm(channelTwist);
							if (twistNorm > Sample(0)) {
								channelTwist /= std::sqrt(twistNorm);
							}
							Complex channelPhase = _impl::mul(outputBin.output, channelTwist);
							channelBin.output = channelPrediction.makeOutput(channelPhase);
						}
					}
				}
			}
			if (chunk + 1 == splitMainPrediction && transientState_ == TransientState::RESET) {
				transientState_ = transientInputSamples_ > 0 ? TransientState::COOLDOWN : TransientState::IDLE;
				if (transientState_ == TransientState::IDLE) {
					std::fill(transientHold_.begin(), transientHold_.end(), 0);
				}
			}
			return;
		}
	}

	// Produces smoothed energy across all channels
	static constexpr size_t smoothEnergySteps = 3;
	Sample smoothEnergyState = 0;
	Sample smoothTimeFactor_ = 1;
	Sample sourcePhaseWeight_ = 0;
	void trackTimeFactor(Sample measured) {
		Sample difference = measured >= smoothTimeFactor_ ? measured - smoothTimeFactor_ : smoothTimeFactor_ - measured;
		if (difference > smoothTimeFactor_*TIMEFACTOR_SNAP_RATIO) {
			smoothTimeFactor_ = measured;
		} else {
			smoothTimeFactor_ += (measured - smoothTimeFactor_)*TIMEFACTOR_SMOOTHING;
		}
	}
	void smoothEnergy(size_t step, Sample smoothingBins) {
		Sample smoothingSlew = 1/(1 + smoothingBins*Sample(0.5));
		if (step-- == 0) {
			for (auto &e : energy) e = 0;
			for (int c = 0; c < channels; ++c) {
				Band *bins = bandsForChannel(c);
				for (int b = 0; b < bands; ++b) {
					Sample e = _impl::norm(bins[b].input);
					bins[b].inputEnergy = e; // Used for interpolating prediction energy
					energy[b] += e;
				}
			}
			for (int b = 0; b < bands; ++b) {
				smoothedEnergy[b] = energy[b];
			}
			smoothEnergyState = 0;
			return;
		}

		// The two other steps are repeated smoothing passes, down and up
		Sample e = smoothEnergyState;
		for (int b = bands - 1; b >= 0; --b) {
			e += (smoothedEnergy[b] - e)*smoothingSlew;
			smoothedEnergy[b] = e;
		}
		for (int b = 0; b < bands; ++b) {
			e += (smoothedEnergy[b] - e)*smoothingSlew;
			smoothedEnergy[b] = e;
		}
		smoothEnergyState = e;
	}
	
	Sample mapFreq(Sample freq) const {
		if (customFreqMap) return customFreqMap(freq);
		if (freq > freqTonalityLimit) {
			return freq + (freqMultiplier - 1)*freqTonalityLimit;
		}
		return freq*freqMultiplier;
	}
	
	// Identifies spectral peaks using energy across all channels
	void findPeaks() {
		peaks.resize(0);
		
		int start = 0;
		while (start < bands) {
			if (energy[start] > smoothedEnergy[start]) {
				int end = start;
				Sample bandSum = 0, energySum = 0;
				while (end < bands && energy[end] > smoothedEnergy[end]) {
					bandSum += end*energy[end];
					energySum += energy[end];
					++end;
				}
				Sample avgBand = bandSum/energySum;
				Sample avgFreq = bandToFreq(avgBand);
				peaks.emplace_back(Peak{avgBand, freqToBand(mapFreq(avgFreq))});

				start = end;
			}
			++start;
		}
	}
	
	void updateOutputMap() {
		if (peaks.empty()) {
			for (int b = 0; b < bands; ++b) {
				outputMap[b] = {Sample(b), 1};
			}
			return;
		}
		Sample bottomOffset = peaks[0].input - peaks[0].output;
		for (int b = 0; b < std::min(bands, static_cast<int>(std::ceil(peaks[0].output))); ++b) {
			outputMap[b] = {b + bottomOffset, 1};
		}
		// Interpolate between points
		for (size_t p = 1; p < peaks.size(); ++p) {
			const Peak &prev = peaks[p - 1], &next = peaks[p];
			Sample rangeScale = 1/(next.output - prev.output);
			Sample outOffset = prev.input - prev.output;
			Sample outScale = next.input - next.output - prev.input + prev.output;
			Sample gradScale = outScale*rangeScale;
			int startBin = std::max(0, static_cast<int>(std::ceil(prev.output)));
			int endBin = std::min(bands, static_cast<int>(std::ceil(next.output)));
			for (int b = startBin; b < endBin; ++b) {
				Sample r = (b - prev.output)*rangeScale;
				Sample h = r*r*(3 - 2*r);
				Sample outB = b + outOffset + h*outScale;
				
				Sample gradH = 6*r*(1 - r);
				Sample gradB = 1 + gradH*gradScale;
				
				outputMap[b] = {outB, gradB};
			}
		}
		Sample topOffset = peaks.back().input - peaks.back().output;
		for (int b = std::max(0, static_cast<int>(peaks.back().output)); b < bands; ++b) {
			outputMap[b] = {b + topOffset, 1};
		}
	}

	// If we mapped formants the same way as mapFreq(), this would be the inverse
	Sample invMapFormant(Sample freq) const {
		if (freq*invFormantMultiplier > freqTonalityLimit) {
			return freq + (1 - formantMultiplier)*freqTonalityLimit;
		}
		return freq*invFormantMultiplier;
	}

	Sample freqEstimateWeighted = 0;
	Sample freqEstimateWeight = 0;
	Sample estimateFrequency() {
		// 3 highest peaks in the input
		std::array<int, 3> peakIndices{0, 0, 0};
		for (int b = 1; b < bands - 1; ++b) {
			Sample e = formantMetric[b];
			// local maxima only
			if (e < formantMetric[b - 1] || e <= formantMetric[b + 1]) continue;
			
			if (e > formantMetric[peakIndices[0]]) {
				if (e > formantMetric[peakIndices[1]]) {
					if (e > formantMetric[peakIndices[2]]) {
						peakIndices = {peakIndices[1], peakIndices[2], b};
					} else {
						peakIndices = {peakIndices[1], b, peakIndices[2]};
					}
				} else {
					peakIndices[0] = b;
				}
			}
		}
		
		// VERY rough pitch estimation
		int peakEstimate = peakIndices[2];
		if (formantMetric[peakIndices[1]] > formantMetric[peakIndices[2]]*0.1) {
			int diff = std::abs(peakEstimate - peakIndices[1]);
			if (diff > peakEstimate/8 && diff < peakEstimate*7/8) peakEstimate = peakEstimate%diff;
			if (formantMetric[peakIndices[0]] > formantMetric[peakIndices[2]]*0.01) {
				diff = std::abs(peakEstimate - peakIndices[0]);
				if (diff > peakEstimate/8 && diff < peakEstimate*7/8) peakEstimate = peakEstimate%diff;
			}
		}
		Sample weight = formantMetric[peakIndices[2]];
		// Smooth it out a bit
		freqEstimateWeighted += (peakEstimate*weight - freqEstimateWeighted)*Sample(0.25);
		freqEstimateWeight += (weight - freqEstimateWeight)*Sample(0.25);
		
		return freqEstimateWeighted/(freqEstimateWeight + Sample(1e-30));
	}
	
	Sample freqEstimate;
	
	std::vector<Sample> formantMetric;
	Sample formantBaseFreq = 0;
	void updateFormants(size_t step) {
		if (step-- == 0) {
			for (auto &e : formantMetric) e = 0;
			for (int c = 0; c < channels; ++c) {
				Band *bins = bandsForChannel(c);
				for (int b = 0; b < bands; ++b) {
					formantMetric[b] += bins[b].inputEnergy;
				}
			}

			freqEstimate = freqToBand(formantBaseFreq);
			if (formantBaseFreq <= 0) freqEstimate = estimateFrequency();
		} else if (step-- == 0) {
			Sample decay = 1 - 1/(freqEstimate*Sample(0.5) + 1);
			Sample e = 0;
			for (size_t repeat = 0; repeat < 2; ++repeat) {
				for (int b = bands - 1; b >= 0; --b) {
					e = std::max(formantMetric[b], e*decay);
					formantMetric[b] = e;
				}
				for (int b = 0; b < bands; ++b) {
					e = std::max(formantMetric[b], e*decay);
					formantMetric[b] = e;
				}
			}
			decay = 1/decay;
			for (size_t repeat = 0; repeat < 2; ++repeat) {
				for (int b = bands - 1; b >= 0; --b) {
					e = std::min(formantMetric[b], e*decay);
					formantMetric[b] = e;
				}
				for (int b = 0; b < bands; ++b) {
					e = std::min(formantMetric[b], e*decay);
					formantMetric[b] = e;
				}
			}
		} else {
			auto getFormant = [&](Sample band) -> Sample {
				if (band < 0) return 0;
				band = std::min(band, static_cast<Sample>(bands));
				int floorBand = std::floor(band);
				Sample fracBand = band - floorBand;
				Sample low = formantMetric[floorBand], high = formantMetric[floorBand + 1];
				return low + (high - low)*fracBand;
			};

			for (int b = 0; b < bands; ++b) {
				Sample inputF = bandToFreq(static_cast<Sample>(b));
				Sample outputF = formantCompensation ? mapFreq(inputF) : inputF;
				outputF = invMapFormant(outputF);

				Sample inputE = formantMetric[b];
				Sample targetE = getFormant(freqToBand(outputF));

				Sample formantRatio = targetE/(inputE + Sample(1e-30));
				Sample energyRatio = formantRatio;

				for (int c = 0; c < channels; ++c) {
					Band *bins = bandsForChannel(c);
					// This is what's used to decide the output energy, so this affects the output
					bins[b].inputEnergy *= energyRatio;
				}
			}
		}
	}

	// Proxy class to avoid copying/allocating anything
	template<class Io>
	struct OffsetIO {
		Io &io;
		int offset;

		struct Channel {
			Io &io;
			int channel;
			int offset;
			
			auto operator[](int i) -> decltype(io[0][0]) {
				return io[channel][i + offset];
			}
		};
		Channel operator[](int c) {
			return {io, c, offset};
		}
	};
};

} // namespace yarg::audio

#endif // include guard
