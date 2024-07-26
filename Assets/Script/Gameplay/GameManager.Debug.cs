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
        private const int DEBUG_WINDOW_WIDTH = 400;
        private const int DEBUG_WINDOW_ACTIVE_HEIGHT = 500;
        private const int DEBUG_WINDOW_INACTIVE_HEIGHT = 50;

        private bool _enableDebug;

        // Box style doesn't account for the title text, so window style it is
        private GUIStyle VerticalGroupStyle => GUI.skin.window;

        private GUI.WindowFunction _debugWindowCallback;
        private Rect _debugWindowRect = new(
            DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN,
            DEBUG_WINDOW_WIDTH, DEBUG_WINDOW_INACTIVE_HEIGHT
        );
        private Vector2 _debugWindowScroll;

        private List<(string title, Action callback)> _debugMenus;
        private string[] _debugMenuTitles;
        private int _debugMenuIndex = -1;

        // Needed because of non-static methods being used as delegates
        private void InitializeDebugGUI()
        {
            _debugWindowCallback = WindowCallback;
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

        private void ToggleDebugEnabled() => SetDebugEnabled(!_enableDebug);

        private void OnGUI()
        {
            if (!_enableDebug || _debugWindowCallback == null)
            {
                // We're either not fully initialized or something has gone out of sync, force-disable
                SetDebugEnabled(false);
                return;
            }

            _debugWindowRect = GUI.Window(DEBUG_WINDOW_ID, _debugWindowRect, _debugWindowCallback, "Debug Menu");
        }

        private void WindowCallback(int windowId)
        {
            _debugMenuIndex = GUILayout.Toolbar(_debugMenuIndex, _debugMenuTitles);
            if (_debugMenuIndex >= 0)
            {
                _debugWindowRect.size = new Vector2(DEBUG_WINDOW_WIDTH, DEBUG_WINDOW_ACTIVE_HEIGHT);
                _debugWindowScroll = GUILayout.BeginScrollView(_debugWindowScroll);
                {
                    _debugMenus[_debugMenuIndex].callback();
                }
                GUILayout.EndScrollView();
            }
            else
            {
                _debugWindowRect.size = new Vector2(DEBUG_WINDOW_WIDTH, DEBUG_WINDOW_INACTIVE_HEIGHT);
            }

            GUI.DragWindow();
        }

        private void CloseDebug()
        {
            SetDebugEnabled(false);
            _debugMenuIndex = -1;
            _debugWindowRect.size = new Vector2(DEBUG_WINDOW_WIDTH, DEBUG_WINDOW_INACTIVE_HEIGHT);
            _debugWindowRect.position = new Vector2(DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN);
        }

        private string[] _debugPlayers;
        private int _debugSelectedPlayer = -1;

        private void PlayerDebug()
        {
            GUILayout.BeginVertical("Player Selection", VerticalGroupStyle);
            int buttonStride = 50 / _debugPlayers.Max((p) => p.Length);
            _debugSelectedPlayer = GUILayout.SelectionGrid(_debugSelectedPlayer, _debugPlayers, buttonStride);
            GUILayout.EndVertical();

            if (_debugSelectedPlayer < 0 || _debugSelectedPlayer >= _players.Count)
                return;

            var player = _players[_debugSelectedPlayer];

            GUILayout.BeginVertical("Base Engine", VerticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                var state = player.BaseEngine.BaseState;
                text.AppendLine("State:");
                text.AppendFormat("- Current tick: {0}\n", state.CurrentTick);
                text.AppendFormat("- Current time: {0:0.000000}\n", state.CurrentTime);
                text.AppendFormat("- Last tick: {0}\n", state.LastTick);
                text.AppendFormat("- Last time: {0:0.000000}\n", state.LastUpdateTime);
                if (state.LastQueuedInputTime != double.MinValue)
                    text.AppendFormat("- Last queued input time: {0:0.000000}\n", state.LastQueuedInputTime);
                else
                    text.Append("- Last queued input time: None\n");
                text.AppendLine();
                text.AppendFormat("- Note index: {0}\n", state.NoteIndex);
                text.AppendFormat("- Solo index: {0}\n", state.CurrentSoloIndex);
                text.AppendFormat("- Star index: {0}\n", state.CurrentStarIndex);
                text.AppendFormat("- Countdown index: {0}\n", state.CurrentWaitCountdownIndex);
                text.AppendLine();
                text.AppendFormat("- Solo active: {0}\n", state.IsSoloActive);
                text.AppendFormat("- Wait countdown active: {0}\n", state.IsWaitCountdownActive);
                text.AppendFormat("- Star Power input active: {0}\n", state.IsStarPowerInputActive);

                var stats = player.BaseEngine.BaseStats;
                text.AppendLine("Stats:");
                text.AppendFormat("- Committed score: {0}\n", stats.CommittedScore);
                text.AppendFormat("- Pending score: {0}\n", stats.PendingScore);
                text.AppendFormat("- Total score: {0}\n", stats.TotalScore);
                text.AppendFormat("- Star score: {0}\n", stats.StarScore);
                text.AppendFormat("- Stars: {0}\n", stats.Stars);
                text.AppendLine();
                text.AppendFormat("- Solo bonus score: {0}\n", stats.SoloBonuses);
                text.AppendFormat("- Star Power score: {0}\n", stats.StarPowerScore);
                text.AppendLine();
                text.AppendFormat("- Combo: {0}\n", stats.Combo);
                text.AppendFormat("- Max combo: {0}\n", stats.MaxCombo);
                text.AppendFormat("- Multiplier: {0}\n", stats.ScoreMultiplier);
                text.AppendLine();
                text.AppendFormat("- Notes hit: {0}/{1}\n", stats.NotesHit, stats.TotalNotes);
                text.AppendFormat("- Notes missed: {0}\n", stats.NotesMissed);
                text.AppendFormat("- Note hit percentage: {0}\n", stats.Percent);
                text.AppendLine();
                text.AppendFormat("- Star Power phrases hit: {0}/{1}\n",
                    stats.StarPowerPhrasesHit, stats.TotalStarPowerPhrases);
                text.AppendFormat("- Star Power phrases missed: {0}\n", stats.StarPowerPhrasesMissed);
                text.AppendLine();
                text.AppendFormat("- Star Power active: {0}\n", stats.IsStarPowerActive);
                text.AppendFormat("- Star Power bar amount: {0:0.000000}\n", stats.StarPowerBarAmount);
                text.AppendFormat("- Star Power tick amount: {0}\n", stats.StarPowerTickAmount);
                text.AppendFormat("- Total Star Power ticks: {0}\n", stats.TotalStarPowerTicks);
                text.AppendFormat("- Time in Star Power: {0:0.000000}\n", stats.TimeInStarPower);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();

            string playerType = player switch
            {
                FiveFretPlayer => "Five Fret Guitar",
                DrumsPlayer => "Drums",
                VocalsPlayer => "Vocals",
                ProKeysPlayer => "Pro Keys",

                _ => "Unhandled"
            };

            GUILayout.BeginVertical(playerType, VerticalGroupStyle);
            switch (player)
            {
                case FiveFretPlayer fiveFretPlayer:
                {
                    using var text = ZString.CreateStringBuilder(true);

                    var state = fiveFretPlayer.Engine.State;
                    text.AppendLine("State:");
                    text.AppendFormat("- Button mask: 0x{0:X2}\n", state.ButtonMask);
                    text.AppendFormat("- Last button mask: 0x{0:X2}\n", state.LastButtonMask);
                    text.AppendFormat("- Note was ghosted: {0}\n", state.WasNoteGhosted);
                    text.AppendLine();
                    text.AppendFormat("- Strum leniency timer: {0}\n", state.HopoLeniencyTimer);
                    text.AppendFormat("- HOPO leniency timer: {0}\n", state.StrumLeniencyTimer);
                    text.AppendFormat("- Star Power whammy timer: {0}\n", state.StarPowerWhammyTimer);
                    if (state.FrontEndExpireTime != double.MaxValue)
                        text.AppendFormat("- Front-end expire time: {0:0.000000}\n", state.FrontEndExpireTime);
                    else
                        text.Append("- Front-end expire time: Not set\n");

                    var stats = fiveFretPlayer.Engine.EngineStats;
                    text.AppendLine("Stats:");
                    text.AppendFormat("- Overstrums: {0}\n", stats.Overstrums);
                    text.AppendFormat("- Ghost inputs: {0}\n", stats.GhostInputs);
                    text.AppendFormat("- HOPOs strummed: {0}\n", stats.HoposStrummed);
                    text.AppendFormat("- Sustain score: {0}\n", stats.SustainScore);
                    text.AppendFormat("- Star Power whammy ticks: {0}\n", stats.WhammyTicks);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    break;
                }

                case DrumsPlayer drumsPlayer:
                {
                    using var text = ZString.CreateStringBuilder(true);

                    var state = drumsPlayer.Engine.State;
                    text.AppendLine("State:");
                    text.AppendLine("- No persistent state");

                    var stats = drumsPlayer.Engine.EngineStats;
                    text.AppendLine("Stats:");
                    text.AppendFormat("- Overhits: {0}\n", stats.Overhits);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    break;
                }

                case VocalsPlayer vocalsPlayer:
                {
                    using var text = ZString.CreateStringBuilder(true);

                    var state = vocalsPlayer.Engine.State;
                    text.AppendLine("State:");
                    text.AppendFormat("- Last pitch sang: {0:0.000}\n", state.PitchSang);
                    text.AppendFormat("- Current phrase ticks hit: {0:0.000}/{1}\n",
                        state.PhraseTicksHit, state.PhraseTicksTotal);
                    text.AppendFormat("- Last sing tick: {0}\n", state.LastSingTick);

                    var stats = vocalsPlayer.Engine.EngineStats;
                    text.AppendLine("Stats:");
                    text.AppendFormat("- Ticks hit: {0}\n", stats.TicksHit);
                    text.AppendFormat("- Ticks missed: {0}\n", stats.TicksMissed);
                    text.AppendFormat("- Total ticks so far: {0}\n", stats.TotalTicks);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    break;
                }

                case ProKeysPlayer proKeysPlayer:
                {
                    using var text = ZString.CreateStringBuilder(true);

                    var state = proKeysPlayer.Engine.State;
                    text.AppendLine("State:");
                    text.AppendFormat("- Key mask: 0x{0:X8}\n", state.KeyMask);
                    text.AppendFormat("- Visual key mask: 0x{0:X8}\n", state.KeyHeldMaskVisual);
                    text.AppendLine();
                    text.AppendFormat("- Chord stagger timer: {0}\n", state.ChordStaggerTimer);
                    text.AppendFormat("- Fat finger timer: {0}\n", state.FatFingerTimer);

                    var stats = proKeysPlayer.Engine.EngineStats;
                    text.AppendLine("Stats:");
                    text.AppendFormat("- Overhits: {0}\n", stats.Overhits);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                    break;
                }

                default:
                    GUILayout.Label($"Player type {player.GetType()} not handled yet");
                    break;
            }
            GUILayout.EndVertical();
        }

        private void TimingDebug()
        {
            GUILayout.BeginVertical("Calibration", VerticalGroupStyle);
            {
                using var text = ZString.CreateStringBuilder(true);

                text.AppendFormat("Audio calibration: {0}ms\n", _songRunner.AudioCalibration);
                text.AppendFormat("Video calibration: {0}ms\n", _songRunner.VideoCalibration);
                text.AppendFormat("Song offset: {0}ms\n", _songRunner.SongOffset);
                text.AppendFormat("Device audio latency: {0}ms\n", GlobalAudioHandler.PlaybackLatency);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Time", VerticalGroupStyle);
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

            GUILayout.BeginVertical("Sync", VerticalGroupStyle);
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
            GUILayout.BeginVertical("Lighting", VerticalGroupStyle);
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