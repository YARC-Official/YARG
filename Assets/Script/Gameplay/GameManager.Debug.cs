using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Gameplay.Player;
using YARG.Integration;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private const int DEBUG_WINDOW_ID = 0;
        private const int DEBUG_WINDOW_MARGIN = 25;
        private const int DEBUG_WINDOW_WIDTH = 350;

        private bool _enableDebug;

        private GUIStyle _verticalGroupStyle;

        private GUI.WindowFunction _windowCallback;
        private Rect _windowRect = new(DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_WIDTH, 0);

        private List<(string title, Action callback)> _debugMenus;
        private string[] _debugMenuTitles;
        private int _debugMenuIndex = -1;

        private string[] _debugPlayers;
        private int _debugSelectedPlayer = -1;

        // Needed because of non-static methods being used as delegates
        private void InitializeDebugGUI()
        {
            // Box style doesn't account for the title text,
            // so window style it is
            _verticalGroupStyle = GUI.skin.window;

            _windowCallback = WindowCallback;
            _debugMenus = new()
            {
                ("Player", PlayerDebug),
                ("Timing", TimingDebug),
                ("Venue",  VenueDebug),

                ("Close",  CloseDebug),
            };
            _debugMenuTitles = _debugMenus.Select((s) => s.title).ToArray();

            var debugPlayers = new List<string>();
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                debugPlayers.Add($"{i + 1}: {player.Player.Profile.Name}");
            }
            _debugPlayers = debugPlayers.ToArray();
        }

        private void SetDebugEnabled(bool enabled)
        {
            _enableDebug = enabled;
            // GUI layout is toggled since merely having it enabled causes needless memory allocations
            useGUILayout = enabled;
        }

        private void ToggleDebugEnabled() => SetDebugEnabled(_enableDebug);

        private void OnGUI()
        {
            if (!_enableDebug || _windowCallback == null)
            {
                // We're either not fully initialized or something has gone out of sync, force-disable
                SetDebugEnabled(false);
                return;
            }

            // Reset size so that resizing from auto-layouts don't persist across menus
            _windowRect.size = new Vector2(DEBUG_WINDOW_WIDTH, 0);
            _windowRect = GUILayout.Window(DEBUG_WINDOW_ID, _windowRect, _windowCallback, "Debug Menu");
        }

        private void WindowCallback(int windowId)
        {
            _debugMenuIndex = GUILayout.Toolbar(_debugMenuIndex, _debugMenuTitles);
            if (_debugMenuIndex >= 0)
                _debugMenus[_debugMenuIndex].callback();

            GUI.DragWindow();
        }

        private void CloseDebug()
        {
            SetDebugEnabled(false);
            _debugMenuIndex = -1;
            _windowRect.position = new Vector2(DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN);
        }

        private void PlayerDebug()
        {
            GUILayout.BeginVertical("Player Selection", _verticalGroupStyle);
            int buttonStride = 50 / _debugPlayers.Max((p) => p.Length);
            _debugSelectedPlayer = GUILayout.SelectionGrid(_debugSelectedPlayer, _debugPlayers, buttonStride);
            GUILayout.EndVertical();

            if (_debugSelectedPlayer < 0 || _debugSelectedPlayer >= _players.Count)
                return;

            var player = _players[_debugSelectedPlayer];

            GUILayout.BeginVertical("Base Engine", _verticalGroupStyle);
            {
                var engine = player.BaseEngine;
                var state = engine.BaseState;
                var stats = engine.BaseStats;

                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Note index: {0}\n", state.NoteIndex);
                text.AppendFormat("Star Power: {0:0.0000}\n", stats.StarPowerBarAmount);
                text.AppendFormat("Star Power ticks: {0}\n", stats.StarPowerTickAmount);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();

            switch (player)
            {
                case FiveFretPlayer fiveFretPlayer:
                {
                    GUILayout.BeginVertical("Five Fret Guitar", _verticalGroupStyle);
                    {
                        var engine = fiveFretPlayer.Engine;
                        var state = engine.State;
                        var stats = engine.EngineStats;

                        using var text = ZString.CreateStringBuilder(true);

                        text.AppendFormat("Buttons: {0}\n", state.ButtonMask);

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    }
                    GUILayout.EndVertical();
                    break;
                }

                case DrumsPlayer drumsPlayer:
                {
                    GUILayout.BeginVertical("Drums", _verticalGroupStyle);
                    {
                        var engine = drumsPlayer.Engine;
                        var state = engine.State;
                        var stats = engine.EngineStats;

                        using var text = ZString.CreateStringBuilder(true);

                        text.Append("Drums not handled yet");

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    }
                    GUILayout.EndVertical();
                    break;
                }

                case VocalsPlayer vocalsPlayer:
                {
                    GUILayout.BeginVertical("Vocals", _verticalGroupStyle);
                    {
                        var engine = vocalsPlayer.Engine;
                        var state = engine.State;
                        var stats = engine.EngineStats;

                        using var text = ZString.CreateStringBuilder(true);

                        text.Append("Vocals not handled yet");

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    }
                    GUILayout.EndVertical();
                    break;
                }

                default:
                    GUILayout.Label($"Player type {player.GetType()} not handled yet");
                    break;
            }
        }

        private void TimingDebug()
        {
            GUILayout.BeginVertical("Calibration", _verticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Audio calibration: {0}ms\n", _songRunner.AudioCalibration);
                text.AppendFormat("Video calibration: {0}ms\n", _songRunner.VideoCalibration);
                text.AppendFormat("Song offset: {0}ms\n", _songRunner.SongOffset);
                text.AppendFormat("Device audio latency: {0}ms\n", GlobalAudioHandler.PlaybackLatency);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Time", _verticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Song time: {0:0.000000}\n", _songRunner.SongTime);
                text.AppendFormat("Audio time: {0:0.000000}\n", _songRunner.AudioTime);
                text.AppendFormat("Visual time: {0:0.000000}\n", _songRunner.VisualTime);
                text.AppendFormat("Input time: {0:0.000000}\n", _songRunner.InputTime);
                text.AppendLine();
                text.AppendFormat("Real song time: {0:0.000000}\n", _songRunner.RealSongTime);
                text.AppendFormat("Real audio time: {0:0.000000}\n", _songRunner.RealAudioTime);
                text.AppendFormat("Real visual time: {0:0.000000}\n", _songRunner.RealVisualTime);
                text.AppendFormat("Real input time: {0:0.000000}\n", _songRunner.RealInputTime);
                text.AppendLine();
                text.AppendFormat("Input base: {0:0.000000}\n", _songRunner.InputTimeBase);
                text.AppendFormat("Input offset: {0:0.000000}\n", _songRunner.InputTimeOffset);
                text.AppendFormat("Pause time: {0:0.000000}\n", _songRunner.PauseStartTime);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Sync", _verticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Audio/visual difference: {0:0.000000}\n", _songRunner.SyncDelta);
                text.AppendFormat("Resync start delta: {0:0.000000}\n", _songRunner.SyncStartDelta);
                text.AppendFormat("Resync worst delta: {0:0.000000}\n", _songRunner.SyncWorstDelta);
                text.AppendFormat("Speed adjustment: {0:0.00}\n", _songRunner.SyncSpeedAdjustment);
                text.AppendFormat("Speed multiplier: {0}\n", _songRunner.SyncSpeedMultiplier);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();
        }

        private void VenueDebug()
        {
            GUILayout.BeginVertical("Lighting", _verticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Lighting index: {0:000}/{1:000}\n",
                    MasterLightingGameplayMonitor.LightingIndex,
                    MasterLightingGameplayMonitor.Venue.Lighting.Count
                );

                // Explicit check instead of using ?, as nullable enum types are not specially
                // formatted by ZString to avoid allocations (while non-nullable enums are)
                if (MasterLightingController.CurrentLightingCue != null)
                    text.AppendFormat("Lighting event: {0}\n", MasterLightingController.CurrentLightingCue.Type);
                else
                    text.Append("Lighting event: None\n");

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();
        }
    }
}