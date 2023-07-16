using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Replays.IO;
using YARG.Gameplay.HUD;
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
        public double SongTime      => GlobalVariables.AudioManager.CurrentPositionD +
            SettingsManager.Settings.AudioCalibration.Data / 1000f;

        public bool IsReplay { get; private set; }

        public bool Paused { get; private set; }

        private List<BasePlayer> _players;
        private List<Beat>       _beats;

        private void Awake()
        {
            _beats = new List<Beat>();
            Song = GlobalVariables.Instance.CurrentSong;

            string notesFile = Path.Combine(Song.Location, Song.NotesFile);
            Debug.Log(notesFile);
            Chart = SongChart.FromFile(new SongMetadata(), notesFile);

            IsReplay = GlobalVariables.Instance.isReplay;

            var beatHandler = new BeatHandler(Chart);
            beatHandler.GenerateBeats();
            _beats = beatHandler.Beats;

            LoadSong();
            CreatePlayers();
        }

        private void LoadSong()
        {
            var song = GlobalVariables.Instance.CurrentSong;

            song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.songSpeed);

            SongLength = GlobalVariables.AudioManager.AudioLengthD;

            GlobalVariables.AudioManager.Play();
            InputManager.InputTimeOffset = InputManager.CurrentInputTime;
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            var profile = new YargProfile
            {
                Name = "RileyTheFox"
            };

            PlayerContainer.AddProfile(profile);
            PlayerContainer.CreatePlayerFromProfile(profile);

            int count = -1;
            foreach (var player in PlayerContainer.Players)
            {
                count++;
                GameObject prefab;

                switch (player.Profile.InstrumentType)
                {
                    case GameMode.FiveFretGuitar:
                        prefab = fiveFretGuitarPrefab;
                        break;
                    case GameMode.SixFretGuitar:
                        prefab = sixFretGuitarPrefab;
                        break;
                    case GameMode.FourLaneDrums:
                        prefab = fourLaneDrumsPrefab;
                        break;
                    case GameMode.FiveLaneDrums:
                        prefab = fiveLaneDrumsPrefab;
                        break;
                    case GameMode.ProGuitar:
                        prefab = proGuitarPrefab;
                        break;
                    default:
                        continue;
                }

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
            switch (yargPlayer.Profile.Instrument)
            {
                case Instrument.FiveFretGuitar:
                    var notes = Chart.FiveFretGuitar.Difficulties[yargPlayer.Profile.Difficulty].Notes;
                    goto initFiveFret;
                case Instrument.FiveFretBass:
                    notes = Chart.FiveFretBass.Difficulties[yargPlayer.Profile.Difficulty].Notes;
                    goto initFiveFret;
                case Instrument.FiveFretRhythm:
                    notes = Chart.FiveFretRhythm.Difficulties[yargPlayer.Profile.Difficulty].Notes;
                    goto initFiveFret;
                case Instrument.FiveFretCoopGuitar:
                    notes = Chart.FiveFretCoop.Difficulties[yargPlayer.Profile.Difficulty].Notes;
                    goto initFiveFret;
                case Instrument.Keys:
                    notes = Chart.Keys.Difficulties[yargPlayer.Profile.Difficulty].Notes;

                initFiveFret:
                    (basePlayer as FiveFretPlayer)?.Initialize(yargPlayer, notes);
                    break;
                case Instrument.SixFretGuitar:
                case Instrument.SixFretBass:
                case Instrument.SixFretRhythm:
                case Instrument.SixFretCoopGuitar:

                case Instrument.FourLaneDrums:
                case Instrument.ProDrums:

                case Instrument.FiveLaneDrums:

                case Instrument.ProGuitar_17Fret:
                case Instrument.ProGuitar_22Fret:
                case Instrument.ProBass_17Fret:
                case Instrument.ProBass_22Fret:

                case Instrument.Vocals:
                case Instrument.Harmony:

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