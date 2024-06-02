using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class ProKeysChordBarElement : TrackElement<ProKeysPlayer>
    {
        public ProKeysNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        [SerializeField]
        private float _middlePadding;
        [SerializeField]
        private float _endOffsets;

        [Space]
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private Transform _middleModel;
        [SerializeField]
        private Transform _leftModel;
        [SerializeField]
        private Transform _rightModel;

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

            // Subtract range shift offset because that will be applied to the container
            var minPos = Player.GetNoteX(min!.Value) - _middlePadding - Player.RangeShiftOffset;
            var maxPos = Player.GetNoteX(max!.Value) + _middlePadding - Player.RangeShiftOffset;

            var size = maxPos - minPos;
            var mid = (minPos + maxPos) / 2f;

            // Transform the middle model
            var cachedTransform = _middleModel.transform;
            cachedTransform.localScale = new Vector3(size, 1f, 1f);
            cachedTransform.localPosition = new Vector3(mid, 0f, 0f);

            // Transform the end models
            _leftModel.localPosition = _leftModel.localPosition.WithX(minPos - _endOffsets);
            _rightModel.localPosition = _rightModel.localPosition.WithX(maxPos + _endOffsets);

            // Update the container to the proper range shift offset
            UpdateXPosition();
        }

        public void UpdateXPosition()
        {
            _container.localPosition = _container.localPosition.WithX(Player.RangeShiftOffset);
        }

        public void CheckForChordHit()
        {
            // If the note was fully hit, remove the chord bar
            if (NoteRef.WasFullyHit())
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}