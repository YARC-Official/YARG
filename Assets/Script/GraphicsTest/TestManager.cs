using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Playback;
using YARG.Player;

namespace YARG.GraphicsTest
{
    public partial class TestManager : MonoBehaviour
    {
        private const double DEFAULT_VOLUME = 1.0;

        [SerializeField]
        private Camera _camera;

        [Space]
        [SerializeField]
        private Mesh _noteMesh;
        [SerializeField]
        private Material _noteMaterial;

        private NoteManager _noteManager;

        private StemMixer _mixer;
        private SongRunner _songRunner;

        private SongEntry _song;
        private SongChart _chart;
        private double _songLength;

        private YargPlayer _player;

        private void Awake()
        {
            _song = GlobalVariables.State.CurrentSong;

            Navigator.Instance.PopAllSchemes();
            GameStateFetcher.SetSongEntry(_song);

            if (_song is null)
            {
                YargLogger.LogError("Null song set when loading gameplay!");

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            _player = PlayerContainer.Players[0];
        }

        private void OnDestroy()
        {
            YargLogger.LogInfo("Exiting song");

            _mixer?.Dispose();
            _songRunner?.Dispose();
        }

        private async void Start()
        {
            await Load();

            var track = _chart.GetFiveFretTrack(_player.Profile.CurrentInstrument);
            var notes = track.Difficulties[_player.Profile.CurrentDifficulty].Notes.DuplicateNotes();
            _noteManager = new(
                _noteMesh, _noteMaterial, _camera,
                notes, _player.Profile.NoteSpeed, 0,
                3, -0.070
            );
        }

        private void Update()
        {
            _songRunner.Update();
            _noteManager.Update(_songRunner.SongTime);

            // Check for song end
            if (_songRunner.SongTime >= _songLength)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }
        }
    }
}
