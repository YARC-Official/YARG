using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public class VocalTalkieElement : VocalElement
    {
        private const float ALPHA_VALUE = 0.2f;

        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        protected override float RemovePointOffset => VocalTrack.GetPosForTime(NoteRef.TotalTimeLength);

        [SerializeField]
        private MeshRenderer _quad;

        protected override void InitializeElement()
        {
            // Set the color
            var color = VocalTrack.Colors[NoteRef.HarmonyPart];
            _quad.material.color = color.WithAlpha(ALPHA_VALUE);

            // Update the size of the talkie
            var transform = _quad.transform;
            float length = VocalTrack.GetPosForTime(NoteRef.TotalTimeEnd) - VocalTrack.GetPosForTime(NoteRef.Time);
            transform.localScale = transform.localScale.WithX(length);
            transform.localPosition = transform.localPosition.WithX(length / 2f);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}