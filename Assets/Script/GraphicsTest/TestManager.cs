using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Playback;
using YARG.Player;
using YARG.Rendering;

namespace YARG.GraphicsTest
{
    public partial class TestManager : MonoBehaviour
    {
        private const double DEFAULT_VOLUME = 1.0;

        [Space]
        [SerializeField]
        private Camera _camera;

        [Space]
        [SerializeField]
        private Mesh _noteMesh;
        [SerializeField]
        private Material _noteMaterial;

        private List<GuitarNote> _notes;
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
            _noteManager?.Dispose();
        }

        private async void Start()
        {
            await Load();

            var track = _chart.GetFiveFretTrack(_player.Profile.CurrentInstrument);
            _notes = track.Difficulties[_player.Profile.CurrentDifficulty].Notes.DuplicateNotes();

            var instancer = new MeshInstancer(_noteMesh, _noteMaterial, 65536);
            _noteManager = new(instancer, _notes, _player.Profile.NoteSpeed, 0f, 3.0, -0.070);
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
