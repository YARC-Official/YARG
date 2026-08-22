#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ManagedBass;
using UnityEditor;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Input;
using YARG.Playback;
using YARG.Settings;
using YARG.Song;

namespace YARG.Editor
{
    public sealed partial class AudioDebugWindow
    {
        private void UpdateFft(double now, double dt)
        {
            int fftSize = 1 << _fftSizeLog;
            int binCount = fftSize / 2;

            if (_fftBuffer == null || _fftBuffer.Length != binCount)
            {
                _fftBuffer = new float[binCount];
                _smoothedFft = new float[binCount];
                _peakFft = new float[binCount];
                for (int i = 0; i < binCount; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }

            bool isPlaying = _bassSong != null && !_bassSong.IsPaused;

            if (isPlaying && !_freezeGraph)
            {
                int bytesRead = _bassSong!.GetFFTData(_fftBuffer, _fftSizeLog, false);
                _lastFftBytesRead = bytesRead;
                if (bytesRead > 0)
                {
                    int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
                    float nyquist = sampleRate * 0.5f;
                    float freqPerBin = nyquist / binCount;

                    float maxMag = 0f;
                    int maxBin = 0;
                    double weightedFreqSum = 0;
                    double totalMagSum = 0;

                    float smooth = Mathf.Clamp01(_fftSmoothingFactor);
                    float peakDecay = _fftPeakDecayRate * (float) dt;

                    for (int i = 0; i < binCount; i++)
                    {
                        float rawMag = _fftBuffer[i];
                        _smoothedFft![i] = (_smoothedFft[i] * smooth) + (rawMag * (1f - smooth));
                        float curMag = _smoothedFft[i];

                        float db = 20f * Mathf.Log10(Mathf.Max(curMag, 1e-6f));

                        if (db > _peakFft![i])
                        {
                            _peakFft[i] = db;
                        }
                        else
                        {
                            _peakFft[i] = Mathf.Max(_fftMinDb, _peakFft[i] - peakDecay);
                        }

                        if (curMag > maxMag)
                        {
                            maxMag = curMag;
                            maxBin = i;
                        }

                        float freq = i * freqPerBin;
                        weightedFreqSum += freq * curMag;
                        totalMagSum += curMag;
                    }

                    if (maxMag > 1e-4f)
                    {
                        _dominantFrequencyHz = maxBin * freqPerBin;
                        _dominantDb = 20f * Mathf.Log10(Mathf.Max(maxMag, 1e-6f));

                        if (_dominantFrequencyHz >= 20f)
                        {
                            float midi = FreqToMidi(_dominantFrequencyHz);
                            int roundedMidi = (int) MathF.Round(midi);
                            int noteIndex = ((roundedMidi % 12) + 12) % 12;
                            int octave = (roundedMidi / 12) - 1;
                            _dominantNoteName = $"{NOTE_NAMES[noteIndex]}{octave}";
                            _dominantCents = (midi - roundedMidi) * 100f;
                        }
                        else
                        {
                            _dominantNoteName = "--";
                            _dominantCents = 0f;
                        }
                    }
                    else
                    {
                        _dominantFrequencyHz = 0f;
                        _dominantDb = _fftMinDb;
                        _dominantNoteName = "--";
                        _dominantCents = 0f;
                    }

                    _spectralCentroidHz = totalMagSum > 1e-5 ? (float) (weightedFreqSum / totalMagSum) : 0f;

                    for (int b = 0; b < _fftBands.Length; b++)
                    {
                        float minF = _fftBands[b].MinFreq;
                        float maxF = _fftBands[b].MaxFreq;
                        int startBin = Math.Clamp((int) (minF / freqPerBin), 0, binCount - 1);
                        int endBin = Math.Clamp((int) (maxF / freqPerBin), startBin, binCount - 1);

                        float bandMax = 0f;
                        for (int i = startBin; i <= endBin; i++)
                        {
                            if (_smoothedFft![i] > bandMax)
                            {
                                bandMax = _smoothedFft[i];
                            }
                        }

                        float bandDb = 20f * Mathf.Log10(Mathf.Max(bandMax, 1e-6f));
                        _fftBands[b].CurrentDb = bandDb;
                        if (bandDb > _fftBands[b].PeakDb)
                        {
                            _fftBands[b].PeakDb = bandDb;
                        }
                        else
                        {
                            _fftBands[b].PeakDb = Mathf.Max(_fftMinDb, _fftBands[b].PeakDb - peakDecay);
                        }
                    }
                }
            }
            else if (!isPlaying && _smoothedFft != null && _peakFft != null)
            {
                float decay = (float) (dt * 15f);
                float peakDecay = _fftPeakDecayRate * (float) dt;
                for (int i = 0; i < _smoothedFft.Length; i++)
                {
                    _smoothedFft[i] = Mathf.Max(0f, _smoothedFft[i] - decay);
                    _peakFft[i] = Mathf.Max(_fftMinDb, _peakFft[i] - peakDecay);
                }
                for (int b = 0; b < _fftBands.Length; b++)
                {
                    _fftBands[b].CurrentDb = Mathf.Max(_fftMinDb, _fftBands[b].CurrentDb - peakDecay);
                    _fftBands[b].PeakDb = Mathf.Max(_fftMinDb, _fftBands[b].PeakDb - peakDecay);
                }
            }

            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                Repaint();
            }
        }

    }
}
