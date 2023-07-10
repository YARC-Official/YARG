using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Player;

namespace YARG.Gameplay
{
    public class GameManager : MonoBehaviour
    {

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

            song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.SongSpeed);

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