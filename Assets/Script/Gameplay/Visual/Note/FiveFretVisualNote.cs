using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public class FiveFretVisualNote : VisualNote<GuitarNote, FiveFretPlayer>
    {
        // TODO: Move this to player profiles
        private static readonly Color[] Colors =
        {
            Color.magenta,
            Color.green,
            Color.red,
            Color.yellow,
            Color.blue,
            new(1f, 0.5f, 0f),
        };

        [SerializeField]
        private NoteGroup _normalGroup;
        // [SerializeField]
        // private NoteGroup _hopoGroup;
        // [SerializeField]
        // private NoteGroup _tapGroup;

        protected override void InitializeNote()
        {
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            _normalGroup.SetActive(true);
            _normalGroup.ColoredMaterial.color = Colors[NoteRef.Fret];
        }

        protected override void HideNote()
        {
            _normalGroup.SetActive(false);
            // _hopoGroup.SetActive(false);
            // _tapGroup.SetActive(false);
        }
    }
}