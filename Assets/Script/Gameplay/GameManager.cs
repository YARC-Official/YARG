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
                var basePlayer = playerObject.GetComponent<BasePlayer>();
                basePlayer.Player = player;


                LoadChart(player, basePlayer);

                _trackViewManager.CreateTrackView(basePlayer);
                _players.Add(basePlayer);
            }
        }

        private void LoadChart(YargPlayer yargPlayer, BasePlayer basePlayer)
        {
            switch (yargPlayer.Profile.InstrumentType)
            {
                case GameMode.FiveFretGuitar:
                    var notes = Chart.FiveFretGuitar.Difficulties[yargPlayer.Profile.Difficulty].Notes;
                    (basePlayer as FiveFretPlayer)?.Initialize(yargPlayer, notes);
                    break;
                case GameMode.SixFretGuitar:
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                case GameMode.ProGuitar:
                case GameMode.Vocals:
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