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
        private MeshRenderer _soloTalkie;
        [SerializeField]
        private MeshRenderer _harmonyTalkie;

        protected override void InitializeElement()
        {
            // Get and show the correct mesh (solo vs harmony)
            MeshRenderer mesh;
            if (GameManager.VocalTrack.HarmonyShowing)
            {
                mesh = _harmonyTalkie;
                _harmonyTalkie.gameObject.SetActive(true);
                _soloTalkie.gameObject.SetActive(false);
            }
            else
            {
                mesh = _soloTalkie;
                _soloTalkie.gameObject.SetActive(true);
                _harmonyTalkie.gameObject.SetActive(false);
            }

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