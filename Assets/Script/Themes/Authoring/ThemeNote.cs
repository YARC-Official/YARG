using System.Collections.Generic;
using UnityEngine;

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields (or the enum) in this file will result
    // in older themes not working properly. Only change if you need to.

    public enum ThemeNoteType
    {
        Normal,

        HOPO,
        Tap,
        Open,
        OpenHOPO,

        Cymbal,
        Kick,

        White,
        Black,
        Glissando,
    }

    public class ThemeNote : MonoBehaviour
    {
        [field: Space]
        [field: SerializeField]
        public ThemeNoteType NoteType { get; private set; }
        [field: SerializeField]
        public bool StarPowerVariant { get; private set; }

        [Space]
        [SerializeField]
        private MeshEmissionMaterialIndex[] _coloredMaterials;
        [SerializeField]
        private MeshEmissionMaterialIndex[] _coloredMaterialsNoStarPower;

        public IEnumerable<MeshEmissionMaterialIndex> ColoredMaterials => _coloredMaterials;
        public IEnumerable<MeshEmissionMaterialIndex> ColoredMaterialsNoStarPower => _coloredMaterialsNoStarPower;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                transform.position.AddX(-0.4f),
                transform.position.AddX(0.4f));
        }
    }
}