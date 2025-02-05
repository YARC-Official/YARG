using System;
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
        private MeshRenderer _oneLaneTalkie;
        [SerializeField]
        private MeshRenderer _twoLaneTalkie;
        [SerializeField]
        private MeshRenderer _threeLaneTalkie;

        protected override void InitializeElement()
        {
            // Get the right talkie mesh
            int lanes = GameManager.VocalTrack.LyricLaneCount;
            var mesh = lanes switch
            {
                1 => _oneLaneTalkie,
                2 => _twoLaneTalkie,
                3 => _threeLaneTalkie,
                _ => throw new Exception("Unreachable.")
            };

            // Hide all of the other meshes
            _oneLaneTalkie.gameObject.SetActive(lanes == 1);
            _twoLaneTalkie.gameObject.SetActive(lanes == 2);
            _threeLaneTalkie.gameObject.SetActive(lanes == 3);

            // Set the color
            var color = Player.VocalTrack.Colors[NoteRef.HarmonyPart];
            mesh.material.color = color.WithAlpha(ALPHA_VALUE);

            // Update the size of the talkie
            var transform = mesh.transform;
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