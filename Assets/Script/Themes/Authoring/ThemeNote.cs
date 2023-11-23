using UnityEngine;

namespace YARG.Themes
{
    // WARNING: DO NOT CHANGE THIS!
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public enum ThemeNoteType
    {
        Normal,

        HOPO,
        Tap,
        Open,
        OpenHOPO,

        Cymbal,
        Kick,
    }

    public class ThemeNote : MonoBehaviour
    {
        [field: Space]
        [field: SerializeField]
        public ThemeNoteType NoteType { get; private set; }

        [field: Space]
        [field: SerializeField]
        public MeshRenderer ColoredMaterialRenderer { get; private set; }

        [field: SerializeField]
        public int ColoredMaterialIndex { get; private set; }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                transform.position.AddX(-0.4f),
                transform.position.AddX(0.4f));
        }
    }
}