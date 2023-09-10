using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;

namespace YARG.Settings.Preview
{
    public class FakeNote : MonoBehaviour, IPoolable
    {
        public Pool ParentPool { get; set; }

        public GuitarNote NoteRef { get; set; }
        public FakeTrackPlayer FakeTrackPlayer { get; set; }

        [SerializeField]
        private NoteGroup _noteGroup;

        private Material[] _materials;

        public void EnableFromPool()
        {
            // Set the position
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            // Set color and materials
            _noteGroup.ColoredMaterial.color = ColorProfile.Default.
                FiveFretGuitar.GetNoteColor(NoteRef.Fret).ToUnityColor();
            _materials = _noteGroup.GetAllMaterials();

            // Force update position
            Update();


            gameObject.SetActive(true);
        }

        protected void Update()
        {
            // Update fade for settings
            foreach (var material in _materials)
            {
                material.SetFade(3f, SettingsManager.Settings.CameraPreset_FadeLength.Data);
            }

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
            gameObject.SetActive(false);
        }
    }
}