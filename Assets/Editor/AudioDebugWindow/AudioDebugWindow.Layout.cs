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
        private void HandleDragAndDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0)
            {
                return;
            }

            string path = DragAndDrop.paths[0];
            bool isDirectory = Directory.Exists(path);
            bool isFile = File.Exists(path);

            if (!isDirectory && !isFile)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (isDirectory)
                {
                    LoadSongFolder(path);
                }
                else
                {
                    LoadAudioFile(path);
                }
                evt.Use();
            }
        }

        private void HandleKeyboardShortcuts()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Space)
            {
                TogglePlayPause();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.LeftArrow && evt.control)
            {
                JumpRelative(-5.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.RightArrow && evt.control)
            {
                JumpRelative(5.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.LeftArrow)
            {
                JumpRelative(-1.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                JumpRelative(1.0);
                evt.Use();
            }
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isLoaded = _bassSong != null;
                    bool isPlaying = _bassSong?.IsPaused == false;

                    var prevBg = GUI.backgroundColor;
                    if (isLoaded && isPlaying)
                    {
                        GUI.backgroundColor = new Color(0.18f, 0.78f, 0.38f, 1f);
                        GUILayout.Label("● PLAYING", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    else if (isLoaded)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                        GUILayout.Label("⏸ PAUSED", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.40f, 0.45f, 0.52f, 1f);
                        GUILayout.Label("⏹ STOPPED", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(6);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = isLoaded ? Color.white : new Color(0.7f, 0.75f, 0.8f) }
                    };
                    EditorGUILayout.LabelField(new GUIContent(_loadedSongName, _sourcePath ?? _loadedSongName), titleStyle, GUILayout.Height(20));

                    GUILayout.FlexibleSpace();

                    string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                    var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
                    string cleanDev = CleanDeviceName(currentDevice);
                    string devButtonLabel = currentMode switch
                    {
                        AudioOutputMode.Asio => $"⚡ ASIO: {cleanDev} ▾",
                        AudioOutputMode.WasapiExclusive => $"⚡ WASAPI: {cleanDev} ▾",
                        _ => $"🔊 {cleanDev} ▾"
                    };

                    if (GUILayout.Button(devButtonLabel, EditorStyles.miniButton, GUILayout.Height(20), GUILayout.MaxWidth(240)))
                    {
                        ShowDeviceMenu();
                    }

                    GUILayout.Space(4);

                    if (GUILayout.Button("Open Audio ▾", EditorStyles.miniButton, GUILayout.Height(20), GUILayout.Width(105)))
                    {
                        ShowAudioMenu();
                    }

                    if (GUILayout.Button("Force GC", EditorStyles.miniButton, GUILayout.Height(20), GUILayout.Width(70)))
                    {
                        ForceGarbageCollection();
                    }

                    GUI.enabled = !string.IsNullOrEmpty(_sourcePath);
                    if (GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Height(20), GUILayout.Width(60)))
                    {
                        EditorUtility.RevealInFinder(_sourcePath);
                    }
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    int sampleRate = Bass.Info.SampleRate;
                    int speakers = Bass.Info.SpeakerCount;
                    string activeDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                    string cleanActive = CleanDeviceName(activeDevice);
                    var mode = GlobalAudioHandler.GetOutputMode(activeDevice);
                    string modeLabel = mode switch
                    {
                        AudioOutputMode.Asio => "ASIO",
                        AudioOutputMode.WasapiExclusive => "WASAPI Exclusive",
                        _ => "Shared"
                    };
                    double latencyMs = GlobalAudioHandler.PlaybackLatency;
                    var bufferInfo = GlobalAudioHandler.GetOutputBufferInfo();
                    string bufferStr = bufferInfo is { } bInfo && bInfo.PreferredLength > 0 ? $" • {bInfo.PreferredLength} spl" : string.Empty;

                    string specBadge = $"{sampleRate} Hz  •  {speakers} ch  •  {latencyMs:F1} ms latency{bufferStr}";
                    string shortPath;
                    if (string.IsNullOrEmpty(_sourcePath))
                    {
                        shortPath = "Drag & drop audio file or folder to load";
                    }
                    else
                    {
                        shortPath = Path.GetFileName(_sourcePath);
                        string? dir = Path.GetDirectoryName(_sourcePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string dirName = Path.GetFileName(dir);
                            shortPath = $".../{dirName}/{shortPath}";
                        }
                    }

                    var metaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.6f, 0.65f, 0.72f) }
                    };
                    var specStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        normal = { textColor = new Color(0.75f, 0.82f, 0.92f) },
                        alignment = TextAnchor.MiddleRight
                    };

                    EditorGUILayout.LabelField(new GUIContent($"📁 {shortPath}", _sourcePath), metaStyle);
                    EditorGUILayout.LabelField(specBadge, specStyle, GUILayout.Width(310));
                }
            }
        }

        private static string CleanDeviceName(string? rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return "Default";
            }

            string name = rawName!.Trim();
            while (name.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("ASIO:", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("WASAPI: ", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("WASAPI:", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(name.IndexOf(':') + 1).Trim();
            }

            return name;
        }

        private void ShowAudioMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Browse Audio File..."), false, () =>
            {
                string path = EditorUtility.OpenFilePanel("Select Audio File", "", "ogg,opus,mp3,wav,aiff,mogg,sng");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadAudioFile(path);
                }
            });

            menu.AddItem(new GUIContent("Browse Song Folder..."), false, () =>
            {
                string path = EditorUtility.OpenFolderPanel("Select Song Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadSongFolder(path);
                }
            });

            menu.AddItem(new GUIContent("Song Library Drawer"), _showLibrarySection, () =>
            {
                _showLibrarySection = !_showLibrarySection;
            });

            if (_recentPaths.Count > 0)
            {
                menu.AddSeparator("");
                for (int i = 0; i < _recentPaths.Count; i++)
                {
                    string p = _recentPaths[i];
                    string name = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(name))
                    {
                        name = p;
                    }

                    menu.AddItem(new GUIContent($"Recent/{i + 1}. {name}"), false, () =>
                    {
                        if (Directory.Exists(p))
                        {
                            LoadSongFolder(p);
                        }
                        else if (File.Exists(p))
                        {
                            LoadAudioFile(p);
                        }
                    });
                }

                menu.AddItem(new GUIContent("Recent/Clear History"), false, () =>
                {
                    _recentPaths.Clear();
                    EditorPrefs.DeleteKey(RECENT_PATHS_KEY);
                });
            }

            menu.ShowAsContext();
        }

        private void ShowDeviceMenu()
        {
            var menu = new GenericMenu();
            var allDevices = GlobalAudioHandler.GetAllOutputDevices();
            string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";

            var sharedDevices = allDevices.Where(d => GlobalAudioHandler.GetOutputMode(d.name) == AudioOutputMode.Shared).ToList();
            var wasapiDevices = allDevices.Where(d => GlobalAudioHandler.GetOutputMode(d.name) == AudioOutputMode.WasapiExclusive).ToList();
            var asioDevices = allDevices.Where(d => GlobalAudioHandler.GetOutputMode(d.name) == AudioOutputMode.Asio).ToList();

            foreach (var device in sharedDevices)
            {
                string devName = device.name;
                bool isCurrent = devName == currentDevice;
                menu.AddItem(new GUIContent($"Shared (DirectSound\\/WASAPI)/{devName}"), isCurrent, () =>
                {
                    SwitchOutputDevice(devName);
                });
            }

            if (wasapiDevices.Count > 0)
            {
                foreach (var device in wasapiDevices)
                {
                    string devName = device.name;
                    string displayName = CleanDeviceName(devName);
                    bool isCurrent = devName == currentDevice;
                    menu.AddItem(new GUIContent($"WASAPI Exclusive (Low Latency)/{displayName}"), isCurrent, () =>
                    {
                        SwitchOutputDevice(devName);
                    });
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("WASAPI Exclusive (Low Latency)/No WASAPI Devices Found"));
            }

            if (asioDevices.Count > 0)
            {
                foreach (var device in asioDevices)
                {
                    string devName = device.name;
                    string displayName = CleanDeviceName(devName);
                    bool isCurrent = devName == currentDevice;
                    menu.AddItem(new GUIContent($"ASIO (Low Latency)/{displayName}"), isCurrent, () =>
                    {
                        SwitchOutputDevice(devName);
                    });
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("ASIO (Low Latency)/No ASIO Drivers Found"));
            }

            menu.ShowAsContext();
        }

        private void SwitchOutputDevice(string deviceName)
        {
            bool success;
            if (SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.OutputDevice.Value = deviceName;
                string active = SettingsManager.Settings.OutputDevice.Value;
                success = active == deviceName;
            }
            else
            {
                success = GlobalAudioHandler.SetOutputDevice(deviceName);
            }

            string activeDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
            if (!success)
            {
                _deviceStatusMessage = $"Failed to switch to '{deviceName}'. Active device: {activeDevice}";
                _deviceStatusIsError = true;
            }
            else
            {
                _deviceStatusMessage = $"Active device: {activeDevice}";
                _deviceStatusIsError = false;
            }

            _lastDeviceStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OpenAsioControlPanel()
        {
            if (SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.OpenAsioControlPanel();
            }
            else
            {
                GlobalAudioHandler.OpenOutputControlPanel();
                GlobalAudioHandler.ReinitializeOutput();
            }

            Repaint();
        }

        private void RestartOutput()
        {
            bool restarted = GlobalAudioHandler.ReinitializeOutput();
            _deviceStatusMessage = restarted ? "Output driver reinitialized successfully." : "Failed to reinitialize output driver.";
            _deviceStatusIsError = !restarted;
            _lastDeviceStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void DrawLibraryDrawer()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Song Library", EditorStyles.boldLabel, GUILayout.Width(100));
                    _librarySearch = EditorGUILayout.TextField(_librarySearch);

                    if (GUILayout.Button("Scan Songs", GUILayout.Width(90), GUILayout.Height(19)))
                    {
                        _ = SongContainer.RunRefresh(false);
                    }

                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(19)))
                    {
                        _showLibrarySection = false;
                    }
                }

                var songs = SongContainer.Songs;
                if (songs == null || songs.Length == 0)
                {
                    EditorGUILayout.HelpBox("No songs currently indexed in SongContainer. Scan your song folders in YARG or click 'Scan Songs'.", MessageType.Info);
                    return;
                }

                var filtered = string.IsNullOrEmpty(_librarySearch)
                    ? songs.Take(40)
                    : songs.Where(s => s.Name.Original.Contains(_librarySearch, StringComparison.OrdinalIgnoreCase) ||
                                       s.Artist.Original.Contains(_librarySearch, StringComparison.OrdinalIgnoreCase)).Take(40);

                using var scroll = new EditorGUILayout.ScrollViewScope(_libraryScroll, GUILayout.Height(120));
                _libraryScroll = scroll.scrollPosition;

                foreach (var song in filtered)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{song.Artist.Original} - {song.Name.Original}", EditorStyles.label);
                        if (GUILayout.Button("Load", GUILayout.Width(60), GUILayout.Height(18)))
                        {
                            LoadSongEntry(song);
                        }
                    }
                }
            }
        }

        private void DrawTransportBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool isLoaded = _bassSong != null;
                bool isPlaying = _bassSong?.IsPaused == false;
                double currentPos = _bassSong?.GetPosition() ?? 0;
                double totalLength = _bassSong?.Length ?? 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prevBg = GUI.backgroundColor;

                    GUI.enabled = isLoaded;
                    if (isPlaying)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                        if (GUILayout.Button("⏸ Pause", EditorStyles.miniButton, GUILayout.Width(78), GUILayout.Height(22)))
                        {
                            PauseSong();
                        }
                    }
                    else
                    {
                        GUI.backgroundColor = isLoaded ? new Color(0.2f, 0.78f, 0.35f, 1f) : prevBg;
                        if (GUILayout.Button("▶ Play", EditorStyles.miniButton, GUILayout.Width(78), GUILayout.Height(22)))
                        {
                            PlaySong();
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("⏹ Stop", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(22)))
                    {
                        StopSong();
                    }

                    GUILayout.Space(6);

                    if (GUILayout.Button("-5s", EditorStyles.miniButtonLeft, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(-5.0);
                    if (GUILayout.Button("-1s", EditorStyles.miniButtonMid, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(-1.0);
                    if (GUILayout.Button("+1s", EditorStyles.miniButtonMid, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(1.0);
                    if (GUILayout.Button("+5s", EditorStyles.miniButtonRight, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(5.0);

                    GUILayout.Space(6);

                    var timeStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.9f, 0.93f, 0.96f) }
                    };

                    EditorGUILayout.LabelField($"{FormatTime(currentPos)} / {FormatTime(totalLength)}", timeStyle, GUILayout.Width(130), GUILayout.Height(22));

                    GUILayout.Space(4);

                    GUI.enabled = isLoaded && totalLength > 0;
                    float displayPos = _isScrubbing ? _scrubTarget : (float) currentPos;
                    EditorGUI.BeginChangeCheck();
                    float newPos = GUILayout.HorizontalSlider(displayPos, 0f, Mathf.Max(0.1f, (float) totalLength), GUILayout.Height(22));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _isScrubbing = true;
                        _scrubTarget = newPos;
                    }

                    if (_isScrubbing && Event.current.type == EventType.MouseUp)
                    {
                        _isScrubbing = false;
                        SeekSong(_scrubTarget);
                    }

                    GUI.enabled = true;
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Speed", EditorStyles.miniBoldLabel, GUILayout.Width(40));
                    DrawSpeedPill(0.5f, EditorStyles.miniButtonLeft);
                    DrawSpeedPill(0.75f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.0f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.25f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.5f, EditorStyles.miniButtonRight);

                    GUILayout.Space(4);
                    float newSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 2.5f, GUILayout.Width(85));
                    if (Mathf.Abs(newSpeed - _playbackSpeed) > 0.001f)
                    {
                        SetPlaybackSpeed(newSpeed);
                    }

                    if (GUILayout.Button("1x", EditorStyles.miniButton, GUILayout.Width(32)))
                    {
                        SetPlaybackSpeed(1f);
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("🔊 Volume", EditorStyles.miniBoldLabel, GUILayout.Width(62));
                    float newVol = EditorGUILayout.Slider(_volume, 0f, 1f, GUILayout.Width(100));
                    if (Mathf.Abs(newVol - _volume) > 0.001f)
                    {
                        _volume = newVol;
                        _bassSong?.SetVolume(_volume);
                    }
                    EditorGUILayout.LabelField($"{(int)(_volume * 100)}%", EditorStyles.miniLabel, GUILayout.Width(32));
                }
            }
        }

        private void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            if (_bassSong == null)
            {
                return;
            }

            double currentInputSystemTime = InputManager.CurrentInputTime;
            double currentPos = _bassSong.GetPosition();
            _inputTimeOffset = currentInputSystemTime - ((currentPos - _simulatedClockDisturbance) / _playbackSpeed);

            if (_audioSynchronizer != null && _modelSongSync)
            {
                _audioSynchronizer.ChangeSongSpeed(_playbackSpeed);
            }
            else
            {
                _bassSong.SetPlaybackSpeed(_playbackSpeed);
            }
        }

        private void DrawSpeedPill(float speed, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_playbackSpeed, speed);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.25f, 0.65f, 1f, 1f);
            }

            if (GUILayout.Button($"{speed:0.##}x", style, GUILayout.Width(46), GUILayout.Height(18)))
            {
                SetPlaybackSpeed(speed);
            }

            GUI.backgroundColor = prevBg;
        }

    }
}
