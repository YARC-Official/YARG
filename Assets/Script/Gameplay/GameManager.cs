using System.Collections.Generic;
using TagLib.Mpeg;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Player;
using YARG.Song;

namespace YARG.Gameplay
{
    public class GameManager : MonoBehaviour
    {

        [Header("Instrument Prefabs")]
        [SerializeField]
        private GameObject _fiveFretGuitarPrefab;

        [SerializeField]
        private GameObject _sixFretGuitarPrefab;

        [SerializeField]
        private GameObject _fourLaneDrumsPrefab;

        [SerializeField]
        private GameObject _fiveLaneDrumsPrefab;

        [SerializeField]
        private GameObject _proGuitarPrefab;

        public SongChart Chart { get; private set; }

        public double SongStartTime { get; private set; }
        public double SongLength    { get; private set; }

        public bool Paused { get; private set; }

        private List<BasePlayer> _players;
        private List<Beat> _beats;

        private void Awake()
        {
            _beats = new List<Beat>();
            Chart = SongChart.FromFile(GlobalVariables.Instance.CurrentSong.NotesFile);

            var beatHandler = new BeatHandler(null);
            beatHandler.GenerateBeats();
            _beats = beatHandler.Beats;

            LoadSong();
            CreatePlayers();
        }

        private void LoadSong()
        {
            var song = GlobalVariables.Instance.CurrentSong;

            if (song is ExtractedConSongEntry exConSong)
            {
                GlobalVariables.AudioManager.LoadMogg(exConSong, GlobalVariables.Instance.SongSpeed);
            }
            else
            {
                var stems = AudioHelpers.GetSupportedStems(song.Location);
                GlobalVariables.AudioManager.LoadSong(stems, GlobalVariables.Instance.SongSpeed);
            }

            SongLength = GlobalVariables.AudioManager.AudioLengthD;
        }

        private void CreatePlayers()
        {
            _players = new List<BasePlayer>();

            int count = -1;
            foreach (var player in GlobalVariables.Instance.Players)
            {
                count++;
                GameObject prefab;

                switch (player.Profile.InstrumentType)
                {
                    case GameMode.FiveFretGuitar:
                        prefab = _fiveFretGuitarPrefab;
                        break;
                    case GameMode.SixFretGuitar:
                        prefab = _sixFretGuitarPrefab;
                        break;
                    case GameMode.FourLaneDrums:
                        prefab = _fourLaneDrumsPrefab;
                        break;
                    case GameMode.FiveLaneDrums:
                        prefab = _fiveLaneDrumsPrefab;
                        break;
                    case GameMode.ProGuitar:
                        prefab = _proGuitarPrefab;
                        break;
                    default:
                        continue;
                }

                var playerObject = Instantiate(prefab, new Vector3(count * 25f, 100f, 0f), prefab.transform.rotation);
                var basePlayer = playerObject.GetComponent<BasePlayer>();
                basePlayer.Player = player;

                LoadChart(player, basePlayer);

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

    }
}