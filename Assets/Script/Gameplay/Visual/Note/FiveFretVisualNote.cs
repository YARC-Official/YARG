using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public class FiveFretVisualNote : VisualNote<GuitarNote, FiveFretPlayer>
    {
        [SerializeField]
        private MeshRenderer _noteRenderer;
        [SerializeField]
        private int _noteMiddleIndex;

        protected override void InitializeNote()
        {
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            _noteRenderer.materials[_noteMiddleIndex].color = new Color(Random.value, Random.value, Random.value);
        }
    }
}