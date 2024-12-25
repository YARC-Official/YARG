using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalPercussionElement : VocalElement
    {
        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        [SerializeField]
        private GameObject _mesh;

        protected override void InitializeElement()
        {
            _mesh.SetActive(true);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
            _mesh.SetActive(false);
        }
    }
}