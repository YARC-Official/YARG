using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class ProKeysChordBarElement : TrackElement<ProKeysPlayer>
    {
        public ProKeysNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        [SerializeField]
        private Transform _middleModel;

        protected override void InitializeElement()
        {
            // Get the min and max keys
            int? min = null;
            int? max = null;
            foreach (var note in NoteRef.AllNotes)
            {
                if (min is null || note.Key < min)
                {
                    min = note.Key;
                }

                if (max is null || note.Key > max)
                {
                    max = note.Key;
                }
            }

            var minPos = Player.GetNoteX(min!.Value);
            var maxPos = Player.GetNoteX(max!.Value);

            var size = maxPos - minPos;
            var mid = (minPos + maxPos) / 2f;

            var cachedTransform = _middleModel.transform;
            cachedTransform.localScale = new Vector3(size, 1f, 1f);
            cachedTransform.localPosition = new Vector3(mid, 0f, 0f);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}