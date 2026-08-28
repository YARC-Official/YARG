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
        private void UpdateMicrophone(double now, double dt)
        {
            EnsureDefaultMicSlot();

            bool anyFrame = false;
            bool anySolo = _micSlots.Any(s => s.Solo);

            for (int s = 0; s < _micSlots.Count; s++)
            {
                var slot = _micSlots[s];
                if (slot.ActiveDevice == null)
                {
                    continue;
                }

                slot.ActiveDevice.IsRecordingOutput = true;

                // Apply Mute / Solo to live monitoring
                float effectiveVol = anySolo
                    ? (slot.Solo ? slot.MonitoringVolume : 0f)
                    : (slot.Mute ? 0f : slot.MonitoringVolume);
                slot.ActiveDevice.SetMonitoringLevel(effectiveVol);

                while (slot.ActiveDevice.DequeueOutputFrame(out var frame))
                {
                    anyFrame = true;
                    slot.FramesReceived++;
                    slot.FpsFrameCount++;

                    if (slot.LastFrameTime > 0)
                    {
                        slot.FrameIntervalMs = (now - slot.LastFrameTime) * 1000.0;
                    }
                    slot.LastFrameTime = now;

                    if (frame.IsHit)
                    {
                        slot.LastHitTime = now;
                        slot.TotalHitCount++;
                    }
                    else
                    {
                        if (frame.Pitch > 0)
                        {
                            slot.CurrentPitchHz = frame.Pitch;
                            slot.CurrentMidi = frame.PitchAsMidiNote;
                            slot.CurrentDb = frame.Volume;
                            slot.IsVoiced = true;

                            int roundedMidi = (int) MathF.Round(slot.CurrentMidi);
                            int noteIndex = ((roundedMidi % 12) + 12) % 12;
                            int octave = (roundedMidi / 12) - 1;
                            slot.CurrentNoteName = $"{NOTE_NAMES[noteIndex]}{octave}";
                            slot.CurrentCents = (slot.CurrentMidi - roundedMidi) * 100f;
                        }
                        else
                        {
                            slot.CurrentDb = frame.Volume;
                            slot.IsVoiced = false;
                        }
                    }
                }

                if (now - slot.LastFrameTime > 0.15)
                {
                    slot.IsVoiced = false;
                    slot.CurrentDb = Mathf.Lerp(slot.CurrentDb, -160f, (float) (dt * 8.0));
                }

                if (slot.CurrentDb > slot.PeakDb)
                {
                    slot.PeakDb = slot.CurrentDb;
                    slot.PeakHoldDb = slot.CurrentDb;
                    slot.LastPeakHoldTime = now;
                }
                else
                {
                    slot.PeakDb = Mathf.Lerp(slot.PeakDb, slot.CurrentDb, (float) (dt * 5.0));
                    if (now - slot.LastPeakHoldTime > 1.0)
                    {
                        slot.PeakHoldDb = Mathf.Lerp(slot.PeakHoldDb, slot.CurrentDb, (float) (dt * 2.0));
                    }
                }

                if (now - slot.LastFpsTime >= 1.0)
                {
                    slot.Fps = (float) (slot.FpsFrameCount / (now - slot.LastFpsTime));
                    slot.FpsFrameCount = 0;
                    slot.LastFpsTime = now;
                }

                if (!_freezeGraph && now - slot.LastSampleTime >= SAMPLE_INTERVAL)
                {
                    if (now - slot.LastSampleTime > SAMPLE_INTERVAL * 4.0 || slot.LastSampleTime <= 0)
                    {
                        slot.LastSampleTime = now - SAMPLE_INTERVAL;
                    }
                    slot.LastSampleTime += SAMPLE_INTERVAL;

                    slot.Samples.Add(new MicSample
                    {
                        RealTime = now,
                        MidiNote = slot.IsVoiced ? slot.CurrentMidi : 0f,
                        VolumeDb = slot.CurrentDb,
                        IsHit = (now - slot.LastHitTime) < 0.06,
                        IsVoiced = slot.IsVoiced
                    });

                    if (slot.Samples.Count > MAX_SAMPLES)
                    {
                        slot.Samples.RemoveAt(0);
                    }
                }
            }

            UpdateMonitoringGcState();

            if (anyFrame || _selectedBottomTab == 3 || _graphMode == GraphMode.MicPitchAndHits)
            {
                Repaint();
            }
        }

        private void EnsureDefaultMicSlot()
        {
            if (_micSlots.Count == 0)
            {
                var slot = new MicSlot
                {
                    Id = 1,
                    DisplayLabel = "Mic 1",
                    ThemeColor = MIC_SLOT_COLORS[0]
                };
                _micSlots.Add(slot);
                _selectedMicSlotIndex = 0;

                if (_availableMicDevices.Count > 0)
                {
                    ConnectMicSlot(slot, _availableMicDevices[0]);
                }
            }
        }

        private MicSlot AddMicSlot(InputDeviceInfo? device = null)
        {
            int nextId = 1;
            while (_micSlots.Any(s => s.Id == nextId))
            {
                nextId++;
            }

            Color color = MIC_SLOT_COLORS[(nextId - 1) % MIC_SLOT_COLORS.Length];
            var slot = new MicSlot
            {
                Id = nextId,
                DisplayLabel = $"Mic {nextId}",
                ThemeColor = color
            };
            _micSlots.Add(slot);
            _selectedMicSlotIndex = _micSlots.Count - 1;

            if (device.HasValue)
            {
                ConnectMicSlot(slot, device.Value);
            }

            Repaint();
            return slot;
        }

        private void RemoveMicSlot(int index)
        {
            if (index < 0 || index >= _micSlots.Count)
            {
                return;
            }

            var slot = _micSlots[index];
            slot.Dispose();
            _micSlots.RemoveAt(index);

            if (_micSlots.Count == 0)
            {
                EnsureDefaultMicSlot();
            }
            else
            {
                _selectedMicSlotIndex = Math.Clamp(_selectedMicSlotIndex, 0, _micSlots.Count - 1);
            }

            Repaint();
        }

        private void ShowAddMicDeviceMenu()
        {
            RefreshAvailableMicrophones();
            var menu = new GenericMenu();

            if (_availableMicDevices.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Microphones Found"));
                menu.ShowAsContext();
                return;
            }

            var (sharedMics, wasapiMics, asioMics) = GroupMicrophones();

            foreach (var device in sharedMics)
            {
                string devName = device.DisplayName;
                bool alreadyUsed = _micSlots.Any(s => s.SelectedDevice?.DisplayName == devName && s.ActiveDevice != null);
                string suffix = alreadyUsed ? " (In use)" : "";
                var captured = device;
                menu.AddItem(new GUIContent($"Shared (DirectSound\\/WASAPI)/{devName}{suffix}"), false, () =>
                {
                    AddMicSlot(captured);
                });
            }

            foreach (var device in wasapiMics)
            {
                string devName = device.DisplayName;
                string displayName = CleanDeviceName(devName);
                bool alreadyUsed = _micSlots.Any(s => s.SelectedDevice?.DisplayName == devName && s.ActiveDevice != null);
                string suffix = alreadyUsed ? " (In use)" : "";
                var captured = device;
                menu.AddItem(new GUIContent($"WASAPI Exclusive (Low Latency)/{displayName}{suffix}"), false, () =>
                {
                    AddMicSlot(captured);
                });
            }

            foreach (var device in asioMics)
            {
                string devName = device.DisplayName;
                string displayName = CleanDeviceName(devName);
                bool alreadyUsed = _micSlots.Any(s => s.SelectedDevice?.DisplayName == devName && s.ActiveDevice != null);
                string suffix = alreadyUsed ? " (In use)" : "";
                var captured = device;
                menu.AddItem(new GUIContent($"ASIO (Low Latency)/{displayName}{suffix}"), false, () =>
                {
                    AddMicSlot(captured);
                });
            }

            menu.ShowAsContext();
        }

        private void ShowSlotDeviceMenu(MicSlot slot)
        {
            RefreshAvailableMicrophones();
            var menu = new GenericMenu();

            if (_availableMicDevices.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Microphones Found"));
                menu.ShowAsContext();
                return;
            }

            var (sharedMics, wasapiMics, asioMics) = GroupMicrophones();

            foreach (var device in sharedMics)
            {
                string devName = device.DisplayName;
                bool isCurrent = slot.ActiveDevice != null && slot.SelectedDevice?.DisplayName == devName;
                var captured = device;
                menu.AddItem(new GUIContent($"Shared (DirectSound\\/WASAPI)/{devName}"), isCurrent, () =>
                {
                    ConnectMicSlot(slot, captured);
                });
            }

            foreach (var device in wasapiMics)
            {
                string devName = device.DisplayName;
                string displayName = CleanDeviceName(devName);
                bool isCurrent = slot.ActiveDevice != null && slot.SelectedDevice?.DisplayName == devName;
                var captured = device;
                menu.AddItem(new GUIContent($"WASAPI Exclusive (Low Latency)/{displayName}"), isCurrent, () =>
                {
                    ConnectMicSlot(slot, captured);
                });
            }

            foreach (var device in asioMics)
            {
                string devName = device.DisplayName;
                string displayName = CleanDeviceName(devName);
                bool isCurrent = slot.ActiveDevice != null && slot.SelectedDevice?.DisplayName == devName;
                var captured = device;
                menu.AddItem(new GUIContent($"ASIO (Low Latency)/{displayName}"), isCurrent, () =>
                {
                    ConnectMicSlot(slot, captured);
                });
            }

            menu.ShowAsContext();
        }

        private (List<InputDeviceInfo> Shared, List<InputDeviceInfo> Wasapi, List<InputDeviceInfo> Asio)
            GroupMicrophones()
        {
            var shared = new List<InputDeviceInfo>();
            var wasapi = new List<InputDeviceInfo>();
            var asio = new List<InputDeviceInfo>();

            foreach (var device in _availableMicDevices)
            {
                if (device.DisplayName.StartsWith("WASAPI: ", StringComparison.OrdinalIgnoreCase))
                {
                    wasapi.Add(device);
                }
                else if (device.DisplayName.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase))
                {
                    asio.Add(device);
                }
                else
                {
                    shared.Add(device);
                }
            }

            return (shared, wasapi, asio);
        }

        private void RefreshAvailableMicrophones()
        {
            try
            {
                _availableMicDevices.Clear();
                _availableMicDevices.AddRange(GlobalAudioHandler.GetAllInputDevices());
            }
            catch (Exception ex)
            {
                var activeSlot = ActiveMicSlot;
                if (activeSlot != null)
                {
                    activeSlot.StatusMessage = $"Failed to scan input devices: {ex.Message}";
                    activeSlot.StatusIsError = true;
                    activeSlot.LastStatusTime = EditorApplication.timeSinceStartup;
                }
            }
        }

        private void ConnectMicSlot(MicSlot slot, InputDeviceInfo device)
        {
            DisconnectMicSlot(slot);

            try
            {
                slot.ActiveDevice = GlobalAudioHandler.CreateInputDevice(device);
                if (slot.ActiveDevice == null)
                {
                    slot.StatusMessage = $"Failed to initialize input '{device.DisplayName}'.";
                    slot.StatusIsError = true;
                    slot.LastStatusTime = EditorApplication.timeSinceStartup;
                    return;
                }

                slot.SelectedDevice = device;
                slot.ActiveDevice.IsRecordingOutput = true;
                slot.ActiveDevice.SetMonitoringLevel(!slot.Mute ? slot.MonitoringVolume : 0f);
                slot.StatusMessage = $"Connected to '{device.DisplayName}'";
                slot.StatusIsError = false;
                slot.LastStatusTime = EditorApplication.timeSinceStartup;
            }
            catch (Exception ex)
            {
                slot.StatusMessage = $"Microphone error: {ex.Message}";
                slot.StatusIsError = true;
                slot.LastStatusTime = EditorApplication.timeSinceStartup;
            }

            Repaint();
        }

        private void DisconnectMicSlot(MicSlot slot)
        {
            StopMicRecording(slot);
            slot.DetachRecordingChannel();
            slot.DisposePlayback();

            if (slot.ActiveDevice != null)
            {
                slot.ActiveDevice.Dispose();
                slot.ActiveDevice = null;
            }

            slot.CurrentDb = -160f;
            slot.PeakDb = -160f;
            slot.PeakHoldDb = -160f;
            slot.CurrentPitchHz = 0f;
            slot.CurrentMidi = 0f;
            slot.CurrentNoteName = "--";
            slot.CurrentCents = 0f;
            slot.IsVoiced = false;
            slot.Fps = 0f;
            slot.FpsFrameCount = 0;
            Repaint();
        }

        private void DisposeAllMicSlots()
        {
            for (int i = 0; i < _micSlots.Count; i++)
            {
                _micSlots[i].Dispose();
            }
            _micSlots.Clear();
            _fallbackMicSamples.Clear();
            RestoreGcMode();
        }

    }
}
