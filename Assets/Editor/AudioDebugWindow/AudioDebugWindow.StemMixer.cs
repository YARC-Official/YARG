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
        private void DrawStemMixerCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(120)))
            {
                int channelCount = _bassSong?.Channels?.Count ?? 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Stem Mixer ({channelCount} Channels)", EditorStyles.boldLabel);

                    if (channelCount > 0)
                    {
                        bool anyReverb = _stemReverbs.Values.Any(r => r);
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = anyReverb ? new Color(0.4f, 0.8f, 1f, 1f) : prevBg;
                        if (GUILayout.Button(anyReverb ? "★ All Reverb ON" : "☆ All Reverb OFF", EditorStyles.miniButton, GUILayout.Width(125)))
                        {
                            ToggleAllReverb(!anyReverb);
                        }
                        GUI.backgroundColor = prevBg;

                        if (GUILayout.Button("Reset All", EditorStyles.miniButton, GUILayout.Width(75)))
                        {
                            ResetStemControls();
                        }
                    }
                }

                EditorGUILayout.Space(4);

                if (_bassSong?.Channels == null || !_bassSong.Channels.Any())
                {
                    EditorGUILayout.LabelField("Load a multi-track song folder to mix individual stems.", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                bool anySolo = _stemSolos.Values.Any(s => s);
                var distinctStems = _bassSong.Channels.Select(c => c.Stem).Distinct();

                foreach (var stem in distinctStems)
                {
                    if (!_stemVolumes.ContainsKey(stem))
                    {
                        _stemVolumes[stem] = 1f;
                        _stemMutes[stem] = false;
                        _stemSolos[stem] = false;
                        _stemReverbs[stem] = false;
                    }

                    float currentVol = _stemVolumes[stem];
                    bool isMuted = _stemMutes[stem];
                    bool isSolo = _stemSolos[stem];
                    bool isReverb = _stemReverbs.TryGetValue(stem, out bool r) && r;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(stem.ToString(), EditorStyles.label, GUILayout.Width(75));

                        EditorGUI.BeginChangeCheck();
                        float newVol = GUILayout.HorizontalSlider(currentVol, 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _stemVolumes[stem] = newVol;
                            UpdateStemVolume(stem, anySolo);
                        }

                        EditorGUILayout.LabelField($"{(int) (newVol * 100f)}%", EditorStyles.miniLabel, GUILayout.Width(36));

                        var prevBg = GUI.backgroundColor;

                        GUI.backgroundColor = isMuted ? new Color(0.95f, 0.25f, 0.25f, 1f) : Color.white;
                        if (GUILayout.Button("M", EditorStyles.miniButtonLeft, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            _stemMutes[stem] = !_stemMutes[stem];
                            UpdateAllStemVolumes();
                            Repaint();
                        }

                        GUI.backgroundColor = isSolo ? new Color(1f, 0.75f, 0.1f, 1f) : Color.white;
                        if (GUILayout.Button("S", EditorStyles.miniButtonMid, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            bool isMultiSelect = Event.current.shift || Event.current.control;
                            if (isMultiSelect)
                            {
                                _stemSolos[stem] = !_stemSolos[stem];
                            }
                            else
                            {
                                int soloCount = _stemSolos.Values.Count(s => s);
                                bool alreadySolo = _stemSolos[stem];
                                if (alreadySolo && soloCount == 1)
                                {
                                    _stemSolos[stem] = false;
                                }
                                else
                                {
                                    foreach (var key in _stemSolos.Keys.ToList())
                                    {
                                        _stemSolos[key] = false;
                                    }
                                    _stemSolos[stem] = true;
                                }
                            }

                            UpdateAllStemVolumes();
                            Repaint();
                        }

                        GUI.backgroundColor = isReverb ? new Color(0.4f, 0.8f, 1f, 1f) : Color.white;
                        if (GUILayout.Button(new GUIContent("R", "Toggle Reverb (Starpower FX)"), EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            SetStemReverb(stem, !isReverb);
                            Repaint();
                        }

                        GUI.backgroundColor = prevBg;
                    }
                }
            }
        }

        private void DrawBufferPresetPill(int ms)
        {
            bool isActive = _readAheadBufferMs == ms;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button($"{ms}ms", EditorStyles.miniButton, GUILayout.Width(50), GUILayout.Height(18)))
            {
                SetBufferPreset(ms);
            }

            GUI.backgroundColor = prevBg;
        }

        private void SetBufferPreset(int bufferMs)
        {
            _readAheadBufferMs = bufferMs;
            ApplyReadAheadBuffer(_readAheadBufferMs);
        }

        private void ApplyReadAheadBuffer(int bufferMs)
        {
            GlobalAudioHandler.SetBufferLength(bufferMs);
            _bassSong?.SetReadAheadBuffer(bufferMs);
        }

        private void ToggleAllReverb(bool enable)
        {
            if (_bassSong?.Channels != null && _bassSong.Channels.Count > 0)
            {
                var distinctStems = _bassSong.Channels.Select(c => c.Stem).Distinct();
                foreach (var stem in distinctStems)
                {
                    SetStemReverb(stem, enable);
                }
            }
            else
            {
                foreach (var stem in ALL_STEMS)
                {
                    SetStemReverb(stem, enable);
                }
            }
            Repaint();
        }

        private void SetStemReverb(SongStem stem, bool enable)
        {
            if (stem == SongStem.Master)
            {
                return;
            }

            _stemReverbs[stem] = enable;
            GlobalAudioHandler.SetReverbSetting(stem, enable);
        }

        private void ResetStemControls()
        {
            StemSettings.ApplySettings = true;
            _stemVolumes.Clear();
            _stemMutes.Clear();
            _stemSolos.Clear();
            _stemReverbs.Clear();

            foreach (var stem in ALL_STEMS)
            {
                GlobalAudioHandler.SetVolumeSetting(stem, 1.0);
                GlobalAudioHandler.SetReverbSetting(stem, false);
            }

            if (_bassSong?.Channels == null)
            {
                return;
            }

            foreach (var channel in _bassSong.Channels)
            {
                _stemVolumes[channel.Stem] = 1f;
                _stemMutes[channel.Stem] = false;
                _stemSolos[channel.Stem] = false;
                _stemReverbs[channel.Stem] = false;
            }

            Repaint();
        }

        private void UpdateAllStemVolumes()
        {
            StemSettings.ApplySettings = true;
            bool anySolo = _stemSolos.Values.Any(s => s);

            foreach (var stem in ALL_STEMS)
            {
                if (_stemVolumes.ContainsKey(stem))
                {
                    UpdateStemVolume(stem, anySolo);
                }
                else
                {
                    GlobalAudioHandler.SetVolumeSetting(stem, anySolo ? 0.0 : 1.0);
                }
            }
        }

        private void UpdateStemVolume(SongStem stem, bool anySolo)
        {
            if (stem == SongStem.Master)
            {
                return;
            }

            StemSettings.ApplySettings = true;
            bool isMuted = _stemMutes.TryGetValue(stem, out bool m) && m;
            bool isSolo = _stemSolos.TryGetValue(stem, out bool s) && s;
            float baseVol = _stemVolumes.TryGetValue(stem, out float v) ? v : 1f;

            double effectiveVol;
            if (anySolo)
            {
                effectiveVol = isSolo && !isMuted ? baseVol : 0.0;
            }
            else
            {
                effectiveVol = isMuted ? 0.0 : baseVol;
            }

            GlobalAudioHandler.SetVolumeSetting(stem, effectiveVol);
        }

    }
}
