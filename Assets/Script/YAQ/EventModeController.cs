using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Main;
using YARG.Menu.MusicLibrary;
using YARG.Menu.ScoreScreen;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

namespace YARG.YAQ
{
    /// <summary>
    /// Event-mode runtime: idle HUD with up-next, YAQ bridge, hot mics, and song launch into Difficulty Select.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EventModeController : MonoBehaviour
    {
        public static EventModeController Instance { get; private set; }

        private YaqBridgeClient _bridge;
        private YaqQueuePreview _preview = new();
        private YaqPlaySet _currentSet;
        private List<YaqSetPlayer> _currentPlayers = new();
        private string _phase = "idle";
        private string _status = "Connecting to YAQ…";
        private string _pendingSetId;
        private bool _librarySynced;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private readonly ConcurrentQueue<Action> _mainThread = new();

        private Texture2D _currentCover;
        private Texture2D _previewCover;
        private string _currentCoverHash;
        private string _previewCoverHash;
        private int _coverLoadGeneration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // CLI only here — settings load later and invoke YaqStreamEnabledCallback.
            if (!CommandLineArgs.YaqEvent) return;
            SetStreamEnabled(true);
        }

        /// <summary>
        /// Connect or disconnect the YAQ websocket stream (and event-mode HUD/menu gating).
        /// </summary>
        public static void SetStreamEnabled(bool enabled)
        {
            if (enabled)
            {
                EnsureExists();
                Instance?.StartBridge();
            }
            else
            {
                Instance?.StopBridge();
            }
        }

        private static void EnsureExists()
        {
            if (Instance != null) return;

            var go = new GameObject("YAQ Event Mode");
            DontDestroyOnLoad(go);
            go.AddComponent<EventModeController>();
        }

        private void Awake()
        {
            Instance = this;

            if (!string.IsNullOrEmpty(CommandLineArgs.YaqUrl))
            {
                EventMode.YaqWebSocketUrl = CommandLineArgs.YaqUrl;
            }

            _bridge = new YaqBridgeClient();
            _bridge.Connected += () => Enqueue(() =>
            {
                _status = EventMode.Suspended
                    ? "Connected to YAQ — Event Mode off"
                    : "Connected to YAQ — waiting for next set";
                _phase = "idle";
                ReportFlags();
                ReportEventModeState();
                if (!EventMode.Suspended)
                {
                    SendState("idle");
                    TrySyncLibrary();
                }
            });
            _bridge.Disconnected += () => Enqueue(() =>
            {
                _status = "YAQ disconnected — retrying…";
                _librarySynced = false;
            });
            _bridge.MessageReceived += msg => Enqueue(() => OnBridgeMessage(msg));
        }

        private void StartBridge()
        {
            EventMode.Enabled = true;
            EventMode.Suspended = false;
            _status = "Connecting to YAQ…";
            _bridge?.Start(EventMode.YaqWebSocketUrl);
            ApplyMainMenuVisibility();
            EnsureHotMics();
            ReportEventModeState();
        }

        private void StopBridge()
        {
            EventMode.Enabled = false;
            EventMode.Suspended = false;
            _bridge?.Stop();
            _librarySynced = false;
            _pendingSetId = null;
            _currentSet = null;
            _currentPlayers = new List<YaqSetPlayer>();
            _preview = new YaqQueuePreview();
            _phase = "idle";
            _status = "YAQ stream off";
            ClearCovers();
            ApplyMainMenuVisibility();
        }

        /// <summary>
        /// Enter Event Mode behaviors while keeping the WebSocket connected.
        /// </summary>
        public void EnterEventMode()
        {
            EventMode.Suspended = false;
            EventMode.Enabled = true;
            _status = "Event Mode on — waiting for next set";
            _phase = "idle";
            ApplyMainMenuVisibility();
            EnsureHotMics();
            TrySyncLibrary();
            SendState("idle");
            ReportEventModeState();
            YargLogger.LogInfo("YAQ entered Event Mode");
        }

        /// <summary>
        /// Exit Event Mode behaviors but keep the WebSocket connected for re-entry.
        /// </summary>
        public void ExitEventMode()
        {
            EventMode.Suspended = true;
            _pendingSetId = null;
            _currentSet = null;
            _currentPlayers = new List<YaqSetPlayer>();
            _phase = "idle";
            _status = "Event Mode off (bridge still connected)";
            ClearCovers();
            ApplyMainMenuVisibility();
            ReportEventModeState();
            YargLogger.LogInfo("YAQ exited Event Mode (bridge remains connected)");
        }

        private void ReportEventModeState()
        {
            _bridge?.Send(new
            {
                type = "eventmode.state",
                enabled = EventMode.IsActive,
                suspended = EventMode.Suspended
            });
        }

        private void OnDestroy()
        {
            ClearCovers();
            _bridge?.Dispose();
            if (Instance == this) Instance = null;
        }

        private void Enqueue(Action action) => _mainThread.Enqueue(action);

        private void Update()
        {
            while (_mainThread.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "YAQ event mode main-thread action failed");
                }
            }

            if (!EventMode.Enabled) return;

            if (EventMode.Suspended) return;

            if (!_librarySynced && SongContainer.Count > 0)
            {
                TrySyncLibrary();
            }

            EnsureHotMics();
        }

        private void OnGUI()
        {
            if (!EventMode.IsActive || !EventMode.Flags.showUpNextHud) return;
            if (GlobalVariables.Instance != null &&
                GlobalVariables.Instance.CurrentScene == SceneIndex.Gameplay)
            {
                return;
            }

            EnsureStyles();

            const float pad = 48f;
            const float artSize = 280f;
            GUILayout.BeginArea(new Rect(pad, pad, Screen.width - pad * 2f, Screen.height - pad * 2f));
            GUILayout.Label("YAQ EVENT", _titleStyle);
            GUILayout.Label(_status, _mutedStyle);
            GUILayout.Space(24f);

            if (_currentSet != null && (_phase == "ready" || _phase == "score"))
            {
                GUILayout.Label(_phase == "score" ? "SCORE" : "READY", _mutedStyle);
                GUILayout.BeginHorizontal();
                DrawAlbumArt(_currentCover, artSize);
                GUILayout.BeginVertical();
                GUILayout.Label($"{_currentSet.songArtist} — {_currentSet.songName}", _titleStyle);
                GUILayout.Space(12f);
                foreach (var player in _currentPlayers)
                {
                    GUILayout.Label(
                        $"{player.name}  ·  {player.instrument}  ·  {player.difficulty}",
                        _bodyStyle);
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(32f);
            }

            GUILayout.Label("UP NEXT", _mutedStyle);
            if (!string.IsNullOrEmpty(_preview?.songName))
            {
                GUILayout.BeginHorizontal();
                DrawAlbumArt(_previewCover, artSize);
                GUILayout.BeginVertical();
                GUILayout.Label($"{_preview.songArtist} — {_preview.songName}", _titleStyle);
                GUILayout.Space(12f);
                if (_preview.players != null)
                {
                    foreach (var player in _preview.players)
                    {
                        GUILayout.Label(
                            $"{player.name}  ·  {player.instrument}  ·  {player.difficulty}",
                            _bodyStyle);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Waiting for the next group…", _bodyStyle);
            }

            GUILayout.FlexibleSpace();
            if (EventMode.Flags.hotMic)
            {
                GUILayout.Label("Mics stay hot for host announcements.", _mutedStyle);
            }

            GUILayout.Label($"Bridge: {EventMode.YaqWebSocketUrl}", _mutedStyle);
            GUILayout.EndArea();
        }

        private static void DrawAlbumArt(Texture2D texture, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (texture != null)
            {
                // Album textures are loaded flipped for RawImage; flip for OnGUI too.
                GUI.DrawTextureWithTexCoords(rect, texture, new Rect(0f, 1f, 1f, -1f));
            }
            else
            {
                GUI.Box(rect, GUIContent.none);
            }

            GUILayout.Space(24f);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) },
                wordWrap = true
            };
            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.65f, 0.75f, 0.8f) },
                wordWrap = true
            };
        }

        private void OnBridgeMessage(JObject msg)
        {
            var type = msg.Value<string>("type");
            switch (type)
            {
                case "eventmode.enter":
                    EnterEventMode();
                    break;
                case "eventmode.exit":
                    ExitEventMode();
                    break;
                case "queue.preview":
                    if (EventMode.Suspended) break;
                    _preview = msg["preview"]?.ToObject<YaqQueuePreview>() ?? new YaqQueuePreview();
                    RequestCover(true, _preview?.songHash);
                    break;
                case "set.prepare":
                    if (EventMode.Suspended)
                    {
                        SendError("eventmode_suspended", null, null, "Event Mode is off; send eventmode.enter first");
                        break;
                    }

                    PrepareSet(msg);
                    break;
                case "set.launch":
                    if (EventMode.Suspended) break;
                    EnsureDifficultySelectOpen();
                    break;
                case "settings.update":
                    ApplyEventFlags(msg["flags"]?.ToObject<EventFlags>());
                    break;
                case "library.request":
                    _librarySynced = false;
                    TrySyncLibrary();
                    break;
                case "hello":
                    ReportFlags();
                    ReportEventModeState();
                    TrySyncLibrary();
                    if (!EventMode.Suspended)
                    {
                        SendState("idle");
                    }

                    break;
            }
        }

        private void ApplyEventFlags(EventFlags flags)
        {
            EventMode.Flags.CopyFrom(flags ?? EventFlags.Defaults);
            ApplyMainMenuVisibility();
            EnsureHotMics();
            _bridge?.Send(new
            {
                type = "settings.ack",
                flags = new
                {
                    EventMode.Flags.hotMic,
                    EventMode.Flags.showUpNextHud,
                    EventMode.Flags.skipMainMenu,
                    EventMode.Flags.openDifficultySelect
                }
            });
            YargLogger.LogFormatInfo(
                "YAQ event flags applied (hotMic={0}, hud={1}, skipMenu={2}, difficulty={3})",
                EventMode.Flags.hotMic,
                EventMode.Flags.showUpNextHud,
                EventMode.Flags.skipMainMenu,
                EventMode.Flags.openDifficultySelect);
        }

        private void ReportFlags()
        {
            _bridge?.Send(new
            {
                type = "settings.report",
                flags = new
                {
                    EventMode.Flags.hotMic,
                    EventMode.Flags.showUpNextHud,
                    EventMode.Flags.skipMainMenu,
                    EventMode.Flags.openDifficultySelect
                }
            });
        }

        private void PrepareSet(JObject msg)
        {
            var set = msg["set"]?.ToObject<YaqPlaySet>();
            var players = msg["players"]?.ToObject<List<YaqSetPlayer>>() ?? new List<YaqSetPlayer>();
            if (set == null || string.IsNullOrEmpty(set.songHash))
            {
                YargLogger.LogWarning("YAQ set.prepare missing song hash");
                SendError("invalid_set", set?.id, set?.songHash, "set.prepare missing song hash");
                return;
            }

            if (!SongContainer.SongsByHash.TryGetValue(HashWrapper.FromString(set.songHash), out var songs) ||
                songs == null || songs.Count == 0)
            {
                _status = $"Song hash not in library: {set.songHash}";
                YargLogger.LogFormatError("YAQ could not find song hash {0}", set.songHash);
                SendError("song_not_found", set.id, set.songHash, "Song hash not in YARG library");
                return;
            }

            var song = songs[0];
            ApplyPlayers(players);

            GlobalVariables.State.CurrentSong = song;
            GlobalVariables.State.ShowSongs.Clear();
            GlobalVariables.State.ShowSongs.Add(song);
            GlobalVariables.State.PlayingAShow = false;
            MusicLibraryMenu.CurrentlyPlaying = song;

            _pendingSetId = set.id;
            _currentSet = set;
            _currentPlayers = players;
            _phase = "ready";
            _status = $"Ready: {set.songArtist} — {set.songName}";
            RequestCover(false, set.songHash);

            if (GlobalVariables.Instance.CurrentScene != SceneIndex.Menu)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }

            if (EventMode.Flags.openDifficultySelect)
            {
                StartCoroutine(OpenReadyWhenPossible());
            }

            SendState("ready");
            _bridge.Send(new { type = "ready", setId = set.id });
        }

        private void EnsureDifficultySelectOpen()
        {
            if (!EventMode.Flags.openDifficultySelect) return;
            if (_currentSet == null) return;
            StartCoroutine(OpenReadyWhenPossible());
        }

        private System.Collections.IEnumerator OpenReadyWhenPossible()
        {
            for (var i = 0; i < 180; i++)
            {
                if (MenuManager.Instance != null)
                {
                    MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
                    yield break;
                }

                yield return null;
            }
        }

        private void ApplyPlayers(List<YaqSetPlayer> players)
        {
            var profiles = PlayerContainer.Players.ToList();

            // Trim extras when YAQ sends fewer players than currently active
            for (var i = profiles.Count - 1; i >= players.Count; i--)
            {
                PlayerContainer.DisposePlayer(profiles[i]);
            }

            profiles = PlayerContainer.Players.ToList();
            for (var i = 0; i < players.Count; i++)
            {
                var request = players[i];
                if (i >= profiles.Count)
                {
                    var profile = new YargProfile
                    {
                        Name = request.name,
                        CurrentInstrument = ParseInstrument(request.instrument),
                        PreferredInstrument = ParseInstrument(request.instrument),
                        CurrentDifficulty = ParseDifficulty(request.difficulty),
                        DifficultyFallback = ParseDifficulty(request.difficulty),
                    };
                    if (PlayerContainer.AddProfile(profile))
                    {
                        PlayerContainer.CreatePlayerFromProfile(profile, true);
                    }

                    continue;
                }

                var existing = profiles[i];
                existing.Profile.Name = request.name;
                existing.Profile.CurrentInstrument = ParseInstrument(request.instrument);
                existing.Profile.PreferredInstrument = existing.Profile.CurrentInstrument;
                existing.Profile.CurrentDifficulty = ParseDifficulty(request.difficulty);
                existing.Profile.DifficultyFallback = existing.Profile.CurrentDifficulty;
            }
        }

        private static Instrument ParseInstrument(string value)
        {
            return Enum.TryParse(value, true, out Instrument instrument)
                ? instrument
                : Instrument.FiveFretGuitar;
        }

        private static Difficulty ParseDifficulty(string value)
        {
            return Enum.TryParse(value, true, out Difficulty difficulty)
                ? difficulty
                : Difficulty.Expert;
        }

        private void TrySyncLibrary()
        {
            if (_bridge == null || !_bridge.IsConnected) return;
            if (SongContainer.Count <= 0) return;

            var songs = new List<object>();
            foreach (var song in SongContainer.Songs)
            {
                var instruments = new List<string>();
                foreach (Instrument instrument in Enum.GetValues(typeof(Instrument)))
                {
                    try
                    {
                        if (song.HasInstrument(instrument))
                        {
                            instruments.Add(instrument.ToString());
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                songs.Add(new
                {
                    hash = song.Hash.ToString(),
                    name = song.Name.ToString(),
                    artist = song.Artist.ToString(),
                    album = song.Album.ToString(),
                    year = song.UnmodifiedYear ?? song.ParsedYear ?? "",
                    genre = song.Genre.ToString(),
                    charter = song.Charter.ToString(),
                    folderPath = song.Location ?? song.ActualLocation ?? "",
                    instruments,
                    source = "yarg",
                    verified = true
                });
            }

            _bridge.Send(new { type = "library.sync", songs });
            _librarySynced = true;
            YargLogger.LogFormatInfo("YAQ library sync sent ({0} songs)", songs.Count);
        }

        public void NotifySongEnded(object scores = null)
        {
            scores ??= BuildScorePayload();
            _bridge?.Send(new { type = "song.ended", setId = _pendingSetId, scores });
            SendState("score");
            _phase = "score";
            _status = "Score — continue when ready for the next group";
        }

        public void NotifyIdle()
        {
            SendState("idle");
            _phase = "idle";
            _status = "Connected to YAQ — waiting for next set";
            _pendingSetId = null;
            _currentSet = null;
            _currentPlayers = new List<YaqSetPlayer>();
            ClearCover(false);
        }

        public void NotifyPlaying()
        {
            _phase = "playing";
            SendState("playing");
        }

        private static object BuildScorePayload()
        {
            if (GlobalVariables.State.ScoreScreenStats is not { } stats)
            {
                return null;
            }

            return new
            {
                bandScore = stats.BandScore,
                bandStars = stats.BandStars,
                players = stats.PlayerScores?.Select(card => new
                {
                    name = card.Player?.Profile?.Name,
                    instrument = card.Player?.Profile?.CurrentInstrument.ToString(),
                    difficulty = card.Player?.Profile?.CurrentDifficulty.ToString(),
                    score = card.Stats?.TotalScore ?? 0,
                    stars = card.Stats?.Stars ?? 0f,
                    isBot = card.Player?.Profile?.IsBot ?? false
                }).ToArray()
            };
        }

        private void SendState(string state)
        {
            _bridge?.Send(new { type = "state", state });
        }

        private void SendError(string code, string setId, string songHash, string message)
        {
            _bridge?.Send(new
            {
                type = "error",
                code,
                setId,
                songHash,
                message
            });
        }

        private void RequestCover(bool preview, string songHash)
        {
            if (string.IsNullOrEmpty(songHash))
            {
                ClearCover(preview);
                return;
            }

            if (preview)
            {
                if (_previewCoverHash == songHash && _previewCover != null) return;
                _previewCoverHash = songHash;
            }
            else
            {
                if (_currentCoverHash == songHash && _currentCover != null) return;
                _currentCoverHash = songHash;
            }

            var generation = ++_coverLoadGeneration;
            LoadCoverAsync(preview, songHash, generation).Forget();
        }

        private async UniTaskVoid LoadCoverAsync(bool preview, string songHash, int generation)
        {
            if (!SongContainer.SongsByHash.TryGetValue(HashWrapper.FromString(songHash), out var songs) ||
                songs == null || songs.Count == 0)
            {
                Enqueue(() =>
                {
                    if (generation != _coverLoadGeneration) return;
                    ClearCover(preview);
                });
                return;
            }

            var song = songs[0];
            YARG.Core.IO.YARGImage image = null;
            try
            {
                image = await UniTask.RunOnThreadPool(() => song.LoadAlbumData());
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "YAQ album art load failed");
            }

            Enqueue(() =>
            {
                if (generation != _coverLoadGeneration)
                {
                    image?.Dispose();
                    return;
                }

                ClearCover(preview);
                if (image == null) return;

                try
                {
                    var texture = image.LoadTexture(false);
                    if (preview)
                    {
                        _previewCover = texture;
                        _previewCoverHash = songHash;
                    }
                    else
                    {
                        _currentCover = texture;
                        _currentCoverHash = songHash;
                    }
                }
                finally
                {
                    image.Dispose();
                }
            });
        }

        private void ClearCover(bool preview)
        {
            if (preview)
            {
                if (_previewCover != null)
                {
                    Destroy(_previewCover);
                    _previewCover = null;
                }

                _previewCoverHash = null;
            }
            else
            {
                if (_currentCover != null)
                {
                    Destroy(_currentCover);
                    _currentCover = null;
                }

                _currentCoverHash = null;
            }
        }

        private void ClearCovers()
        {
            _coverLoadGeneration++;
            ClearCover(false);
            ClearCover(true);
        }

        private void ApplyMainMenuVisibility()
        {
            var mainMenu = FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
            if (mainMenu == null) return;

            var hide = EventMode.IsActive && EventMode.Flags.skipMainMenu;
            mainMenu.gameObject.SetActive(!hide);
        }

        private void EnsureHotMics()
        {
            if (!EventMode.Flags.hotMic) return;

            try
            {
                if (SettingsManager.Settings.VocalMonitoring.Value < 0.35f)
                {
                    SettingsManager.Settings.VocalMonitoring.Value = 0.7f;
                }

                foreach (var player in PlayerContainer.Players)
                {
                    foreach (var mic in player.Bindings.Microphones)
                    {
                        mic.SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Value);
                    }
                }
            }
            catch
            {
                // Bindings may not be ready during early boot
            }
        }
    }
}
