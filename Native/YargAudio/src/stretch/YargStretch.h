#ifndef YARG_AUDIO_STRETCH_YARG_STRETCH_H
#define YARG_AUDIO_STRETCH_YARG_STRETCH_H

// YargStretch - time stretcher: changes playback speed without changing pitch.
//
// Based on Signalsmith Stretch 1.3.2 by Geraint Luff (Signalsmith Audio):
//   https://github.com/Signalsmith-Audio/signalsmith-stretch
//   Licensed under the MIT License (see third_party/signalsmith-linear/LICENSE.txt).
//
// YARG changes on top of upstream:
//   - Transient detection with phase snapping, so drum hits stay sharp.
//   - Split low/high handling that keeps bass solid at extreme slowdowns.
//   - PVDR loudest-first traversal (Prusa & Holighaus), so strong partials lead.
//   - Reassigned STFT gradients for sharper pitch/timing estimates.
//   - Shaping chain (harmonic/stereo locks, de-esser, limiter) for clean extremes.
//
// One output block goes through process() in this order:
//   1. Map the block to its input position for the current rate.
//   2. STFT analysis of the new input, plus gradient spectra for reassignment.
//   3. Loudest-first phase advance (PVDR traversal).
//   4. Shaping chain: locks, transient handling, harshness control, limiting.
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

	void setAttackGainParams(Sample powerGate, Sample smoothRatio, Sample slope, Sample mediumSlope) {
		attackPowerGate_ = powerGate;
		attackSmoothRatio_ = smoothRatio;
		attackSlope_ = slope;
		attackMediumSlope_ = mediumSlope;
	}


	int transientCount() const {
		return transientCount_;
	}

	void reset(long seed) {
		randomEngine.seed(seed);
		reset();
		noiseMorph.reset(seed);
	}

	void reset() {
		stft.reset(0.1);
		noiseMorph.reset(0);
		stashedInput = stft.input;
		stashedOutput = stft.output;
		
		prevInputOffset = -1;
		_channelBands.assign(_channelBands.size(), Band());
		channelPredictions.assign(channelPredictions.size(), Prediction());
		silenceCounter = 0;
		didSeek = false;
		blockProcess = {};
		smoothTimeFactor_ = 1;
		freqEstimateWeighted = freqEstimateWeight = 0;
		transientSamples_ = int(stft.blockSamples());
		transientCooldown_ = 0;
		transientCount_ = 0;
		transientOnset_ = false;
		transientBg_[0] = transientBg_[1] = transientBg_[2] = 0;
		transientBgReady_ = false;
		for (int b = 0; b < bands; ++b) {
			pvdrOrder[b] = b;
			pvdrCurrentEnergy[b] = 0;
			pvdrPreviousEnergy[b] = 0;
		}
		std::fill(transientHold_.begin(), transientHold_.end(), 0);
		std::fill(hblLocked_.begin(), hblLocked_.end(), 0);
	}

	// One-time setup: channel count, sample rate, block and hop sizes.
	void configure(int nChannels, Sample sampleRate, int blockSamples, int intervalSamples, bool splitComputation=false) {
		_splitComputation = splitComputation;
		channels = nChannels;
		sampleRate_ = sampleRate;
		stft.configure(channels, channels, blockSamples, intervalSamples + 1);
		stft.setInterval(intervalSamples, stft.kaiser);
		stft.reset(Sample(0.1));
		const bool useGrains = channels <= 2 && !splitComputation &&
			intervalSamples * 3 <= blockSamples;
		noiseMorph.configure(stft, channels, useGrains);
		derivativeWindow.resize(blockSamples);
		timeWindow.resize(blockSamples);
		const auto *window = stft.analysisWindow();
		for (int i = 0; i < blockSamples; ++i) {
			Sample left = i > 0 ? window[i - 1] : Sample(0);
			Sample right = i + 1 < blockSamples ? window[i + 1] : Sample(0);
			derivativeWindow[i] = (right - left)*Sample(0.5);
			timeWindow[i] = window[i]*(i - int(stft.analysisOffset()));
		}
		transientSamples_ = int(stft.blockSamples());
		transientCooldown_ = 0;
		transientCount_ = 0;
		transientOnset_ = false;
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
		pvdrOrder.resize(bands);
		pvdrParent.resize(bands);
		pvdrHeap.resize(bands*2);
		pvdrCurrentEnergy.resize(bands);
		pvdrPreviousEnergy.resize(bands);
		pvdrSteer_.assign(bands, Sample(1));
		transientHold_.assign(bands, 0);
		hblLocked_.assign(bands, 0);

		for (int b = 0; b < bands; ++b) {
			pvdrOrder[b] = b;
			pvdrCurrentEnergy[b] = 0;
		}

		blockProcess = {};
		formantMetric.resize(bands + 2);

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
#ifdef YARG_STRETCH_PROFILE_PROCESS_START
		YARG_STRETCH_PROFILE_PROCESS_START(inputSamples, outputSamples);
#endif
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
					}
					//stft.reset();
					blockProcess = {};
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
				prevInputOffset = inputOffset;
				
				copyInput(inputOffset);
				stashedInput = stft.input; // save the input state, since that's what we'll analyse later
				if (_splitComputation) {
					stashedOutput = stft.output; // save the current output, and read from it
					stft.moveOutput(stft.defaultInterval()); // the actual input jumps forward in time by one interval, ready for the synthesis
				}

				blockProcess.newSpectrum = didSeek || (inputInterval > 0);
				blockProcess.mappedFrequencies = customFreqMap || freqMultiplier != 1;
				if (blockProcess.newSpectrum) {
					// make sure the previous input is the correct distance in the past (give or take 1 sample)
					blockProcess.reanalysePrev = didSeek || std::abs(inputInterval - int(stft.defaultInterval())) > 1;
					if (blockProcess.reanalysePrev) blockProcess.steps += stft.analyseSteps() + 1;

					// analyse a new input
					blockProcess.steps += 3*(stft.analyseSteps() + 1);
				}
				
				blockProcess.processFormants = formantMultiplier != 1 || (formantCompensation && blockProcess.mappedFrequencies);

				blockProcess.timeFactor = didSeek ? seekTimeFactor : stft.defaultInterval()/static_cast<Sample>(std::max(1, inputInterval));
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
#ifdef YARG_STRETCH_PROFILE_PROCESS_STEP
				YARG_STRETCH_PROFILE_PROCESS_STEP(step, blockProcess.steps);
#endif
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
						for (int c = 0; c < channels; ++c) {
							auto channelBands = bandsForChannel(c);
							auto *spectrumBands = stft.spectrum(c);
							for (int b = 0; b < bands; ++b) {
								channelBands[b].input = spectrumBands[b];
							}
						}
						continue;
					}
					step -= 1;

					const size_t gradientSteps = stft.analyseSteps() + 1;
					if (step < 2*gradientSteps) {
						const bool timeWeighted = step >= gradientSteps;
						step %= gradientSteps;
						if (step < stft.analyseSteps()) {
							auto &window = timeWeighted ? timeWindow : derivativeWindow;
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
									if (timeWeighted) {
										bins[b].timed = spectrum[b];
									} else {
										bins[b].derivative = spectrum[b];
									}
								}
							}
						}
						continue;
					}
					step -= 2*gradientSteps;
				}

				if (step < processSpectrumSteps) {
					processSpectrum(step);
					continue;
				}
				step -= processSpectrumSteps;

				if (step < 1) {
					// Shaping chain: phase locks, transient handling, harshness control,
					// loudness caps, in that order.
					applyHarmonicPolish();
					applySidelobeLock();
					applySubBassPhaseAlignment();
					applyStereoLock();
					applyPhasePolish();
					applyVocalBandEnhancement();
					applyAttackGain();
					applySpectralContrastAndAntiRinging();
					applyInterHopCancellationCompensation();
					applyCausalPreEchoSuppression();
					applySpectralPeakAndValleyShaping();
					applyDeesser();
					applyExtremeStretchAttenuation();
					applyDynamicModulationRestoration();
					applyDecayDamping();
					applyPostTransientShaping();
					applySpectralDynamicsLimiting();
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
					if (channels <= 2 && noiseMorphEnabled_) {
						Sample strength = std::clamp(getSpeedDose(), Sample(0), Sample(1));
						if (blockProcess.mappedFrequencies || blockProcess.processFormants || transientSamples_ > 0) {
							strength = 0;
						}
						noiseMorph.apply(stft, [&](int c, int b) { return bandsForChannel(c)[b].input; }, strength, transientMinFreq_);
					}
					continue;
				}
				step -= 1;
				
				if (step < stft.synthesiseSteps()) {
					stft.synthesiseStep(step);
					continue;
				}
			}
#ifdef YARG_STRETCH_PROFILE_PROCESS_ENDSTEP
			YARG_STRETCH_PROFILE_PROCESS_ENDSTEP();
#endif

			++blockProcess.samplesSinceLast;
			if (_splitComputation) stashedOutput.swap(stft.output);
			for (int c = 0; c < channels; ++c) {
				auto &&outputChannel = outputs[c];
				Sample v = 0;
				stft.readOutput(c, 1, &v);
				if (noiseMorphEnabled_ && noiseMorph.usesGrains()) {
					v += noiseMorph.readGrain(c);
				}
				outputChannel[outputIndex] = v;
			}
			if (noiseMorphEnabled_ && noiseMorph.usesGrains()) {
				noiseMorph.advanceGrain();
			}
			stft.moveOutput(1);
			if (_splitComputation) stashedOutput.swap(stft.output);
		}
		
		copyInput(inputSamples);
		prevInputOffset -= inputSamples;
#ifdef YARG_STRETCH_PROFILE_PROCESS_END
		YARG_STRETCH_PROFILE_PROCESS_END();
#endif
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
		if (noiseMorphEnabled_ && noiseMorph.usesGrains()) {
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
		stft.reset(0.1f);
		noiseMorph.reset(0);
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
		Sample timeFactor;
	} blockProcess;

	using Complex = std::complex<Sample>;
	static constexpr Sample noiseFloor{Sample(1e-15)};
	static constexpr Sample tinyFloor{Sample(1e-30)};
	static constexpr Sample maxCleanLow{6}; // YARG local patch: lows stay clean (upstream maxCleanStretch 2)
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
	Sample transientMinFreq_{Sample(0.03)};
	Sample attackPowerGate_{Sample(2.25)};
	Sample attackSmoothRatio_{Sample(0.5)};
	Sample attackSlope_{Sample(0.12)};
	Sample attackMediumSlope_{Sample(0)};
	int transientSamples_ = 0;
	int transientCooldown_ = 0;
	int transientCount_ = 0;
	bool transientOnset_ = false;
	static constexpr Sample ENHANCEMENT_STRENGTH = Sample(1.5);

	Sample getSpeedDose() const noexcept {
		Sample raw = std::clamp((smoothTimeFactor_ - Sample(1)) / smoothTimeFactor_, Sample(0), Sample(1));
		return raw * ENHANCEMENT_STRENGTH;
	}

	Sample getHighFrequencyTaper(Sample freq) const noexcept {
		constexpr Sample TAPER_START_HZ = Sample(3500);
		constexpr Sample TAPER_END_HZ = Sample(5000);
		if (freq <= TAPER_START_HZ) {
			return Sample(1);
		}
		if (freq >= TAPER_END_HZ) {
			return Sample(0);
		}
		return (TAPER_END_HZ - freq) / (TAPER_END_HZ - TAPER_START_HZ);
	}
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
	bool noiseMorphEnabled_ = true;
	typename STFT::Input stashedInput;
	typename STFT::Output stashedOutput;
	
	std::vector<Sample> tmpProcessBuffer, tmpPreRollBuffer;
	std::vector<Sample> derivativeWindow, timeWindow;

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
	std::vector<int> pvdrOrder;
	std::vector<int> pvdrParent, pvdrHeap;
	std::vector<Sample> pvdrCurrentEnergy;
	std::vector<Sample> pvdrPreviousEnergy;
	std::vector<Sample> pvdrSteer_;
	std::vector<char> transientHold_;
	std::vector<char> hblLocked_;
	static constexpr Sample PVDR_ENERGY_TOLERANCE = Sample(1e-12);
	static constexpr int PVDR_PREVIOUS = -1;
	static constexpr int PVDR_UNPROCESSED = -2;
	static constexpr int PVDR_RANDOM = -3;
	static constexpr int PEAK_SHELTER_RADIUS = 4;
	static constexpr Sample PEAK_SHELTER_RATIO = Sample(4);
	static constexpr Sample TIMEFACTOR_SMOOTHING = Sample(0.05);
	static constexpr Sample TIMEFACTOR_SNAP_RATIO = Sample(0.1);
	Prediction * predictionsForChannel(int c) {
		return channelPredictions.data() + c*bands;
	}

	// PVDR traversal adapted from Holighaus and Prusa, "Phase vocoder done right", EUSIPCO 2017.
	void preparePvdrTraversal() {
		if (transientSamples_ > 0) {
			for (int b = 0; b < bands; ++b) {
				pvdrOrder[b] = b;
			}
			std::sort(pvdrOrder.begin(), pvdrOrder.end(), [&](int a, int b) {
				if (pvdrCurrentEnergy[a] != pvdrCurrentEnergy[b]) {
					return pvdrCurrentEnergy[a] > pvdrCurrentEnergy[b];
				}
				return a < b;
			});
			return;
		}
		if (!blockProcess.mappedFrequencies && blockProcess.timeFactor > TRANSIENT_MIN_STRETCH) {
			Sample peakMax = 0;
			for (int b = 0; b < bands; ++b) {
				peakMax = std::max(peakMax, std::max(pvdrPreviousEnergy[b], pvdrCurrentEnergy[b]));
			}
			Sample peakTol = peakMax*PVDR_ENERGY_TOLERANCE;
			int highBin = static_cast<int>(freqToBand(splitFreq));
			int orderSize = 0;
			int heapSize = 0;
			int significant = 0;
			for (int b = 0; b < highBin; ++b) {
				if (pvdrCurrentEnergy[b] > peakTol) {
					pvdrHeap[heapSize++] = b;
					pvdrParent[b] = PVDR_UNPROCESSED;
					++significant;
				} else {
					pvdrParent[b] = PVDR_RANDOM;
				}
			}
			auto lowEnergy = [&](int node) {
				int b = node < bands ? node : node - bands;
				Sample raw = node < bands ? pvdrPreviousEnergy[node] : pvdrCurrentEnergy[node - bands];
				return raw*pvdrSteer_[b];
			};
			auto lowPriority = [&](int a, int c) {
				Sample aEnergy = lowEnergy(a);
				Sample cEnergy = lowEnergy(c);
				if (aEnergy != cEnergy) {
					return aEnergy < cEnergy;
				}
				return a > c;
			};
			std::make_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowPriority);
			auto addLow = [&](int b, int parent) {
				pvdrParent[b] = parent;
				pvdrOrder[orderSize++] = b;
				pvdrHeap[heapSize++] = b + bands;
				std::push_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowPriority);
			};
			while (orderSize < significant) {
				std::pop_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowPriority);
				int node = pvdrHeap[--heapSize];
				int b = node < bands ? node : node - bands;
				if (node < bands) {
					if (pvdrParent[b] == PVDR_UNPROCESSED) {
						addLow(b, PVDR_PREVIOUS);
					}
					continue;
				}
				if (b + 1 < highBin && pvdrParent[b + 1] == PVDR_UNPROCESSED) {
					addLow(b + 1, b);
				}
				if (b > 0 && pvdrParent[b - 1] == PVDR_UNPROCESSED) {
					addLow(b - 1, b);
				}
			}
			int peakCount = 0;
			for (int b = highBin; b < bands; ++b) {
				if (pvdrCurrentEnergy[b] > peakTol && energy[b] > PEAK_SHELTER_RATIO*smoothedEnergy[b]) {
					pvdrHeap[peakCount++] = b;
				}
			}
			std::sort(pvdrHeap.begin(), pvdrHeap.begin() + peakCount, [&](int a, int c) {
				Sample aEnergy = pvdrCurrentEnergy[a]*pvdrSteer_[a];
				Sample cEnergy = pvdrCurrentEnergy[c]*pvdrSteer_[c];
				if (aEnergy != cEnergy) {
					return aEnergy > cEnergy;
				}
				return a < c;
			});
			for (int i = 0; i < peakCount; ++i) {
				int b = pvdrHeap[i];
				pvdrParent[b] = PVDR_PREVIOUS;
				pvdrOrder[orderSize++] = b;
			}
			int scaledCount = 0;
			for (int b = highBin; b < bands; ++b) {
				if (pvdrCurrentEnergy[b] > peakTol && energy[b] > PEAK_SHELTER_RATIO*smoothedEnergy[b]) {
					continue;
				}
				Sample both = std::max(pvdrPreviousEnergy[b], pvdrCurrentEnergy[b]);
				if (both <= peakTol) {
					pvdrParent[b] = PVDR_RANDOM;
					continue;
				}
				int radius = PEAK_SHELTER_RADIUS;
				int loBest = -1;
				int hiBest = -1;
				for (int d = 1; d <= radius; ++d) {
					if (loBest < 0) {
						int lo = b - d;
						if (lo >= highBin) {
							if (pvdrCurrentEnergy[lo] > peakTol && energy[lo] > PEAK_SHELTER_RATIO*smoothedEnergy[lo]) {
								loBest = lo;
							}
						}
					}
					if (hiBest < 0) {
						int hi = b + d;
						if (hi < bands) {
							if (pvdrCurrentEnergy[hi] > peakTol && energy[hi] > PEAK_SHELTER_RATIO*smoothedEnergy[hi]) {
								hiBest = hi;
							}
						}
					}
					if (loBest >= 0 && hiBest >= 0) {
						break;
					}
				}
				if (loBest >= 0 && hiBest < 0) {
					pvdrParent[b] = loBest;
					pvdrHeap[scaledCount++] = b;
				} else if (hiBest >= 0 && loBest < 0) {
					pvdrParent[b] = hiBest;
					pvdrHeap[scaledCount++] = b;
				} else if (loBest >= 0 && hiBest >= 0) {
					Sample loDist = Sample(b - loBest);
					Sample hiDist = Sample(hiBest - b);
					Sample loScore = pvdrCurrentEnergy[loBest] / (loDist * loDist);
					Sample hiScore = pvdrCurrentEnergy[hiBest] / (hiDist * hiDist);
					pvdrParent[b] = (loScore >= hiScore) ? loBest : hiBest;
					pvdrHeap[scaledCount++] = b;
				} else {
					pvdrParent[b] = PVDR_PREVIOUS;
				}
			}
			std::sort(pvdrHeap.begin(), pvdrHeap.begin() + scaledCount, [&](int a, int c) {
				Sample aEnergy = pvdrCurrentEnergy[a]*pvdrSteer_[a];
				Sample cEnergy = pvdrCurrentEnergy[c]*pvdrSteer_[c];
				if (aEnergy != cEnergy) {
					return aEnergy > cEnergy;
				}
				return a < c;
			});
			for (int i = 0; i < scaledCount; ++i) {
				pvdrOrder[orderSize++] = pvdrHeap[i];
			}
			for (int b = highBin; b < bands; ++b) {
				if (pvdrCurrentEnergy[b] > peakTol && energy[b] > PEAK_SHELTER_RATIO*smoothedEnergy[b]) {
					continue;
				}
				if (pvdrParent[b] == PVDR_PREVIOUS) {
					pvdrOrder[orderSize++] = b;
				}
			}
			for (int b = 0; b < bands; ++b) {
				if (pvdrParent[b] == PVDR_RANDOM) {
					pvdrOrder[orderSize++] = b;
				}
			}
			return;
		}
		auto energyForNode = [&](int node) {
			return node < bands ? pvdrPreviousEnergy[node] : pvdrCurrentEnergy[node - bands];
		};
		auto lowerPriority = [&](int a, int b) {
			Sample aEnergy = energyForNode(a);
			Sample bEnergy = energyForNode(b);
			if (aEnergy != bEnergy) {
				return aEnergy < bEnergy;
			}
			return a > b;
		};
		Sample maxEnergy = 0;
		for (int b = 0; b < bands; ++b) {
			maxEnergy = std::max(maxEnergy,
				std::max(pvdrPreviousEnergy[b], pvdrCurrentEnergy[b]));
		}
		Sample absoluteTolerance = maxEnergy*PVDR_ENERGY_TOLERANCE;
		int heapSize = 0;
		int significant = 0;
		for (int b = 0; b < bands; ++b) {
			if (pvdrCurrentEnergy[b] > absoluteTolerance) {
				pvdrHeap[heapSize++] = b;
				pvdrParent[b] = PVDR_UNPROCESSED;
				++significant;
			} else {
				pvdrParent[b] = PVDR_RANDOM;
			}
		}
		int orderSize = 0;
		std::make_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowerPriority);
		auto addCurrent = [&](int b, int parent) {
			pvdrParent[b] = parent;
			pvdrOrder[orderSize++] = b;
			pvdrHeap[heapSize++] = b + bands;
			std::push_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowerPriority);
		};
		while (orderSize < significant) {
			std::pop_heap(pvdrHeap.begin(), pvdrHeap.begin() + heapSize, lowerPriority);
			int node = pvdrHeap[--heapSize];
			int b = node < bands ? node : node - bands;
			if (node < bands) {
				if (pvdrParent[b] == PVDR_UNPROCESSED) {
					addCurrent(b, PVDR_PREVIOUS);
				}
				continue;
			}
			if (b + 1 < bands && pvdrParent[b + 1] == PVDR_UNPROCESSED) {
				addCurrent(b + 1, b);
			}
			if (b > 0 && pvdrParent[b - 1] == PVDR_UNPROCESSED) {
				addCurrent(b - 1, b);
			}
		}
		for (int b = 0; b < bands; ++b) {
			if (pvdrParent[b] == PVDR_RANDOM) {
				pvdrOrder[orderSize++] = b;
			}
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
		Sample stretchDose = std::clamp((smoothTimeFactor_ - Sample(1)) / smoothTimeFactor_, Sample(0), Sample(1));
		Sample onsetNeed = TRANSIENT_ONSET_FLUX_RATIO * (Sample(1) - Sample(0.3) * stretchDose);
		Sample bgNeed = TRANSIENT_BG_RATIO * (Sample(1) - Sample(0.25) * stretchDose);
		for (int region = 0; region < 3; ++region) {
			if (transientCooldown_ > 0) {
				break;
			}
			if (highPower[region] > TRANSIENT_MIN_POWER &&
				highPower[region] > fullPower * TRANSIENT_POWER_FRACTION &&
				highPower[region] > transientBg_[region] * bgNeed &&
				risingEnergy[region] > totalEnergy[region] * onsetNeed) {
				if (transientSamples_ == 0) {
					++transientCount_;
				}
				transientSamples_ = int(stft.blockSamples()/2);
				transientCooldown_ = int(stft.blockSamples()*3/2);
				transientOnset_ = true;
				transientTripped = true;
				break;
			}
		}
		if (!transientTripped) {
			Sample mediumOnsetNeed = onsetNeed * TRANSIENT_MEDIUM_ONSET_SCALE;
			Sample mediumBgNeed = bgNeed * TRANSIENT_MEDIUM_BG_SCALE;
			for (int region = 0; region < 3; ++region) {
				if (transientCooldown_ > 0) {
					break;
				}
				if (highPower[region] > TRANSIENT_MIN_POWER &&
					highPower[region] > fullPower * TRANSIENT_POWER_FRACTION &&
					highPower[region] > transientBg_[region] * mediumBgNeed &&
					risingEnergy[region] > totalEnergy[region] * mediumOnsetNeed) {
					transientSamples_ = int(stft.blockSamples()/4);
					transientCooldown_ = int(stft.blockSamples()/2);
					transientOnset_ = true;
					transientTripped = true;
					break;
				}
			}
		}
		if (!transientTripped && transientCooldown_ == 0 && firstBand > 1) {
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
				transientOnset_ = true;
				transientTripped = true;
			}
		}
		if (!transientBgReady_) {
			if (!transientTripped && transientSamples_ == 0 && transientCooldown_ == 0) {
				for (int region = 0; region < 3; ++region) {
					transientBg_[region] = highPower[region];
				}
				transientBgReady_ = true;
			}
		} else if (transientSamples_ == 0 && transientCooldown_ == 0) {
			Sample upRate = Sample(1)/(Sample(8)*blockProcess.timeFactor);
			Sample downRate = upRate*Sample(0.125);
			for (int region = 0; region < 3; ++region) {
				Sample rate = highPower[region] > transientBg_[region] ? upRate : downRate;
				transientBg_[region] += (highPower[region] - transientBg_[region])*rate;
			}
		}
		if (transientSamples_ == 0) {
			std::fill(transientHold_.begin(), transientHold_.end(), 0);
		} else {
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
		}
	}

	void processSpectrum(size_t step) {
		Sample clampedTimeFactor = std::max<Sample>(blockProcess.timeFactor, 1/maxCleanLow);
		Sample twistTimeFactor = clampedTimeFactor > TRANSIENT_MIN_STRETCH ?
			std::max<Sample>(smoothTimeFactor_, 1/maxCleanLow) : clampedTimeFactor;

		Sample smoothingBins = Sample(stft.fftSamples())/stft.defaultInterval();
		std::uniform_real_distribution<Sample> phaseDist(Sample(-M_PI), Sample(M_PI));
		int splitBin = static_cast<int>(freqToBand(splitFreq));
		int lockSplit = static_cast<int>(freqToBand(transientMinFreq_));

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
				prediction.energy = getFractional<&Band::inputEnergy>(c, lowIndex, fracIndex);
				prediction.energy *= std::max<Sample>(0, mapPoint.freqGrad); // scale the energy according to local stretch factor
				prediction.input = getFractional<&Band::input>(c, lowIndex, fracIndex);
				if (c == 0) {
					pvdrPreviousEnergy[b] = prevEnergy;
					pvdrCurrentEnergy[b] = prediction.energy;
				} else {
					pvdrPreviousEnergy[b] = std::max(pvdrPreviousEnergy[b], prevEnergy);
					pvdrCurrentEnergy[b] = std::max(pvdrCurrentEnergy[b], prediction.energy);
				}

				auto &outputBin = bins[b];
				Complex derivative = getFractional<&Band::derivative>(c, lowIndex, fracIndex);
				Complex timed = getFractional<&Band::timed>(c, lowIndex, fracIndex);
				Sample inputPower = _impl::norm(prediction.input) + tinyFloor;
				Sample timeGradient = -_impl::mul<true>(derivative, prediction.input).imag()
					/inputPower*stft.defaultInterval();
				prediction.frequencyGradient = -Sample(2*M_PI)/stft.fftSamples()
					*_impl::mul<true>(timed, prediction.input).real()/inputPower;
				timeGradient = std::clamp(timeGradient, Sample(-M_PI), Sample(M_PI));
				prediction.frequencyGradient = std::clamp(prediction.frequencyGradient, Sample(-M_PI), Sample(M_PI));
				Sample horizontalAngle = (prediction.timeGradient + timeGradient)*Sample(0.5);
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
			if (chunk == 0) { // YARG phase-2: loudest-first order, propagated to later chunks
				preparePvdrTraversal();
			}
			int startI = int(bands*chunk/splitMainPrediction);
			int endI = int(bands*(chunk + 1)/splitMainPrediction);
			for (int i = startI; i < endI; ++i) {
				int b = pvdrOrder[i];
				// Find maximum-energy channel and calculate that
				int maxChannel = 0;
				Sample maxEnergy = predictionsForChannel(0)[b].energy;
				for (int c = 1; c < channels; ++c) {
					Sample e = predictionsForChannel(c)[b].energy;
					if (e > maxEnergy) {
						maxChannel = c;
						maxEnergy = e;
					}
				}

				auto *predictions = predictionsForChannel(maxChannel);
				auto &prediction = predictions[b];
				auto *bins = bandsForChannel(maxChannel);
				auto &outputBin = bins[b];

				int parent = pvdrParent[b];
				Complex phase;
				const int firstBand = int(freqToBand(transientMinFreq_));
				if (transientOnset_ && (transientHold_[b] != 0 || b >= firstBand)) {
					phase = prediction.input;
				} else if (!blockProcess.mappedFrequencies && b >= splitBin && parent >= 0 && clampedTimeFactor > TRANSIENT_MIN_STRETCH) {
					auto &peakPrediction = predictions[parent];
					auto &peakBin = bins[parent];
					Complex twist = _impl::mul<true>(prediction.input, peakPrediction.input);
					Sample twistNorm = _impl::norm(twist);
					if (twistNorm > Sample(0)) {
						Sample invNorm = Sample(1)/std::sqrt(twistNorm);
						Complex relativeTwist{twist.real()*invNorm, twist.imag()*invNorm};
						phase = _impl::mul(peakBin.output, relativeTwist);
					} else {
						phase = peakBin.output;
					}
				} else if (parent == PVDR_PREVIOUS) {
					phase = outputBin.output;
				} else if (parent == PVDR_RANDOM) {
					phase = std::polar(Sample(1), phaseDist(randomEngine));
				} else {
					auto &parentPrediction = predictions[parent];
					if (!blockProcess.mappedFrequencies && b < splitBin && parent >= 0 &&
						clampedTimeFactor > maxCleanLow) {
						Complex twist = _impl::mul<true>(prediction.input, parentPrediction.input);
						phase = _impl::mul(bins[parent].output, twist)/(prediction.energy + tinyFloor);
					} else {
						Sample binTimeFactor = twistTimeFactor;
						Sample gradient = (prediction.frequencyGradient + parentPrediction.frequencyGradient)*Sample(0.5);
						if (parent > b) {
							gradient = -gradient;
						}
						Complex verticalTwist = std::polar(Sample(1), binTimeFactor*gradient);
						phase = _impl::mul(bins[parent].output, verticalTwist)/
							(prediction.energy + tinyFloor);
					}
				}
				outputBin.output = prediction.makeOutput(phase);
				
				// All other bins are locked in phase
				for (int c = 0; c < channels; ++c) {
					if (c != maxChannel) {
						auto &channelBin = bandsForChannel(c)[b];
						auto &channelPrediction = predictionsForChannel(c)[b];
						if (transientOnset_ && (transientHold_[b] != 0 || b >= firstBand)) {
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
			if (chunk + 1 == splitMainPrediction) {
				transientOnset_ = false;
			}
			return;
		}
	}

	// Shaping stages, called in the order listed in process().
	void applyHarmonicPolish() {
		applyHarmonicPhaseLocking();
		applyPeakNeighborPhaseLocking();
		applySumFrequencyPhaseLocking();
	}

	void applyPhasePolish() {
		applyMainLobePhaseLocking();
		applyChirpPhaseAlignment();
		applyMidSideStereoAlignment();
	}

	void applyVocalBandEnhancement() {
		applyVocalPresence();
		applyVocalBite();
	}

	void applySpectralPeakAndValleyShaping() {
		applyPeakSharpening();
		applyBarkValleyNoiseReduction();
	}

	void applyDecayDamping() {
		applyHfDecayDamping();
		applyReverbTailDamping();
	}

	void applyPostTransientShaping() {
		applyPostTransientMidDamping();
	}

	void applySpectralDynamicsLimiting() {
		applySpectralBoostLimit();
		applySpectralGainDiffusion();
		applyGainFloor();
	}

	void applyHarmonicPhaseLocking() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		std::fill(hblLocked_.begin(), hblLocked_.end(), 0);

		Sample speedDose = getSpeedDose();
		int maxFundBin = std::min(bands - 1, static_cast<int>(hzToBand(Sample(2000))));
		int minFundBin = std::max(2, static_cast<int>(hzToBand(Sample(35))));

		for (int b = minFundBin; b < maxFundBin; ++b) {
			if (hblLocked_[b] != 0) {
				continue;
			}
			if (energy[b] <= energy[b - 1] || energy[b] <= energy[b + 1] || energy[b] <= smoothedEnergy[b] * Sample(3.0)) {
				continue;
			}

			Sample e0 = energy[b - 1];
			Sample e1 = energy[b];
			Sample e2 = energy[b + 1];
			Sample denom = 2 * e1 - e0 - e2;
			Sample delta = denom > Sample(1e-12) ? Sample(0.5) * (e2 - e0) / denom : Sample(0);
			delta = std::clamp(delta, Sample(-0.5), Sample(0.5));
			Sample bStar = Sample(b) + delta;

			int domChannel = 0;
			if (channels > 1 && predictionsForChannel(1)[b].energy > predictionsForChannel(0)[b].energy) {
				domChannel = 1;
			}

			const auto &fundIn = bandsForChannel(domChannel)[b].input;
			const auto &fundOut = bandsForChannel(domChannel)[b].output;
			if (_impl::norm(fundIn) <= Sample(1e-18) || _impl::norm(fundOut) <= Sample(1e-18)) {
				continue;
			}

			Sample phi1In = std::atan2(fundIn.imag(), fundIn.real());
			Sample phi1Out = std::atan2(fundOut.imag(), fundOut.real());

			for (int m = 2; m <= 4; ++m) {
				int targetBin = static_cast<int>(std::round(freqToBand(bandToFreq(bStar) * Sample(m))));
				if (targetBin >= bands - 1 || bandToHz(Sample(targetBin)) > Sample(4000)) {
					break;
				}
				if (hblLocked_[targetBin] != 0) {
					continue;
				}

				int bestK = targetBin;
				Sample bestEnergy = energy[targetBin];
				for (int k = targetBin - 1; k <= targetBin + 1; ++k) {
					if (k > 0 && k < bands && energy[k] > bestEnergy) {
						bestEnergy = energy[k];
						bestK = k;
					}
				}

				if (bestEnergy <= smoothedEnergy[bestK] * Sample(2.0) || bestEnergy <= e1 * Sample(0.005)) {
					continue;
				}
				if (bestK > 0 && bestK + 1 < bands) {
					if (energy[bestK] <= energy[bestK - 1] || energy[bestK] <= energy[bestK + 1]) {
						continue;
					}
				}

				hblLocked_[bestK] = 1;

				const auto &harmIn = bandsForChannel(domChannel)[bestK].input;
				const auto &harmOut = bandsForChannel(domChannel)[bestK].output;
				if (_impl::norm(harmIn) <= Sample(1e-18) || _impl::norm(harmOut) <= Sample(1e-18)) {
					continue;
				}

				Sample phikIn = std::atan2(harmIn.imag(), harmIn.real());
				Sample phikOut = std::atan2(harmOut.imag(), harmOut.real());

				Sample biphaseIn = phikIn - Sample(m) * phi1In;
				Sample biphaseOut = phikOut - Sample(m) * phi1Out;
				Sample error = biphaseOut - biphaseIn;
				error = error - Sample(2 * M_PI) * std::round(error / Sample(2 * M_PI));

				Sample lambda = Sample(0.4) / std::sqrt(Sample(m));
				Sample steerAngle = std::clamp(-error * lambda * speedDose, Sample(-0.3), Sample(0.3));
				Complex rotator = _impl::fastPolar(Sample(1), steerAngle);

				Sample crest = Sample(1) + Sample(0.02) * speedDose * (Sample(1) / Sample(m));
				Complex crestRotator = rotator * crest;

				for (int c = 0; c < channels; ++c) {
					auto *chBands = bandsForChannel(c);
					chBands[bestK].output = _impl::mul(chBands[bestK].output, crestRotator);
				}
			}
		}
	}

	void applyChirpPhaseAlignment() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 3) {
			return;
		}

		Sample speedDose = getSpeedDose();

		int domChannel = 0;
		if (channels > 1 && predictionsForChannel(1)[bands / 4].energy > predictionsForChannel(0)[bands / 4].energy) {
			domChannel = 1;
		}

		auto *domBands = bandsForChannel(domChannel);

		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(Sample(5000))));
		for (int b = 1; b < maxBand; ++b) {
			if (energy[b] <= smoothedEnergy[b] * Sample(0.5)) {
				continue;
			}

			const auto &inPrev = domBands[b - 1].input;
			const auto &inCur = domBands[b].input;
			const auto &inNext = domBands[b + 1].input;

			const auto &outPrev = domBands[b - 1].output;
			const auto &outCur = domBands[b].output;
			const auto &outNext = domBands[b + 1].output;

			if (_impl::norm(inPrev) <= Sample(1e-18) || _impl::norm(inCur) <= Sample(1e-18) || _impl::norm(inNext) <= Sample(1e-18) ||
				_impl::norm(outPrev) <= Sample(1e-18) || _impl::norm(outCur) <= Sample(1e-18) || _impl::norm(outNext) <= Sample(1e-18)) {
				continue;
			}

			Complex inCurConj = std::conj(inCur);
			Complex inCurConjSq = _impl::mul(inCurConj, inCurConj);
			Complex inChirp = _impl::mul(_impl::mul(inNext, inPrev), inCurConjSq);

			Complex outCurConj = std::conj(outCur);
			Complex outCurConjSq = _impl::mul(outCurConj, outCurConj);
			Complex outChirp = _impl::mul(_impl::mul(outNext, outPrev), outCurConjSq);

			Sample inNorm = _impl::norm(inChirp);
			Sample outNorm = _impl::norm(outChirp);
			if (inNorm <= Sample(1e-18) || outNorm <= Sample(1e-18)) {
				continue;
			}

			Complex dispError = _impl::mul<true>(outChirp, inChirp);
			Sample errAngle = std::atan2(dispError.imag(), dispError.real());

			Sample taper = getHighFrequencyTaper(bandToHz(Sample(b)));
			Sample correction = std::clamp(errAngle * Sample(0.2) * speedDose * taper, Sample(-0.25), Sample(0.25));
			Complex rotator = _impl::fastPolar(Sample(1), correction);

			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				bins[b].output = _impl::mul(bins[b].output, rotator);
			}

			++b;
		}
	}

	void applyPeakNeighborPhaseLocking() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 3) {
			return;
		}

		Sample speedDose = getSpeedDose();

		int maxBand = std::min(bands - 2, static_cast<int>(hzToBand(Sample(5000))));
		for (int b = 2; b < maxBand; ++b) {
			if (energy[b] <= energy[b - 1] || energy[b] <= energy[b + 1] || energy[b] <= smoothedEnergy[b] * Sample(2.5)) {
				continue;
			}

			int domChannel = 0;
			if (channels > 1 && predictionsForChannel(1)[b].energy > predictionsForChannel(0)[b].energy) {
				domChannel = 1;
			}

			const auto &inCar = bandsForChannel(domChannel)[b].input;
			const auto &outCar = bandsForChannel(domChannel)[b].output;
			if (_impl::norm(inCar) <= Sample(1e-18) || _impl::norm(outCar) <= Sample(1e-18)) {
				continue;
			}

			Sample taper = getHighFrequencyTaper(bandToHz(Sample(b)));

			const auto &inUp = bandsForChannel(domChannel)[b + 1].input;
			const auto &outUp = bandsForChannel(domChannel)[b + 1].output;
			if (_impl::norm(inUp) > Sample(1e-18) && _impl::norm(outUp) > Sample(1e-18)) {
				Complex inTwist = _impl::mul<true>(inUp, inCar);
				Complex outTwist = _impl::mul<true>(outUp, outCar);
				Complex errPhasor = _impl::mul<true>(outTwist, inTwist);
				Sample errAngle = std::atan2(errPhasor.imag(), errPhasor.real());
				Sample corrAngle = std::clamp(-errAngle * speedDose * Sample(0.35) * taper, Sample(-0.25), Sample(0.25));
				Complex rotator = _impl::fastPolar(Sample(1), corrAngle);
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					bins[b + 1].output = _impl::mul(bins[b + 1].output, rotator);
				}
			}

			const auto &inDown = bandsForChannel(domChannel)[b - 1].input;
			const auto &outDown = bandsForChannel(domChannel)[b - 1].output;
			if (_impl::norm(inDown) > Sample(1e-18) && _impl::norm(outDown) > Sample(1e-18)) {
				Complex inTwist = _impl::mul<true>(inDown, inCar);
				Complex outTwist = _impl::mul<true>(outDown, outCar);
				Complex errPhasor = _impl::mul<true>(outTwist, inTwist);
				Sample errAngle = std::atan2(errPhasor.imag(), errPhasor.real());
				Sample corrAngle = std::clamp(-errAngle * speedDose * Sample(0.35) * taper, Sample(-0.25), Sample(0.25));
				Complex rotator = _impl::fastPolar(Sample(1), corrAngle);
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					bins[b - 1].output = _impl::mul(bins[b - 1].output, rotator);
				}
			}

			++b;
		}
	}

	void applyStereoLock() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels != 2 || bands < 4) {
			return;
		}
		constexpr Sample BLEND_BASE = Sample(0.22);
		constexpr Sample MIN_HZ = Sample(800);
		constexpr Sample FLOOR = Sample(1e-18);
		Sample speedDose = getSpeedDose();
		Sample blend = BLEND_BASE * speedDose;
		int minBand = std::max(1, static_cast<int>(hzToBand(MIN_HZ)));
		if (minBand >= bands) {
			return;
		}
		auto *binsL = bandsForChannel(0);
		auto *binsR = bandsForChannel(1);
		int maxBand = std::min(bands, static_cast<int>(hzToBand(Sample(5000))));
		for (int b = minBand; b < maxBand; ++b) {
			const auto &inL = binsL[b].input;
			const auto &inR = binsR[b].input;
			const auto &outL = binsL[b].output;
			const auto &outR = binsR[b].output;
			Sample nOutL = _impl::norm(outL);
			Sample nOutR = _impl::norm(outR);
			if (nOutL <= FLOOR || nOutR <= FLOOR) {
				continue;
			}
			bool refIsL = nOutL >= nOutR;
			const auto &inRef = refIsL ? inL : inR;
			const auto &inWeak = refIsL ? inR : inL;
			const auto &outRef = refIsL ? outL : outR;
			const auto &outWeak = refIsL ? outR : outL;
			Complex rel = _impl::mul<true>(inWeak, inRef);
			Sample nRel = _impl::norm(rel);
			if (nRel <= FLOOR) {
				continue;
			}
			Complex relUnit = rel / std::sqrt(nRel);
			Complex dir = _impl::mul(outRef, relUnit);
			Sample magWeak = std::sqrt(refIsL ? nOutR : nOutL);
			Sample magRef = std::sqrt(refIsL ? nOutL : nOutR);
			Complex target = dir * (magWeak / magRef);
			Sample taper = getHighFrequencyTaper(bandToHz(Sample(b)));
			Sample localBlend = blend * taper;
			Complex mixed = outWeak * (Sample(1) - localBlend) + target * localBlend;
			Sample nMix = _impl::norm(mixed);
			if (nMix <= FLOOR) {
				continue;
			}
			Complex fixed = mixed * (magWeak / std::sqrt(nMix));
			if (refIsL) {
				binsR[b].output = fixed;
			} else {
				binsL[b].output = fixed;
			}
			hblLocked_[b] = 1;
		}
	}

	void applyVocalPresence() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}
		constexpr Sample BLEND_BASE = Sample(0.28);
		constexpr Sample MIN_HZ = Sample(1000);
		constexpr Sample MAX_HZ = Sample(4500);
		constexpr Sample FLOOR = Sample(1e-18);
		constexpr Sample ENERGY_FLOOR = Sample(1e-15);
		Sample speedDose = getSpeedDose();
		Sample blend = BLEND_BASE * speedDose;
		int minBand = std::max(2, static_cast<int>(hzToBand(MIN_HZ)));
		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(MAX_HZ)));
		if (minBand >= maxBand) {
			return;
		}
		for (int c = 0; c < channels; ++c) {
			auto *bins = bandsForChannel(c);
			for (int pass = 0; pass < 2; ++pass) {
				for (int b = minBand + pass; b < maxBand; b += 2) {
					Sample eCur = energy[b];
				Sample ePrev = energy[b - 1];
				if (hblLocked_[b] != 0) {
					continue;
				}
				if (eCur <= ENERGY_FLOOR || ePrev <= ENERGY_FLOOR) {
						continue;
					}
					if (eCur > energy[b - 1] && eCur > energy[b + 1] && eCur > smoothedEnergy[b] * Sample(1.8)) {
						continue;
					}
					if (ePrev > energy[b - 2] && ePrev > energy[b] && ePrev > smoothedEnergy[b - 1] * Sample(1.8)) {
						continue;
					}
					const auto &inCur = bins[b].input;
					const auto &inPrev = bins[b - 1].input;
					const auto &outCur = bins[b].output;
					const auto &outPrev = bins[b - 1].output;
					Sample nOutCur = _impl::norm(outCur);
					Sample nOutPrev = _impl::norm(outPrev);
					if (nOutCur <= FLOOR || nOutPrev <= FLOOR) {
						continue;
					}
					Complex rel = _impl::mul<true>(inCur, inPrev);
					Sample nRel = _impl::norm(rel);
					if (nRel <= FLOOR) {
						continue;
					}
					Complex relUnit = rel / std::sqrt(nRel);
					Complex dir = _impl::mul(outPrev, relUnit);
					Sample magCur = std::sqrt(nOutCur);
					Sample magPrev = std::sqrt(nOutPrev);
					Complex target = dir * (magCur / magPrev);
					Sample taper = getHighFrequencyTaper(bandToHz(Sample(b)));
					Sample localBlend = blend * taper;
					Complex mixed = outCur * (Sample(1) - localBlend) + target * localBlend;
					Sample nMix = _impl::norm(mixed);
					if (nMix <= FLOOR) {
						continue;
					}
					bins[b].output = mixed * (magCur / std::sqrt(nMix));
					hblLocked_[b] = 1;
				}
			}
		}
	}

	void applyVocalBite() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}
		constexpr Sample BLEND_BASE = Sample(0.28);
		constexpr Sample TARGET_MIN_HZ = Sample(1500);
		constexpr Sample SEARCH_MAX_HZ = Sample(4500);
		constexpr Sample DRIVER_MIN_HZ = Sample(350);
		constexpr Sample FLOOR = Sample(1e-18);
		constexpr int MAX_PEAKS = 12;
		Sample speedDose = getSpeedDose();
		Sample blend = BLEND_BASE * speedDose;
		int minSearch = std::max(2, static_cast<int>(hzToBand(DRIVER_MIN_HZ)));
		int maxSearch = std::min(bands - 1, static_cast<int>(hzToBand(SEARCH_MAX_HZ)));
		int minTarget = std::max(2, static_cast<int>(hzToBand(TARGET_MIN_HZ)));
		if (minSearch >= maxSearch || minTarget >= bands - 1) {
			return;
		}
		int peaks[MAX_PEAKS];
		int numPeaks = 0;
		for (int b = minSearch; b < maxSearch && numPeaks < MAX_PEAKS; ++b) {
			if (energy[b] > energy[b - 1] && energy[b] > energy[b + 1] && energy[b] > smoothedEnergy[b] * Sample(2.2)) {
				peaks[numPeaks++] = b;
			}
		}
		if (numPeaks < 2) {
			return;
		}
		std::sort(peaks, peaks + numPeaks, [&](int a, int b) {
			return energy[a] > energy[b];
		});
		uint64_t locked[64] = {0};
		for (int i = 0; i < numPeaks; ++i) {
			for (int j = i; j < numPeaks; ++j) {
				int targetBin = peaks[i] + peaks[j];
				if (targetBin < minTarget || targetBin >= bands - 1 || targetBin >= 4096 || bandToHz(Sample(targetBin)) > Sample(4800)) {
					continue;
				}
				int bestK = targetBin;
				Sample bestEnergy = energy[targetBin];
				for (int k = targetBin - 1; k <= targetBin + 1; ++k) {
					if (k > 0 && k < bands && energy[k] > bestEnergy) {
						bestEnergy = energy[k];
						bestK = k;
					}
				}
				if (bestK <= 0 || bestK >= bands - 1) {
					continue;
				}
				if (energy[bestK] <= energy[bestK - 1] || energy[bestK] <= energy[bestK + 1]) {
					continue;
				}
				if (bestEnergy <= smoothedEnergy[bestK] * Sample(1.8)) {
					continue;
				}
				int word = bestK >> 6;
				uint64_t mask = uint64_t(1) << (bestK & 63);
				if ((locked[word] & mask) != 0) {
					continue;
				}
				if (hblLocked_[bestK] != 0) {
					continue;
				}
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					const auto &in1 = bins[peaks[i]].input;
					const auto &in2 = bins[peaks[j]].input;
					const auto &in3 = bins[bestK].input;
					const auto &out1 = bins[peaks[i]].output;
					const auto &out2 = bins[peaks[j]].output;
					const auto &out3 = bins[bestK].output;
					Sample nIn = _impl::norm(in1) * _impl::norm(in2) * _impl::norm(in3);
					Sample nOut = _impl::norm(out1) * _impl::norm(out2) * _impl::norm(out3);
					if (nIn <= Sample(1e-24) || nOut <= Sample(1e-24)) {
						continue;
					}
					Complex inProd = _impl::mul(in1, in2);
					Complex inBi = _impl::mul<true>(inProd, in3);
					Complex outProd = _impl::mul(out1, out2);
					Complex outBi = _impl::mul<true>(outProd, out3);
					Complex err = _impl::mul<true>(outBi, inBi);
					Sample nErr = _impl::norm(err);
					if (nErr <= FLOOR) {
						continue;
					}
					Complex fix = std::conj(err / std::sqrt(nErr));
					Sample nOut3 = _impl::norm(out3);
					Complex target = _impl::mul(out3, fix);
					Complex mixed = out3 * (Sample(1) - blend) + target * blend;
					Sample nMix = _impl::norm(mixed);
					if (nMix <= FLOOR) {
						continue;
					}
					bins[bestK].output = mixed * (std::sqrt(nOut3) / std::sqrt(nMix));
				}
				locked[word] |= mask;
			}
		}
	}

	void applyAttackGain() {
		if (blockProcess.mappedFrequencies || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}
		constexpr Sample MAX_GAIN = Sample(1.10);
		constexpr Sample OVERDRIVE_GAIN = Sample(1.25);
		constexpr Sample BASS_HZ = Sample(250);
		constexpr Sample MEDIUM_POWER_RATIO = Sample(1.69);
		constexpr Sample MEDIUM_SMOOTH_RATIO = Sample(0.3);
		constexpr Sample MEDIUM_EXCESS_CAP = Sample(0.2);
		Sample speedDose = getSpeedDose();
		int minBand = std::max(1, static_cast<int>(hzToBand(BASS_HZ)));
		for (int b = minBand; b < bands - 1; ++b) {
			Sample curPower = Sample(0);
			Sample prevPower = Sample(0);
			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				curPower += _impl::norm(bins[b].input);
				prevPower += _impl::norm(bins[b].prevInput);
			}
			bool strong = curPower > prevPower * attackPowerGate_ && curPower > smoothedEnergy[b] * attackSmoothRatio_;
			bool medium = attackMediumSlope_ > Sample(0) && !strong && curPower > prevPower * MEDIUM_POWER_RATIO && curPower > smoothedEnergy[b] * MEDIUM_SMOOTH_RATIO;
			if (!strong && !medium) {
				continue;
			}
			Sample curMag = std::sqrt(curPower);
			Sample prevMag = std::sqrt(prevPower) + Sample(1e-12);
			Sample fluxRatio = curMag / prevMag;
			Sample attackGain = Sample(1);
			if (strong) {
				Sample fluxExcess = std::clamp(fluxRatio - Sample(1.5), Sample(0), Sample(2.0));
				attackGain = Sample(1) + attackSlope_ * speedDose * fluxExcess;
			} else {
				Sample mediumExcess = std::clamp(fluxRatio - Sample(1.3), Sample(0), MEDIUM_EXCESS_CAP);
				attackGain = Sample(1) + attackMediumSlope_ * speedDose * mediumExcess;
			}
			if (attackGain > MAX_GAIN) {
				attackGain = MAX_GAIN + (attackGain - MAX_GAIN) * Sample(0.5);
			}
			attackGain = std::clamp(attackGain, Sample(1), OVERDRIVE_GAIN);
			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= attackGain;
			}
		}
	}

	void applySidelobeLock() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}
		constexpr Sample BLEND_BASE = Sample(0.24);
		constexpr Sample MIN_HZ = Sample(800);
		constexpr Sample FLOOR = Sample(1e-18);
		Sample speedDose = getSpeedDose();
		Sample blend = BLEND_BASE * speedDose;
		int minBand = std::max(1, static_cast<int>(hzToBand(MIN_HZ)));
		if (minBand >= bands) {
			return;
		}
		std::fill(hblLocked_.begin(), hblLocked_.end(), 0);
		for (int i = 0; i < bands; ++i) {
			int b = pvdrOrder[i];
			if (b < minBand) {
				continue;
			}
			int p = pvdrParent[b];
			if (p < 0) {
				continue;
			}
			Sample localBlend = blend;
			if (energy[b] >= smoothedEnergy[b]) {
				if (b == p - 1 || b == p + 1) {
					Sample ratio = std::clamp(energy[b] / (energy[p] + Sample(1e-12)), Sample(0), Sample(1));
					localBlend = blend * Sample(0.5) * (Sample(1) - ratio * Sample(0.5));
				} else {
					continue;
				}
			}
			Sample freqHz = bandToHz(Sample(b));
			Sample highWeight = std::clamp((freqHz - Sample(2000)) / Sample(2000), Sample(0), Sample(1));
			localBlend = std::clamp(localBlend * (Sample(1) + highWeight * Sample(0.5)), Sample(0), Sample(0.35));
			Sample prom = energy[b] / (smoothedEnergy[b] + FLOOR);
			if (b > 0) {
				prom = std::max(prom, energy[b - 1] / (smoothedEnergy[b - 1] + FLOOR));
			}
			if (b + 1 < bands) {
				prom = std::max(prom, energy[b + 1] / (smoothedEnergy[b + 1] + FLOOR));
			}
			Sample tonal = std::clamp((prom - Sample(2)) / Sample(2), Sample(0), Sample(1));
			localBlend *= (Sample(1) - Sample(0.7) * tonal);
			hblLocked_[b] = 1;
			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				const auto &inB = bins[b].input;
				const auto &inP = bins[p].input;
				const auto outB = bins[b].output;
				const auto &outP = bins[p].output;
				Sample nOutB = _impl::norm(outB);
				Sample nOutP = _impl::norm(outP);
				if (nOutB <= FLOOR || nOutP <= FLOOR) {
					continue;
				}
				Complex rel = _impl::mul<true>(inB, inP);
				Sample nRel = _impl::norm(rel);
				if (nRel <= FLOOR) {
					continue;
				}
				Complex relUnit = rel / std::sqrt(nRel);
				Complex dir = _impl::mul(outP, relUnit);
				Sample magOut = std::sqrt(nOutB);
				Sample magPar = std::sqrt(nOutP);
				Complex target = dir * (magOut / magPar);
				Complex mixed = outB * (Sample(1) - localBlend) + target * localBlend;
				Sample nMix = _impl::norm(mixed);
				if (nMix <= FLOOR) {
					continue;
				}
				bins[b].output = mixed * (magOut / std::sqrt(nMix));
			}
		}
	}

	void applySpectralContrastAndAntiRinging() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();

		for (int c = 0; c < channels; ++c) {
			auto *bins = bandsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				Sample e = _impl::norm(bins[b].input);
				Sample smooth = smoothedEnergy[b] + Sample(1e-12);
				if (e >= smooth) {
					continue;
				}

				Sample v = e / smooth;
				Sample x = Sample(1) - v;
				Sample gain = Sample(1) - speedDose * Sample(0.22) * x * (Sample(1) - Sample(0.5) * x);
				gain = std::clamp(gain, Sample(0.88), Sample(1.0));

				bins[b].output *= gain;
			}
		}
	}

	void applySubBassPhaseAlignment() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		int maxBassBin = static_cast<int>(hzToBand(Sample(100)));
		int minBassBin = std::max(1, static_cast<int>(hzToBand(Sample(25))));

		if (maxBassBin <= minBassBin || maxBassBin >= bands) {
			return;
		}

		if (channels > 1) {
			auto *bins0 = bandsForChannel(0);
			auto *bins1 = bandsForChannel(1);
			for (int b = minBassBin; b <= maxBassBin; ++b) {
				Sample freq = bandToHz(Sample(b));
				if (freq >= Sample(100)) {
					break;
				}

				Sample norm0 = _impl::norm(bins0[b].output);
				Sample norm1 = _impl::norm(bins1[b].output);
				if (norm0 <= Sample(1e-18) || norm1 <= Sample(1e-18)) {
					continue;
				}

				Complex stereoTwist = _impl::mul<true>(bins1[b].output, bins0[b].output);
				Sample angleDiff = std::atan2(stereoTwist.imag(), stereoTwist.real());
				Sample weight = (Sample(1) - (freq / Sample(100))) * speedDose * Sample(0.6);
				Complex rotator = _impl::fastPolar(Sample(1), -angleDiff * weight);
				bins1[b].output = _impl::mul(bins1[b].output, rotator);
			}
		}

		int bestB = minBassBin;
		Sample maxEnergy = energy[minBassBin];
		for (int b = minBassBin + 1; b <= maxBassBin; ++b) {
			if (energy[b] > maxEnergy) {
				maxEnergy = energy[b];
				bestB = b;
			}
		}

		if (maxEnergy > smoothedEnergy[bestB] * Sample(1.5) && bestB > 0 && bestB + 1 < bands) {
			Sample e0 = energy[bestB - 1];
			Sample e1 = energy[bestB];
			Sample e2 = energy[bestB + 1];
			Sample denom = 2 * e1 - e0 - e2;
			Sample delta = denom > Sample(1e-12) ? Sample(0.5) * (e2 - e0) / denom : Sample(0);
			delta = std::clamp(delta, Sample(-0.5), Sample(0.5));
			Sample bStar = Sample(bestB) + delta;

			Sample idealAdvance = Sample(2 * M_PI) * bandToFreq(bStar) * Sample(stft.defaultInterval());
			idealAdvance = idealAdvance - Sample(2 * M_PI) * std::round(idealAdvance / Sample(2 * M_PI));

			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				Sample curNorm = _impl::norm(bins[bestB].output);
				Sample prevNorm = _impl::norm(bins[bestB].prevOutput);
				if (curNorm <= Sample(1e-18) || prevNorm <= Sample(1e-18)) {
					continue;
				}

				Complex advanceTwist = _impl::mul<true>(bins[bestB].output, bins[bestB].prevOutput);
				Sample measuredAdvance = std::atan2(advanceTwist.imag(), advanceTwist.real());
				Sample error = measuredAdvance - idealAdvance;
				error = error - Sample(2 * M_PI) * std::round(error / Sample(2 * M_PI));

				Sample damp = -error * speedDose * Sample(0.35);
				Complex flywheelRotator = _impl::fastPolar(Sample(1), damp);

				bins[bestB].output = _impl::mul(bins[bestB].output, flywheelRotator);
				if (bestB > 0 && pvdrParent[bestB - 1] == bestB) {
					bins[bestB - 1].output = _impl::mul(bins[bestB - 1].output, flywheelRotator);
				}
				if (bestB + 1 < bands && pvdrParent[bestB + 1] == bestB) {
					bins[bestB + 1].output = _impl::mul(bins[bestB + 1].output, flywheelRotator);
				}
			}
		}
	}

	void applyInterHopCancellationCompensation() {
		if (!blockProcess.newSpectrum || blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();

		for (int c = 0; c < channels; ++c) {
			auto *bins = bandsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				const auto &outNow = bins[b].output;
				const auto &outPrev = bins[b].prevOutput;

				if (_impl::norm(outNow) <= Sample(1e-18) || _impl::norm(outPrev) <= Sample(1e-18)) {
					continue;
				}

				Complex outAdvance = _impl::mul<true>(outNow, outPrev);
				Sample nominalAdvance = Sample(2 * M_PI) * bandToFreq(b) * Sample(stft.defaultInterval());
				Complex targetAdvance = _impl::fastPolar(Sample(1), nominalAdvance);
				Complex errorPhasor = _impl::mul<true>(outAdvance, targetAdvance);

				Sample normErr = _impl::norm(errorPhasor);
				if (normErr <= Sample(1e-18)) {
					continue;
				}

				Sample cosErr = errorPhasor.real() / std::sqrt(normErr);
				cosErr = std::clamp(cosErr, Sample(-1), Sample(1));

				Sample cancel = Sample(1) - cosErr;
				Sample compGain = Sample(1) + speedDose * Sample(0.06) * cancel;

				bins[b].output *= compGain;
			}
		}
	}

	void applyMidSideStereoAlignment() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels != 2 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		auto *binsL = bandsForChannel(0);
		auto *binsR = bandsForChannel(1);

		int maxBand = std::min(bands, static_cast<int>(hzToBand(Sample(5000))));
		for (int b = 0; b < maxBand; ++b) {
			const auto &inL = binsL[b].input;
			const auto &inR = binsR[b].input;
			const auto &outL = binsL[b].output;
			const auto &outR = binsR[b].output;

			Complex inM = (inL + inR) * Sample(0.5);
			Complex inS = (inL - inR) * Sample(0.5);
			Complex outM = (outL + outR) * Sample(0.5);
			Complex outS = (outL - outR) * Sample(0.5);

			Sample nInM = _impl::norm(inM);
			Sample nInS = _impl::norm(inS);
			Sample nOutM = _impl::norm(outM);
			Sample nOutS = _impl::norm(outS);

			if (nInM <= Sample(1e-18) || nInS <= Sample(1e-18) || nOutM <= Sample(1e-18) || nOutS <= Sample(1e-18)) {
				continue;
			}

			Complex inCross = _impl::mul<true>(inS, inM);
			Complex outCross = _impl::mul<true>(outS, outM);
			Complex errPhasor = _impl::mul<true>(outCross, inCross);

			Sample errAngle = std::atan2(errPhasor.imag(), errPhasor.real());
			Sample taper = getHighFrequencyTaper(bandToHz(Sample(b)));
			Sample corrAngle = -errAngle * speedDose * Sample(0.45) * taper;
			Complex rotator = _impl::fastPolar(Sample(1), corrAngle);

			Sample rIn = nInS / (nInM + Sample(1e-18));
			Sample rOut = nOutS / (nOutM + Sample(1e-18));
			Sample kRatio = std::sqrt(rIn / (rOut + Sample(1e-18)));
			kRatio = std::clamp(kRatio, Sample(0.75), Sample(1.33));
			Sample kScale = Sample(1) + speedDose * Sample(0.35) * (kRatio - Sample(1)) * taper;

			Complex sCorrected = _impl::mul(outS, rotator) * kScale;
			Sample powerScale = std::sqrt((nOutM + nOutS) / (nOutM + _impl::norm(sCorrected)));

			binsL[b].output = (outM + sCorrected) * powerScale;
			binsR[b].output = (outM - sCorrected) * powerScale;
		}
	}

	void applyPeakSharpening() {
		if (transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 5) {
			return;
		}

		Sample speedDose = getSpeedDose();
		Sample qExp = speedDose * Sample(0.12);

		for (int b = 2; b < bands - 2; ++b) {
			if (energy[b] <= energy[b - 1] || energy[b] <= energy[b + 1] || energy[b] <= smoothedEnergy[b] * Sample(2.0)) {
				continue;
			}

			Sample peakEnergy = energy[b];
			Sample lostEnergy = 0;

			for (int offset = -2; offset <= 2; ++offset) {
				if (offset == 0) {
					continue;
				}

				int k = b + offset;
				if (k < 0 || k >= bands) {
					continue;
				}

				if (offset == -2 && energy[k] >= energy[k + 1]) {
					continue;
				}
				if (offset == 2 && energy[k] >= energy[k - 1]) {
					continue;
				}

				Sample skirtEnergy = energy[k];
				if (skirtEnergy <= smoothedEnergy[k] || skirtEnergy >= peakEnergy) {
					continue;
				}

				Sample ratio = (skirtEnergy + Sample(1e-12)) / (peakEnergy + Sample(1e-12));
				Sample g = std::pow(ratio, qExp);
				g = std::clamp(g, Sample(0.80), Sample(1.0));

				lostEnergy += skirtEnergy * (Sample(1) - g * g);

				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					bins[k].output *= g;
				}
			}

			Sample peakBoost = std::sqrt(Sample(1) + lostEnergy / (peakEnergy + Sample(1e-12)));
			peakBoost = std::clamp(peakBoost, Sample(1.0), Sample(1.02));

			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				bins[b].output *= peakBoost;
			}

			b += 1;
		}
	}

	void applyBarkValleyNoiseReduction() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 5) {
			return;
		}

		Sample speedDose = getSpeedDose();
		constexpr int NUM_BARKS = 25;
		Sample barkMaxEnergy[NUM_BARKS] = {0};

		auto hzToBark = [](Sample f) -> int {
			Sample z = Sample(13) * std::atan(Sample(0.00076) * f) + Sample(3.5) * std::atan(f * f * Sample(1.0 / 56250000.0));
			int idx = static_cast<int>(z);
			return std::clamp(idx, 0, NUM_BARKS - 1);
		};

		int minBand = std::max(3, static_cast<int>(hzToBand(Sample(350))));
		int maxBand = std::min(bands - 3, static_cast<int>(hzToBand(Sample(16000))));
		if (minBand >= maxBand) {
			return;
		}

		for (int b = minBand; b < maxBand; ++b) {
			int z = hzToBark(bandToHz(Sample(b)));
			barkMaxEnergy[z] = std::max(barkMaxEnergy[z], energy[b]);
		}

		Sample barkMask[NUM_BARKS];
		for (int z = 0; z < NUM_BARKS; ++z) {
			barkMask[z] = barkMaxEnergy[z];
		}

		for (int z = 1; z < NUM_BARKS; ++z) {
			barkMask[z] = std::max(barkMask[z], barkMask[z - 1] * Sample(0.063));
		}
		for (int z = NUM_BARKS - 2; z >= 0; --z) {
			barkMask[z] = std::max(barkMask[z], barkMask[z + 1] * Sample(0.002));
		}

		for (int b = minBand; b < maxBand; ++b) {
			Sample e = energy[b];
			Sample se = smoothedEnergy[b];
			if (e >= se * Sample(0.40)) {
				continue;
			}

			int z = hzToBark(bandToHz(Sample(b)));
			Sample threshold = barkMask[z] * Sample(0.008);
			if (e >= threshold || threshold <= Sample(1e-16)) {
				continue;
			}

			bool nearHarmonic = false;
			for (int offset = -2; offset <= 2; ++offset) {
				int k = b + offset;
				if (energy[k] > energy[k - 1] && energy[k] > energy[k + 1] && energy[k] > smoothedEnergy[k] * Sample(1.3)) {
					nearHarmonic = true;
					break;
				}
			}
			if (nearHarmonic) {
				continue;
			}

			Sample ratio = e / threshold;
			Sample atten = Sample(1) - speedDose * Sample(0.18) * (Sample(1) - ratio);
			atten = std::clamp(atten, Sample(0.82), Sample(1.0));

			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= atten;
			}
		}
	}

	void applySumFrequencyPhaseLocking() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}

		Sample speedDose = getSpeedDose();
		constexpr int MAX_PEAKS = 24;
		int peaks[MAX_PEAKS];
		int numPeaks = 0;

		int maxSearchBand = std::min(bands - 1, static_cast<int>(hzToBand(Sample(2200))));
		int minSearchBand = std::max(2, static_cast<int>(hzToBand(Sample(350))));

		for (int b = minSearchBand; b < maxSearchBand && numPeaks < MAX_PEAKS; ++b) {
			if (energy[b] > energy[b - 1] && energy[b] > energy[b + 1] && energy[b] > smoothedEnergy[b] * Sample(2.2)) {
				peaks[numPeaks++] = b;
			}
		}

		if (numPeaks < 2) {
			return;
		}

		std::sort(peaks, peaks + numPeaks, [&](int a, int b) {
			return energy[a] > energy[b];
		});

		int domChannel = 0;
		if (channels > 1 && predictionsForChannel(1)[bands / 4].energy > predictionsForChannel(0)[bands / 4].energy) {
			domChannel = 1;
		}
		auto *domBands = bandsForChannel(domChannel);

		uint64_t b3Locked[64] = {0};

		for (int i = 0; i < numPeaks; ++i) {
			int b1 = peaks[i];
			for (int j = i; j < numPeaks; ++j) {
				int b2 = peaks[j];
				int targetBin = b1 + b2;
				if (targetBin >= bands - 1 || targetBin >= 4096 || bandToHz(Sample(targetBin)) > Sample(4400)) {
					continue;
				}

				int bestK = targetBin;
				Sample bestEnergy = energy[targetBin];
				for (int k = targetBin - 1; k <= targetBin + 1; ++k) {
					if (k > 0 && k < bands && energy[k] > bestEnergy) {
						bestEnergy = energy[k];
						bestK = k;
					}
				}

				if (bestK <= 0 || bestK >= bands - 1) {
					continue;
				}
				if (energy[bestK] <= energy[bestK - 1] || energy[bestK] <= energy[bestK + 1]) {
					continue;
				}
				if (bestEnergy <= smoothedEnergy[bestK] * Sample(1.8)) {
					continue;
				}
				if (hblLocked_[bestK] != 0) {
					continue;
				}

				int word = bestK >> 6;
				uint64_t mask = uint64_t(1) << (bestK & 63);
				if ((b3Locked[word] & mask) != 0) {
					continue;
				}

				const auto &in1 = domBands[b1].input;
				const auto &in2 = domBands[b2].input;
				const auto &in3 = domBands[bestK].input;

				const auto &out1 = domBands[b1].output;
				const auto &out2 = domBands[b2].output;
				const auto &out3 = domBands[bestK].output;

				Sample nIn = _impl::norm(in1) * _impl::norm(in2) * _impl::norm(in3);
				Sample nOut = _impl::norm(out1) * _impl::norm(out2) * _impl::norm(out3);
				if (nIn <= Sample(1e-24) || nOut <= Sample(1e-24)) {
					continue;
				}

				Complex inProd = _impl::mul(in1, in2);
				Complex inBi = _impl::mul<true>(inProd, in3);

				Complex outProd = _impl::mul(out1, out2);
				Complex outBi = _impl::mul<true>(outProd, out3);

				Complex errPhasor = _impl::mul<true>(outBi, inBi);
				Sample normErr = _impl::norm(errPhasor);
				if (normErr <= Sample(1e-18)) {
					continue;
				}

				Sample errAngle = std::atan2(errPhasor.imag(), errPhasor.real());
				Sample corrAngle = std::clamp(errAngle * speedDose * Sample(0.30), Sample(-0.15), Sample(0.15));

				Complex rotator = _impl::fastPolar(Sample(1), corrAngle);
				for (int c = 0; c < channels; ++c) {
					bandsForChannel(c)[bestK].output = _impl::mul(bandsForChannel(c)[bestK].output, rotator);
				}

				b3Locked[word] |= mask;
			}
		}
	}

	void applyCausalPreEchoSuppression() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		Sample halfSpan = Sample(stft.blockSamples()) * Sample(0.5);
		if (halfSpan <= Sample(1)) {
			return;
		}

		for (int b = 0; b < bands; ++b) {
			Sample minGate = Sample(1);

			for (int c = 0; c < channels; ++c) {
				const auto *bins = bandsForChannel(c);
				const auto &inNow = bins[b].input;
				const auto &inTimed = bins[b].timed;
				const auto &inPrev = bins[b].prevInput;

				Sample inPower = _impl::norm(inNow);
				Sample prevPower = _impl::norm(inPrev);
				if (inPower <= Sample(1e-18) || prevPower <= Sample(1e-18)) {
					continue;
				}

				Sample onsetFlux = inPower / prevPower;
				if (onsetFlux <= Sample(1.5)) {
					continue;
				}

				Sample timeOffset = _impl::mul<true>(inTimed, inNow).real() / inPower;
				Sample futureRatio = timeOffset / halfSpan;
				if (futureRatio <= Sample(0.15)) {
					continue;
				}

				Sample suppression = speedDose * std::clamp((futureRatio - Sample(0.15)) / Sample(0.5), Sample(0), Sample(1));
				Sample fluxWeight = std::clamp((onsetFlux - Sample(1.5)) / Sample(2.5), Sample(0), Sample(1));
				Sample gate = Sample(1) - suppression * fluxWeight * Sample(0.30);
				gate = std::max(gate, Sample(0.70));

				minGate = std::min(minGate, gate);
			}

			if (minGate < Sample(1)) {
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					bins[b].output *= minGate;
				}
			}
		}
	}

	void applyMainLobePhaseLocking() {
		if (transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}

		Sample speedDose = getSpeedDose();
		Sample lockStrength = speedDose * Sample(0.65);

		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(Sample(5000))));
		for (int b = 1; b < maxBand; ++b) {
			if (energy[b] <= energy[b - 1] || energy[b] <= energy[b + 1] || energy[b] <= smoothedEnergy[b] * Sample(1.8)) {
				continue;
			}

			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				const auto &peakIn = bins[b].input;
				const auto &peakOut = bins[b].output;
				if (_impl::norm(peakIn) <= Sample(1e-18) || _impl::norm(peakOut) <= Sample(1e-18)) {
					continue;
				}

				for (int offset = -2; offset <= 2; ++offset) {
					if (offset == 0) {
						continue;
					}

					int k = b + offset;
					if (k < 0 || k >= bands) {
						continue;
					}

					if (offset == -2 && energy[k] >= energy[k + 1]) {
						continue;
					}
					if (offset == 2 && energy[k] >= energy[k - 1]) {
						continue;
					}

					if (energy[k] <= smoothedEnergy[k] || energy[k] >= energy[b]) {
						continue;
					}

					const auto &skirtIn = bins[k].input;
					const auto &skirtOut = bins[k].output;
					if (_impl::norm(skirtIn) <= Sample(1e-18) || _impl::norm(skirtOut) <= Sample(1e-18)) {
						continue;
					}

					Complex inRel = _impl::mul<true>(skirtIn, peakIn);
					Complex outRel = _impl::mul<true>(skirtOut, peakOut);
					Complex errPhasor = _impl::mul<true>(outRel, inRel);

					Sample normErr = _impl::norm(errPhasor);
					if (normErr <= Sample(1e-18)) {
						continue;
					}

					Sample errAngle = std::atan2(errPhasor.imag(), errPhasor.real());
					Sample taper = getHighFrequencyTaper(bandToHz(Sample(k)));
					Sample corrAngle = -errAngle * lockStrength * taper;
					Complex rotator = _impl::fastPolar(Sample(1), corrAngle);

					bins[k].output = _impl::mul(bins[k].output, rotator);
				}
			}

			b += 1;
		}
	}


	void applyDeesser() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}
		constexpr Sample MIN_HZ = Sample(5000);
		constexpr Sample MAX_HZ = Sample(12000);
		constexpr Sample DEEP_FLOOR = Sample(0.85);
		Sample speedDose = getSpeedDose();
		int minBand = std::max(1, static_cast<int>(hzToBand(MIN_HZ)));
		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(MAX_HZ)));
		if (minBand >= maxBand) {
			return;
		}
		for (int b = minBand; b < maxBand; ++b) {
			Sample e = energy[b];
			Sample se = smoothedEnergy[b] + Sample(1e-12);
			if (e <= se || e <= Sample(1e-14)) {
				continue;
			}
			Sample x = Sample(1) - se / e;
			Sample gain = Sample(1) - speedDose * Sample(0.15) * x;
			gain = std::clamp(gain, DEEP_FLOOR, Sample(1.0));
			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= gain;
			}
		}
	}

	void applyExtremeStretchAttenuation() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(2.2) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		if (speedDose <= Sample(0)) {
			return;
		}

		Sample extremeDose = std::clamp((smoothTimeFactor_ - Sample(2.2)) / Sample(7.0), Sample(0), Sample(1)) * speedDose;

		for (int c = 0; c < channels; ++c) {
			auto *bins = bandsForChannel(c);
			for (int b = 0; b < bands; ++b) {
				Sample inPower = _impl::norm(bins[b].input);
				Sample outPower = _impl::norm(bins[b].output);
				if (inPower <= Sample(1e-18) || outPower <= Sample(1e-18)) {
					continue;
				}

				Sample smooth = smoothedEnergy[b] + Sample(1e-12);

				if (inPower < smooth * Sample(0.85)) {
					Sample ratio = inPower / (smooth * Sample(0.85));
					Sample fogAtten = Sample(1) - extremeDose * Sample(0.28) * (Sample(1) - ratio);
					bins[b].output *= fogAtten;
				}
			}
		}
	}

	void applyDynamicModulationRestoration() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		if (speedDose <= Sample(0)) {
			return;
		}
		Sample exp = speedDose * Sample(0.065);

		for (int b = 0; b < bands; ++b) {
			Sample inPower = energy[b];
			if (inPower <= Sample(1e-16)) {
				continue;
			}

			Sample smooth = smoothedEnergy[b] + Sample(1e-12);
			Sample ratio = inPower / smooth;
			Sample gain = std::pow(ratio, exp);
			gain = std::clamp(gain, Sample(0.86), Sample(1.16));

			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= gain;
			}
		}
	}

	void applyHfDecayDamping() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		if (speedDose <= Sample(0)) {
			return;
		}

		if (blockProcess.newSpectrum) {
			Sample coeff = speedDose * Sample(0.24);
			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				for (int b = 0; b < bands; ++b) {
					Sample curE = _impl::norm(bins[b].input);
					Sample prevE = _impl::norm(bins[b].prevInput);
					if (prevE <= curE || prevE <= Sample(1e-16)) {
						continue;
					}

					Sample decayFlux = (prevE - curE) / (prevE + Sample(1e-12));
					Sample freqNorm = Sample(b) / Sample(bands);
					Sample fSq = freqNorm * freqNorm;

					Sample atten = coeff * fSq * decayFlux;
					Sample g = std::max(Sample(0.76), Sample(1) - atten);

					bins[b].output *= g;
				}
			}
		} else {
			int minDecayBin = static_cast<int>(hzToBand(Sample(4000)));
			Sample coeff = speedDose * Sample(0.015);
			for (int c = 0; c < channels; ++c) {
				auto *bins = bandsForChannel(c);
				for (int b = minDecayBin; b < bands; ++b) {
					Sample freqNorm = Sample(b) / Sample(bands);
					Sample fSq = freqNorm * freqNorm;
					Sample atten = coeff * fSq;
					Sample g = std::max(Sample(0.85), Sample(1) - atten);
					bins[b].output *= g;
				}
			}
		}
	}

	void applySpectralBoostLimit() {
		if (getSpeedDose() <= Sample(0) || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01)) {
			return;
		}

		constexpr Sample BOOST_START = Sample(1.25);
		constexpr Sample BOOST_LIMIT = Sample(2);
		for (int b = 0; b < bands; ++b) {
			Sample referencePower = 0;
			Sample outputPower = 0;
			for (int c = 0; c < channels; ++c) {
				referencePower += predictionsForChannel(c)[b].energy;
				outputPower += _impl::norm(bandsForChannel(c)[b].output);
			}

			Sample threshold = referencePower * BOOST_START;
			if (outputPower <= threshold) {
				continue;
			}

			Sample excess = outputPower - threshold;
			Sample headroom = referencePower * (BOOST_LIMIT - BOOST_START);
			Sample limitedPower = threshold + headroom * (excess / (headroom + excess));
			Sample gain = std::sqrt(limitedPower / outputPower);
			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= gain;
			}
		}
	}

	void applySpectralGainDiffusion() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01)) {
			return;
		}

		Sample speedDose = getSpeedDose();
		if (speedDose <= Sample(0)) {
			return;
		}

		Sample strength = Sample(0.08) * std::clamp(speedDose, Sample(0), Sample(1));
		for (int sweep = 0; sweep < 2; ++sweep) {
			for (int i = 0; i + 1 < bands; ++i) {
				int b = sweep == 0 ? i : bands - 2 - i;
				Sample leftReference = 0;
				Sample rightReference = 0;
				Sample leftPower = 0;
				Sample rightPower = 0;
				for (int c = 0; c < channels; ++c) {
					auto *predictions = predictionsForChannel(c);
					auto *bins = bandsForChannel(c);
					leftReference += predictions[b].energy;
					rightReference += predictions[b + 1].energy;
					leftPower += _impl::norm(bins[b].output);
					rightPower += _impl::norm(bins[b + 1].output);
				}
				if (leftReference <= 0 || rightReference <= 0 || leftPower <= 0 || rightPower <= 0) {
					continue;
				}

				Sample referenceShare = leftReference / (leftReference + rightReference);
				Sample totalPower = leftPower + rightPower;
				Sample outputShare = leftPower / totalPower;
				Sample referenceBlend = Sample(4) * referenceShare * (Sample(1) - referenceShare);
				Sample outputBlend = Sample(4) * outputShare * (Sample(1) - outputShare);
				Sample transfer = strength * referenceBlend * outputBlend * (totalPower * referenceShare - leftPower);
				Sample leftGain = std::sqrt((leftPower + transfer) / leftPower);
				Sample rightGain = std::sqrt((rightPower - transfer) / rightPower);
				for (int c = 0; c < channels; ++c) {
					auto *bins = bandsForChannel(c);
					bins[b].output *= leftGain;
					bins[b + 1].output *= rightGain;
				}
			}
		}
	}

	void applyGainFloor() {
		if (transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands == 0) {
			return;
		}

		Sample speedDose = getSpeedDose();
		if (speedDose <= Sample(0)) {
			return;
		}

		Sample minPower = Sample(0.25) * std::clamp(speedDose, Sample(0), Sample(1));
		constexpr Sample FLOOR = Sample(1e-18);
		for (int b = 0; b < bands; ++b) {
			for (int c = 0; c < channels; ++c) {
				Sample ref = predictionsForChannel(c)[b].energy;
				Sample out = _impl::norm(bandsForChannel(c)[b].output);
				if (ref <= FLOOR || out <= FLOOR) {
					continue;
				}
				Sample floorPower = ref * minPower;
				if (out < floorPower) {
					bandsForChannel(c)[b].output *= std::sqrt(floorPower / out);
				}
			}
		}
	}

	void applyReverbTailDamping() {
		if (blockProcess.mappedFrequencies || transientSamples_ > 0 || transientCooldown_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}

		Sample speedDose = getSpeedDose();
		int minBand = std::max(1, static_cast<int>(hzToBand(Sample(120))));
		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(Sample(6500))));
		if (minBand >= maxBand) {
			return;
		}

		for (int b = minBand; b < maxBand; ++b) {
			Sample freq = bandToHz(Sample(b));
			Sample wLow = std::clamp((freq - Sample(120)) * Sample(1.0 / 130.0), Sample(0), Sample(1));
			Sample wHigh = std::clamp((Sample(6500) - freq) * Sample(1.0 / 2000.0), Sample(0), Sample(1));
			Sample wFreq = wLow * wHigh;

			Sample e = energy[b];
			Sample smooth = smoothedEnergy[b] + Sample(1e-12);
			Sample etaSpectral = e > smooth ? (e - smooth) / (e + Sample(1e-12)) : Sample(0);
			etaSpectral = std::clamp(etaSpectral, Sample(0), Sample(1));

			Sample eta = etaSpectral;
			if (channels == 2) {
				const auto &inL = bandsForChannel(0)[b].input;
				const auto &inR = bandsForChannel(1)[b].input;
				Complex cross = _impl::mul<true>(inL, inR);
				Sample crossMag = std::sqrt(_impl::norm(cross));
				Sample totalPower = (_impl::norm(inL) + _impl::norm(inR)) * Sample(0.5) + Sample(1e-12);
				Sample gammaBinaural = std::clamp(crossMag / totalPower, Sample(0), Sample(1));
				eta = std::max(etaSpectral, gammaBinaural);
			}

			Sample diffuseFactor = Sample(1) - eta;
			Sample maxAtten = speedDose * Sample(0.24) * wFreq;
			Sample gain = std::max(Sample(0.76), Sample(1) - diffuseFactor * maxAtten);

			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= gain;
			}
		}
	}

	void applyPostTransientMidDamping() {
		if (blockProcess.mappedFrequencies || transientCooldown_ <= 0 || transientSamples_ > 0 || smoothTimeFactor_ <= Sample(1.01) || channels == 0 || bands < 4) {
			return;
		}

		Sample speedDose = getSpeedDose();
		int minBand = std::max(1, static_cast<int>(hzToBand(Sample(260))));
		int maxBand = std::min(bands - 1, static_cast<int>(hzToBand(Sample(1350))));
		if (minBand >= maxBand) {
			return;
		}

		Sample expVal = speedDose * Sample(0.32);

		for (int b = minBand; b < maxBand; ++b) {
			if (energy[b] > smoothedEnergy[b] * Sample(2.5)) {
				if (b > 0 && b + 1 < bands && energy[b] > energy[b - 1] && energy[b] > energy[b + 1]) {
					continue;
				}
			}

			Sample curPower = Sample(0);
			Sample prevPower = Sample(0);
			for (int c = 0; c < channels; ++c) {
				curPower += _impl::norm(bandsForChannel(c)[b].input);
				prevPower += _impl::norm(bandsForChannel(c)[b].prevInput);
			}

			if (curPower >= prevPower || prevPower <= Sample(1e-16)) {
				continue;
			}

			Sample decayRatio = std::max(Sample(1e-6), curPower / prevPower);
			Sample g = std::pow(decayRatio, expVal);
			g = std::clamp(g, Sample(0.78), Sample(1.0));

			Sample freq = bandToHz(Sample(b));
			Sample wLow = std::clamp((freq - Sample(260)) * Sample(1.0 / 100.0), Sample(0), Sample(1));
			Sample wHigh = std::clamp((Sample(1350) - freq) * Sample(1.0 / 250.0), Sample(0), Sample(1));
			Sample w = wLow * wHigh;

			Sample finalGain = Sample(1) - w * (Sample(1) - g);

			for (int c = 0; c < channels; ++c) {
				bandsForChannel(c)[b].output *= finalGain;
			}
		}
	}


	
	// Produces smoothed energy across all channels
	static constexpr size_t smoothEnergySteps = 3;
	Sample smoothEnergyState = 0;
	Sample smoothTimeFactor_ = 1;
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
