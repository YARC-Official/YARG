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
        Normal = 0,

        HOPO     = 1,
        Tap      = 2,
        Open     = 3,
        OpenHOPO = 4,

        Cymbal       = 5,
        Kick         = 6,
        Accent       = 7,
        Ghost        = 8,
        CymbalAccent = 9,
        CymbalGhost  = 10,

        White     = 11,
        Black     = 12,
        Glissando = 13,
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