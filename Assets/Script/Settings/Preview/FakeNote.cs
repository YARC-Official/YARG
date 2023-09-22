using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;

namespace YARG.Settings.Preview
{
    public class FakeNote : MonoBehaviour, IPoolable
    {
        public Pool ParentPool { get; set; }

        public GuitarNote NoteRef { get; set; }
        public FakeTrackPlayer FakeTrackPlayer { get; set; }

        private NoteGroup _currentNoteGroup;

        [SerializeField]
        private NoteGroup _noteGroup;
        [SerializeField]
        private NoteGroup _openGroup;

        private Material[] _materials;

        public void EnableFromPool()
        {
            _noteGroup.SetActive(false);
            _openGroup.SetActive(false);

            if (NoteRef.Fret != 0)
            {
                _currentNoteGroup = _noteGroup;

                // Set the position
                transform.localPosition = new Vector3(
                    BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                    0f, 0f);
            }
            else
            {
                _currentNoteGroup = _openGroup;

                // Set the position
                transform.localPosition = Vector3.zero;
            }

            _currentNoteGroup.SetActive(true);

            // Set materials
            _materials = _currentNoteGroup.GetAllMaterials();

            // Force update position and other properties
            OnSettingChanged();
            Update();

            gameObject.SetActive(true);
            SettingsMenu.Instance.SettingChanged += OnSettingChanged;
        }

        private void OnSettingChanged()
        {
            var s = SettingsManager.Settings;

            // Update fade
            foreach (var material in _materials)
            {
                material.SetFade(3f, s.CameraPreset_FadeLength.Data);
            }

            // Update color
            _currentNoteGroup.SetColorWithEmission(s.ColorProfile_Ref.FiveFretGuitar
                .GetNoteColor(NoteRef.Fret).ToUnityColor());
        }

        protected void Update()
        {
            float z =
                BasePlayer.STRIKE_LINE_POS                             // Shift origin to the strike line
                + (float) (NoteRef.Time - FakeTrackPlayer.PreviewTime) // Get time of note relative to now
                * FakeTrackPlayer.NOTE_SPEED;                          // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < -4f)
            {
                ParentPool.Return(this);
            }
        }

        public void DisableIntoPool()
        {
            SettingsMenu.Instance.SettingChanged -= OnSettingChanged;
            gameObject.SetActive(false);
        }
    }
}