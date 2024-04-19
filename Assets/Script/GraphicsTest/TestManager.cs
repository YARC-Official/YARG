using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.GraphicsTest.Instancing;
using YARG.Integration;
using YARG.Menu.Navigation;
using YARG.Playback;
using YARG.Player;

namespace YARG.GraphicsTest
{
    public partial class TestManager : MonoBehaviour
    {
        private enum InstancingMode
        {
            Standard,
        }

        private const double DEFAULT_VOLUME = 1.0;

        [SerializeField]
        private InstancingMode _mode;

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

        private InstancingMode _currentMode;

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

            SetMode(_mode);
        }

        private void Update()
        {
#if UNITY_EDITOR
            // Update if mode changed in the inspector
            if (_mode != _currentMode)
                SetMode(_mode);
#endif

            _songRunner.Update();
            _noteManager.Update(_songRunner.SongTime);

            // Check for song end
            if (_songRunner.SongTime >= _songLength)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }
        }

        private void SetMode(InstancingMode mode)
        {
            MeshInstancer instancer = mode switch
            {
                InstancingMode.Standard => new StandardMeshInstancer(_noteMesh, _noteMaterial,
                    shadowMode: ShadowCastingMode.Off, receiveShadows: false, lightProbing: LightProbeUsage.Off),

                _ => throw new Exception("Unreachable.")
            };

            _noteManager?.Dispose();
            _noteManager = new(instancer, _notes, _player.Profile.NoteSpeed, 0f, 3.0, -0.070);

            _currentMode = mode;
        }
    }
}
