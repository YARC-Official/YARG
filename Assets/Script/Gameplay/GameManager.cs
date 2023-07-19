using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Replays.IO;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Input;
using YARG.Player;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;

namespace YARG.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TrackViewManager _trackViewManager;

        [Header("Instrument Prefabs")]
        [SerializeField]
        private GameObject fiveFretGuitarPrefab;

        [SerializeField]
        private GameObject sixFretGuitarPrefab;

        [SerializeField]
        private GameObject fourLaneDrumsPrefab;

        [SerializeField]
        private GameObject fiveLaneDrumsPrefab;

        [SerializeField]
        private GameObject proGuitarPrefab;

        public SongEntry Song  { get; private set; }
        public SongChart Chart { get; private set; }

        public double SongStartTime { get; private set; }
        public double SongLength    { get; private set; }

        public double AudioCalibration => -SettingsManager.Settings.AudioCalibration.Data / 1000.0;

        /// <summary>
        /// The time into the song <b>without</b> accounting for calibration.<br/>
        /// This is updated every frame.
        /// </summary>
        public double RealSongTime { get; private set; }
        /// <summary>
        /// The time into the song <b>accounting</b> for calibration.<br/>
        /// This is updated every frame.
        /// </summary>
        public double SongTime => RealSongTime + AudioCalibration;

        public bool IsReplay { get; private set; }

        public bool Paused { get; private set; }

        private List<BasePlayer> _players;
        private List<Beatline>   _beats;

        private void Awake()
        {
            Song = GlobalVariables.Instance.CurrentSong;

            string notesFile = Path.Combine(Song.Location, Song.NotesFile);
            Debug.Log(notesFile);
            Chart = SongChart.FromFile(new SongMetadata(), notesFile);

            IsReplay = GlobalVariables.Instance.isReplay;

            _beats = Chart.SyncTrack.Beatlines;
            if (_beats is null || _beats.Count < 1)
                _beats = Chart.SyncTrack.GenerateBeatlines();

            LoadSong();
            CreatePlayers();
        }

        private void Update()
        {
            // It is more performant to calculate this per frame instead of per call
            RealSongTime = GlobalVariables.AudioManager.CurrentPositionD;
        }

        private void LoadSong()
        {
            var song = GlobalVariables.Instance.CurrentSong;

            song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.songSpeed);

            SongLength = GlobalVariables.AudioManager.AudioLengthD;

            GlobalVariables.AudioManager.Play();
            InputManager.InputTimeOffset = InputManager.CurrentInputTime - AudioCalibration;
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            var profile = new YargProfile
            {
                Name = "RileyTheFox",
                IsBot = true,
            };

            PlayerContainer.AddProfile(profile);
            PlayerContainer.CreatePlayerFromProfile(profile);

            int count = -1;
            foreach (var player in PlayerContainer.Players)
            {
                count++;
                var prefab = player.Profile.InstrumentType switch
                {
                    GameMode.FiveFretGuitar => fiveFretGuitarPrefab,
                    GameMode.SixFretGuitar  => sixFretGuitarPrefab,
                    GameMode.FourLaneDrums  => fourLaneDrumsPrefab,
                    GameMode.FiveLaneDrums  => fiveLaneDrumsPrefab,
                    GameMode.ProGuitar      => proGuitarPrefab,

                    _ => null
                };
                if (prefab == null)
                    continue;

                var playerObject = Instantiate(prefab, new Vector3(count * 25f, 100f, 0f), prefab.transform.rotation);
                Debug.Log("Instantiated");

                // Setup player
                var basePlayer = playerObject.GetComponent<BasePlayer>();
                basePlayer.Player = player;
                _players.Add(basePlayer);

                _trackViewManager.CreateTrackView(basePlayer);

                // Load it up
                LoadChart(player, basePlayer);
            }
        }

        private void LoadChart(YargPlayer yargPlayer, BasePlayer basePlayer)
        {
            var profile = yargPlayer.Profile;
            var instrument = profile.Instrument;
            var difficulty = profile.Difficulty;
            // int vocalsPart = profile.VocalsPart;

            switch (profile.InstrumentType)
            {
                case GameMode.FiveFretGuitar:
                {
                    var chart = Chart.GetFiveFretTrack(instrument).Difficulties[difficulty];
                    (basePlayer as FiveFretPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                case GameMode.SixFretGuitar:
                {
                    var chart = Chart.GetSixFretTrack(instrument).Difficulties[difficulty];
                    // (basePlayer as SixFretPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                case GameMode.FourLaneDrums:
                {
                    var chart = Chart.GetDrumsTrack(instrument).Difficulties[difficulty];
                    // (basePlayer as FourLaneDrumsPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                case GameMode.FiveLaneDrums:
                {
                    var chart = Chart.GetDrumsTrack(instrument).Difficulties[difficulty];
                    // (basePlayer as FiveLaneDrumsPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                case GameMode.ProGuitar:
                {
                    var chart = Chart.GetProGuitarTrack(instrument).Difficulties[difficulty];
                    // (basePlayer as ProGuitarPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                case GameMode.Vocals:
                {
                    // var chart = Chart.GetVocalsTrack(instrument).Parts[vocalsPart];
                    // (basePlayer as VocalsPlayer)?.Initialize(yargPlayer, chart);
                    break;
                }

                default:
                    break;
            }
        }

        public void SetPaused(bool paused)
        {
            // Set pause menu gameobject active status

            if (paused)
            {
                GlobalVariables.AudioManager.Pause();
            }
            else
            {
                GlobalVariables.AudioManager.Play();
            }
        }

        private void EndSong()
        {
            if (!IsReplay)
            {
                var replay = ReplayContainer.CreateNewReplay(Song, _players);
                var entry = new ReplayEntry
                {
                    SongName = replay.SongName,
                    ArtistName = replay.ArtistName,
                    CharterName = replay.CharterName,
                    BandScore = replay.BandScore,
                    Date = replay.Date,
                    SongChecksum = replay.SongChecksum,
                    PlayerCount = replay.PlayerCount,
                    PlayerNames = replay.PlayerNames,
                    GameVersion = replay.Header.GameVersion,
                };

                entry.ReplayFile = entry.GetReplayName();

                ReplayIO.WriteReplay(Path.Combine(ReplayContainer.ReplayDirectory, entry.ReplayFile), replay);
            }
        }
    }
}