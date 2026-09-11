using System.Collections.Generic;
using UnityEngine;

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    // WARNING: Changing this will break code!
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

        Wildcard = 14,

        DedicatedLaneKick = 15,

        BlackGlissando = 16,
        
        // 6-fret guitar specific types
        SixFretDown      = 17,
        SixFretDownHOPO  = 18,
        SixFretDownTap   = 19,
        SixFretUpHOPO    = 20,
        SixFretUpTap     = 21,
        SixFretUp        = 22,
        SixFretBarre     = 23,
        SixFretBarreTap  = 24,
        SixFretBarreHOPO = 25,
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
        [SerializeField]
        private MeshEmissionMaterialIndex[] _coloredMetalMaterials;

        [Space]
        [SerializeField]
        private MeshEmissionMaterialIndex[] _coloredSecondaryMaterials;

        public IEnumerable<MeshEmissionMaterialIndex> ColoredMaterials => _coloredMaterials;
        public IEnumerable<MeshEmissionMaterialIndex> ColoredMaterialsNoStarPower => _coloredMaterialsNoStarPower;
        public IEnumerable<MeshEmissionMaterialIndex> ColoredMetalMaterials => _coloredMetalMaterials;
        public IEnumerable<MeshEmissionMaterialIndex> ColoredSecondaryMaterials => _coloredSecondaryMaterials;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                transform.position.AddX(-0.4f),
                transform.position.AddX(0.4f));
        }
    }
}
