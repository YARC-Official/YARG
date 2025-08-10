using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Gameplay.Player;
using YARG.Integration;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private ref struct DebugScrollView
        {
            private bool _hasVertical;

            public static DebugScrollView Begin(string title, GUIStyle verticalStyle,
                ref Vector2 scrollPosition, params GUILayoutOption[] scrollOptions)
            {
                GUILayout.BeginVertical(title, verticalStyle);
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, scrollOptions);
                return new DebugScrollView()
                {
                    _hasVertical = true,
                };
            }

            public static DebugScrollView Begin(ref Vector2 scrollPosition, params GUILayoutOption[] scrollOptions)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, scrollOptions);
                return new DebugScrollView()
                {
                    _hasVertical = false,
                };
            }

            public void Dispose()
            {
                GUILayout.EndScrollView();
                if (_hasVertical)
                {
                    GUILayout.EndVertical();
                }
            }
        }

        private ref struct DebugVerticalArea
        {
            public static DebugVerticalArea Begin(string title, GUIStyle verticalStyle)
            {
                GUILayout.BeginVertical(title, verticalStyle);
                return new DebugVerticalArea();
            }

            public void Dispose()
            {
                GUILayout.EndVertical();
            }
        }

        private const int DEBUG_WINDOW_ID = 0;
        private const int DEBUG_WINDOW_MARGIN = 25;

        // The values used for everything were designed under a height of
        // 550 pixels (using the Unity editor viewport).
        // Decided to round it down to 500 since it gives a little more room
        // after scaling calculation is applied
        private const int DEBUG_WINDOW_DESIGN_HEIGHT = 500;

        private const int DEBUG_WINDOW_MIN_WIDTH = 300;
        private const int DEBUG_WINDOW_MAX_WIDTH = 600;

        private const int DEBUG_WINDOW_MAX_HEIGHT = DEBUG_WINDOW_DESIGN_HEIGHT - (DEBUG_WINDOW_MARGIN * 2);

        private bool _enableDebug;

        // Box style doesn't account for the title text, so window style it is
        private GUIStyle VerticalGroupStyle => GUI.skin.window;

        private int _debugLastScreenHeight;
        private float _debugGuiScale;

        private GUI.WindowFunction _debugWindowCallback;
        private Rect _debugWindowRect = new(DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN, 0, 0);
        private Vector2 _debugWindowScroll;

        private List<(string title, Action callback)> _debugMenus;
        private string[] _debugMenuTitles;
        private int _debugMenuIndex = -1;

        // Needed because of non-static methods being used as delegates
        private void InitializeDebug()
        {
            _debugWindowCallback = WindowCallback;
            _debugMenus = new()
            {
                ("Player", PlayerDebug),
                ("Timing", TimingDebug),
                ("Input",  InputDebug),
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

            _debugInputEventTrace.onFilterEvent = (eventPtr, device) =>
            {
                if (_debugSelectedPlayer < 0 || _debugSelectedPlayer >= _players.Count)
                {
                    return false;
                }

                var player = _players[_debugSelectedPlayer];
                return player.Player.Bindings.ContainsDevice(device);
            };
        }

        private void DisposeDebug()
        {
            _debugInputEventTrace.Dispose();
        }

        private void SetDebugEnabled(bool enabled)
        {
            _enableDebug = enabled;
            // GUI layout is toggled since merely having it enabled causes needless memory allocations
            useGUILayout = enabled;

            if (enabled)
            {
                _debugInputEventTrace.Clear();
                _debugInputEventTrace.Enable();
            }
            else
            {
                _debugInputEventTrace.Disable();
                _debugInputEventTrace.Clear();
            }
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

            // Update GUI scale as needed
            if (Screen.height != _debugLastScreenHeight)
            {
                _debugLastScreenHeight = Screen.height;

                float oldScale = _debugGuiScale;
                _debugGuiScale = (float) Screen.height / DEBUG_WINDOW_DESIGN_HEIGHT;

                // Adjust window rect to prevent errors in the clamping code below
                _debugWindowRect.width = (_debugWindowRect.width / oldScale) * _debugGuiScale;
                _debugWindowRect.height = (_debugWindowRect.height / oldScale) * _debugGuiScale;
            }

            // Clamp position to screen bounds
            _debugWindowRect.x = Math.Clamp(
                _debugWindowRect.x,
                -_debugWindowRect.width + (DEBUG_WINDOW_MARGIN * 2),
                Math.Max(0, Screen.width - (DEBUG_WINDOW_MARGIN * 2))
            );
            _debugWindowRect.y = Math.Clamp(
                _debugWindowRect.y,
                0,
                Math.Max(0, Screen.height - (DEBUG_WINDOW_MARGIN * 2))
            );

            // Reset size so expansions don't persist
            _debugWindowRect.size = new Vector2();

            _debugWindowRect = GUILayout.Window(
                DEBUG_WINDOW_ID, _debugWindowRect, _debugWindowCallback, "Debug Menu",
                GUILayout.MinWidth(DEBUG_WINDOW_MIN_WIDTH),
                GUILayout.MaxWidth(DEBUG_WINDOW_MAX_WIDTH * _debugGuiScale),
                GUILayout.MaxHeight(DEBUG_WINDOW_MAX_HEIGHT * _debugGuiScale)
            );
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
            _debugWindowRect.position = new Vector2(DEBUG_WINDOW_MARGIN, DEBUG_WINDOW_MARGIN);
        }

        private string[] _debugPlayers;
        private int _debugSelectedPlayer = -1;
        private Vector2 _debugBaseEngineScroll;
        private Vector2 _debugDerivedEngineScroll;

        private bool _debugProKeysPressTimesToggle;

        private bool PlayerDebugSelection()
        {
            GUILayout.BeginVertical("Player Selection", VerticalGroupStyle);
            int buttonStride = 50 / _debugPlayers.Max((p) => p.Length);
            int lastSelected = _debugSelectedPlayer;
            _debugSelectedPlayer = GUILayout.SelectionGrid(_debugSelectedPlayer, _debugPlayers, buttonStride);
            GUILayout.EndVertical();

            return _debugSelectedPlayer != lastSelected;
        }

        private void PlayerDebug()
        {
            PlayerDebugSelection();

            if (_debugSelectedPlayer < 0 || _debugSelectedPlayer >= _players.Count)
                return;

            var player = _players[_debugSelectedPlayer];

            using (DebugScrollView.Begin("Base Engine", VerticalGroupStyle,
                ref _debugBaseEngineScroll, GUILayout.Height(125 * _debugGuiScale)))
            {
                using var text = ZString.CreateStringBuilder(true);

                var engine = player.BaseEngine;
                text.AppendLine("Time state:");
                text.AppendFormat("- Current tick: {0}\n", engine.CurrentTick);
                text.AppendFormat("- Current time: {0:0.000000}\n", engine.CurrentTime);
                text.AppendFormat("- Last tick: {0}\n", engine.LastTick);
                text.AppendFormat("- Last time: {0:0.000000}\n", engine.LastUpdateTime);
                if (engine.LastQueuedInputTime != double.MinValue)
                    text.AppendFormat("- Last queued input time: {0:0.000000}\n", engine.LastQueuedInputTime);
                else
                    text.Append("- Last queued input time: None\n");
                text.AppendLine();
                text.AppendLine("Indexes:");
                text.AppendFormat("- Note index: {0}\n", engine.NoteIndex);
                text.AppendFormat("- Solo index: {0}\n", engine.CurrentSoloIndex);
                text.AppendFormat("- Star index: {0}\n", engine.CurrentStarIndex);
                text.AppendFormat("- Countdown index: {0}\n", engine.CurrentWaitCountdownIndex);
                text.AppendLine();
                text.AppendLine("Star Power:");
                text.AppendFormat("- Ticks per quarter bar: {0}\n", engine.TicksPerQuarterSpBar);
                text.AppendFormat("- Ticks per half bar: {0}\n", engine.TicksPerHalfSpBar);
                text.AppendFormat("- Ticks per full bar: {0}\n", engine.TicksPerFullSpBar);
                text.AppendLine();
                text.AppendFormat("- Current Star Power tick: {0}\n", engine.StarPowerTickPosition);
                text.AppendFormat("- Last Star Power tick: {0}\n", engine.PreviousStarPowerTickPosition);
                text.AppendFormat("- Activation start tick: {0}\n", engine.StarPowerTickActivationPosition);
                text.AppendFormat("- Activation end tick: {0}\n", engine.StarPowerTickEndPosition);
                text.AppendFormat("- Activation start time: {0:0.000000}\n", engine.StarPowerActivationTime);
                text.AppendFormat("- Activation end time: {0:0.000000}\n", engine.StarPowerEndTime);
                text.AppendFormat("- Time In Star Power: {0:0.000000}\n", engine.BaseStats.TimeInStarPower);
                text.AppendFormat("- Base Time In Star Power: {0:0.000000}\n", engine.BaseTimeInStarPower);
                text.AppendLine();
                // text.AppendFormat("- Current drain rate: {0}\n", ); // TODO?
                //text.AppendFormat("- Last whammy tick: {0}\n", engine.LastStarPowerWhammyTick);
                text.AppendFormat("- Star Power whammy timer:\n     {0}\n", engine.GetStarPowerWhammyTimer());
                text.AppendLine();
                text.AppendLine("Miscellaneous:");
                text.AppendFormat("- Solo active: {0}\n", engine.IsSoloActive);
                text.AppendFormat("- Wait countdown active: {0}\n", engine.IsWaitCountdownActive);
                text.AppendFormat("- Star Power input active: {0}\n", engine.IsStarPowerInputActive);
                text.AppendFormat("- Can activate Star Power: {0}\n", engine.CanStarPowerActivate);

                var stats = player.BaseEngine.BaseStats;
                text.AppendLine("\nScore stats:");
                text.AppendFormat("- Base score: {0}\n", engine.BaseScore);
                text.AppendFormat("- Committed score: {0}\n", stats.CommittedScore);
                text.AppendFormat("- Pending score: {0}\n", stats.PendingScore);
                text.AppendFormat("- Total score: {0}\n", stats.TotalScore);
                text.AppendFormat("- Star score: {0}\n", stats.StarScore);
                text.AppendFormat("- Stars: {0}\n", stats.Stars);
                text.AppendLine();
                text.AppendFormat("- Sustain score: {0}\n", stats.SustainScore);
                text.AppendFormat("- Star Power score: {0}\n", stats.StarPowerScore);
                text.AppendFormat("- Solo bonus score: {0}\n", stats.SoloBonuses);
                text.AppendLine();
                text.AppendLine("Combo stats:");
                text.AppendFormat("- Combo: {0}\n", stats.Combo);
                text.AppendFormat("- Max combo: {0}\n", stats.MaxCombo);
                text.AppendFormat("- Multiplier: {0}\n", stats.ScoreMultiplier);
                text.AppendLine();
                text.AppendFormat("- Notes hit: {0}/{1}\n", stats.NotesHit, stats.TotalNotes);
                text.AppendFormat("- Notes missed: {0}\n", stats.NotesMissed);
                text.AppendFormat("- Note hit percentage: {0:0.000}\n", stats.Percent);
                text.AppendLine();
                text.AppendLine("Star Power stats:");
                text.AppendFormat("- Star Power phrases hit: {0}/{1}\n",
                    stats.StarPowerPhrasesHit, stats.TotalStarPowerPhrases);
                text.AppendFormat("- Star Power phrases missed: {0}\n", stats.StarPowerPhrasesMissed);
                text.AppendLine();
                text.AppendFormat("- Star Power active: {0}\n", stats.IsStarPowerActive);
                text.AppendFormat("- Star Power bar amount: {0:0.000000}\n", engine.GetStarPowerBarAmount());
                text.AppendFormat("- Star Power tick amount: {0}\n", stats.StarPowerTickAmount);
                text.AppendFormat("- Total Star Power ticks: {0}\n", stats.TotalStarPowerTicks);
                text.AppendFormat("- Star Power whammy ticks: {0}\n", stats.StarPowerWhammyTicks);
                text.AppendLine();
                text.AppendFormat("- Star Power activation count: {0}\n", stats.StarPowerActivationCount);
                text.AppendFormat("- Total Star Power bars filled: {0:0.000000}\n", stats.TotalStarPowerBarsFilled);
                text.AppendFormat("- Total time in Star Power: {0:0.000000}\n", stats.TimeInStarPower);

                GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
            }

            string playerType = player switch
            {
                FiveFretPlayer => "Five Fret Guitar",
                DrumsPlayer => "Drums",
                VocalsPlayer => "Vocals",
                ProKeysPlayer => "Pro Keys",

                _ => "Unhandled"
            };

            using (DebugScrollView.Begin(playerType, VerticalGroupStyle,
                ref _debugDerivedEngineScroll, GUILayout.Height(125 * _debugGuiScale)))
            {
                switch (player)
                {
                    case FiveFretPlayer fiveFretPlayer:
                    {
                        using var text = ZString.CreateStringBuilder(true);

                        var engine = fiveFretPlayer.Engine;
                        text.AppendLine("State:");
                        text.AppendFormat("- Button mask: 0x{0:X2}\n", engine.EffectiveButtonMask);
                        text.AppendFormat("- Last button mask: 0x{0:X2}\n", engine.LastButtonMask);
                        text.AppendFormat("- Note was ghosted: {0}\n", engine.WasNoteGhosted);
                        text.AppendLine();
                        text.AppendFormat("- Strum leniency timer: {0}\n", engine.GetHopoLeniencyTimer());
                        text.AppendFormat("- HOPO leniency timer: {0}\n", engine.GetStrumLeniencyTimer());
                        double frontEndExpire = engine.GetFrontEndExpireTime();
                        if (frontEndExpire != double.MaxValue)
                            text.AppendFormat("- Front-end expire time: {0:0.000000}\n", frontEndExpire);
                        else
                            text.Append("- Front-end expire time: Not set\n");

                        var stats = fiveFretPlayer.Engine.EngineStats;
                        text.AppendLine("\nStats:");
                        text.AppendFormat("- Overstrums: {0}\n", stats.Overstrums);
                        text.AppendFormat("- Ghost inputs: {0}\n", stats.GhostInputs);
                        text.AppendFormat("- HOPOs strummed: {0}\n", stats.HoposStrummed);
                        text.AppendFormat("- Sustain score: {0}\n", stats.SustainScore);

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                        break;
                    }

                    case DrumsPlayer drumsPlayer:
                    {
                        using var text = ZString.CreateStringBuilder(true);

                        var engine = drumsPlayer.Engine;
                        text.AppendLine("State:");
                        text.AppendLine("- No persistent state");

                        var stats = drumsPlayer.Engine.EngineStats;
                        text.AppendLine("\nStats:");
                        text.AppendFormat("- Overhits: {0}\n", stats.Overhits);

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                        break;
                    }

                    case VocalsPlayer vocalsPlayer:
                    {
                        using var text = ZString.CreateStringBuilder(true);

                        var engine = vocalsPlayer.Engine;
                        text.AppendLine("State:");
                        text.AppendFormat("- Last pitch sang: {0:0.000}\n", engine.PitchSang);
                        text.AppendFormat("- Current phrase ticks hit: {0:0.000}/{1}\n",
                            engine.PhraseTicksHit, engine.PhraseTicksTotal);
                        text.AppendFormat("- Last sing tick: {0}\n", engine.LastSingTick);

                        var stats = vocalsPlayer.Engine.EngineStats;
                        text.AppendLine("\nStats:");
                        text.AppendFormat("- Ticks hit: {0}\n", stats.TicksHit);
                        text.AppendFormat("- Ticks missed: {0}\n", stats.TicksMissed);
                        text.AppendFormat("- Total ticks so far: {0}\n", stats.TotalTicks);

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                        break;
                    }

                    case ProKeysPlayer proKeysPlayer:
                    {
                        using var text = ZString.CreateStringBuilder(true);

                        var engine = proKeysPlayer.Engine;
                        text.AppendLine("State:");
                        text.AppendFormat("- Key mask: 0x{0:X8}\n", engine.KeyMask);
                        text.AppendFormat("- Previous key mask: 0x{0:X8}\n", engine.PreviousKeyMask);
                        text.AppendLine();
                        text.AppendFormat("- Chord stagger timer: {0}\n", engine.GetChordStaggerTimer());
                        text.AppendFormat("- Fat finger timer: {0}\n", engine.GetFatFingerTimer());

                        // Don't strip final newline here, for spacing with the toggle below
                        GUILayout.Label(text.ToString());
                        text.Clear();

                        _debugProKeysPressTimesToggle = GUILayout.Toggle(_debugProKeysPressTimesToggle, "Key press times:");
                        if (_debugProKeysPressTimesToggle)
                        {
                            var pressTimes = engine.GetKeyPressTimes();
                            for (int i = 0; i < pressTimes.Length; i++)
                            {
                                text.AppendFormat("- {0}: {1:0.000000}\n", i + 1, pressTimes[i]);
                            }

                            GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                            text.Clear();
                        }

                        var stats = proKeysPlayer.Engine.EngineStats;
                        text.AppendLine("\nStats:");
                        text.AppendFormat("- Overhits: {0}\n", stats.Overhits);

                        GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                        break;
                    }

                    default:
                        GUILayout.Label($"Player type {player.GetType()} not handled yet");
                        break;
                }
            }
        }

        private Vector2 _debugCalibrationScroll;
        private Vector2 _debugTimeScroll;
        private Vector2 _debugSyncScroll;

        private void TimingDebug()
        {
            using (DebugScrollView.Begin(ref _debugCalibrationScroll, GUILayout.Height(300 * _debugGuiScale)))
            {
                using (DebugVerticalArea.Begin("Calibration", VerticalGroupStyle))
                {
                    using var text = ZString.CreateStringBuilder(true);

                    text.AppendFormat("Audio calibration: {0}ms\n", _songRunner.AudioCalibration);
                    text.AppendFormat("Video calibration: {0}ms\n", _songRunner.VideoCalibration);
                    text.AppendFormat("Song offset: {0}ms\n", _songRunner.SongOffset);
                    text.AppendFormat("Device audio latency: {0}ms\n", GlobalAudioHandler.PlaybackLatency);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                }

                using (DebugVerticalArea.Begin("Time", VerticalGroupStyle))
                {
                    using var text = ZString.CreateStringBuilder(true);

                    text.AppendFormat("Song time: {0:0.000000}\n", _songRunner.SongTime);
                    text.AppendFormat("Audio time: {0:0.000000}\n", _songRunner.AudioTime);
                    text.AppendFormat("Visual time: {0:0.000000}\n", _songRunner.VisualTime);
                    text.AppendFormat("Input time: {0:0.000000}\n", _songRunner.InputTime);
                    text.AppendFormat("Input offset: {0:0.000000}\n", _songRunner.InputTimeOffset);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                }

                using (DebugVerticalArea.Begin("Sync", VerticalGroupStyle))
                {
                    using var text = ZString.CreateStringBuilder(true);

                    text.AppendFormat("Audio/visual difference: {0:0.000000}\n", _songRunner.SyncDelta);
                    text.AppendFormat("Resync start delta: {0:0.000000}\n", _songRunner.SyncStartDelta);
                    text.AppendFormat("Resync worst delta: {0:0.000000}\n", _songRunner.SyncWorstDelta);
                    text.AppendFormat("Speed adjustment: {0:0.00}\n", _songRunner.SyncSpeedAdjustment);
                    text.AppendFormat("Speed multiplier: {0}\n", _songRunner.SyncSpeedMultiplier);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                }

                using (DebugVerticalArea.Begin("Beats", VerticalGroupStyle))
                {
                    using var text = ZString.CreateStringBuilder(true);

                    var sync = Chart.SyncTrack;

                    uint tick = sync.TimeToTick(_songRunner.SongTime);

                    double strongBeat = sync.GetStrongBeatPosition(tick);
                    double weakBeat = sync.GetWeakBeatPosition(tick);
                    double denomBeat = sync.GetDenominatorBeatPosition(tick);
                    double quarterNote = sync.GetQuarterNotePosition(tick);
                    double measure = sync.GetMeasurePosition(tick);

                    text.AppendFormat("Strong beat position: {0:0.000}\n", strongBeat);
                    text.AppendFormat("Weak beat position: {0:0.000}\n", weakBeat);
                    text.AppendFormat("Denominator beat position: {0:0.000}\n", denomBeat);
                    text.AppendFormat("Quarter note position: {0:0.000}\n", quarterNote);
                    text.AppendFormat("Measure position: {0:0.000}\n", measure);

                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                }
            }
        }

        private InputEventTrace _debugInputEventTrace = new();
        private long _debugLastInputCount;
        private Vector2 _debugInputLogScroll;
        private bool _debugInputLogAutoScroll = true;

        private void InputDebug()
        {
            if (PlayerDebugSelection())
            {
                _debugInputEventTrace.Clear();
            }

            GUILayout.BeginVertical("Input Event Log", VerticalGroupStyle);
            {
                _debugInputLogAutoScroll = GUILayout.Toggle(_debugInputLogAutoScroll, "Auto-scroll input event log");
                if (GUILayout.Button("Clear input event log"))
                {
                    _debugInputEventTrace.Clear();
                }

                if (_debugInputLogAutoScroll && _debugInputEventTrace.eventCount != _debugLastInputCount)
                {
                    _debugLastInputCount = _debugInputEventTrace.eventCount;
                    _debugInputLogScroll.y = 999999999;
                }

                _debugInputLogScroll = GUILayout.BeginScrollView(_debugInputLogScroll,
                    GUILayout.Width(300 * _debugGuiScale), GUILayout.Height(250 * _debugGuiScale));
                {
                    using var text = ZString.CreateStringBuilder(true);
                    foreach (var inputEvent in _debugInputEventTrace)
                    {
                        text.AppendFormat("{0:0.000000} {1}", inputEvent.time, inputEvent.type);

                        if (inputEvent.type == StateEvent.Type)
                        {
                            unsafe
                            {
                                var state = StateEvent.From(inputEvent);
                                text.AppendFormat(" {0} size={1}\n", state->stateFormat, state->stateSizeInBytes);
                                PrintState(new(state->state, (int)state->stateSizeInBytes));
                            }
                        }
                        else if (inputEvent.type == DeltaStateEvent.Type)
                        {
                            unsafe
                            {
                                var state = DeltaStateEvent.From(inputEvent);
                                text.AppendFormat(" {0} offset={1} size={2}\n",
                                    state->stateFormat, state->stateOffset, state->deltaStateSizeInBytes);
                                PrintState(new(state->deltaState, (int)state->deltaStateSizeInBytes));
                            }
                        }
                        else
                        {
                            text.AppendFormat(" {0}", inputEvent.sizeInBytes);
                        }

                        void PrintState(ReadOnlySpan<byte> bytes)
                        {
                            const int bytesPerLine = 24;

                            Span<char> formatBuffer = stackalloc char[bytes.Length * 3];
                            for (int i = 0; i < bytes.Length; i += bytesPerLine)
                            {
                                text.Append("    ");

                                var lineBytes = bytes[i..Math.Min(bytesPerLine, bytes.Length)];
                                if (!lineBytes.TryFormatHex(formatBuffer, out int written, dashes: true))
                                {
                                    text.AppendLine("Failed to format state bytes");
                                    return;
                                }
                                text.Append(formatBuffer[..written]);

                                text.AppendLine();
                            }
                        }

                        text.AppendLine();
                    }
                    GUILayout.Label(text.AsSpan().TrimEnd('\n').ToString());
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private Vector2 _debugLightingScroll;

        private void VenueDebug()
        {
            using (DebugScrollView.Begin("Lighting", VerticalGroupStyle,
                ref _debugLightingScroll, GUILayout.Height(50 * _debugGuiScale)))
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
        }
    }
}