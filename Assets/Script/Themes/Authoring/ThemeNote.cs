using System;
using System.Collections.Generic;
using System.Linq;
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
        Kick
    }

    public class ThemeNote : MonoBehaviour
    {
        [Serializable]
        public struct MeshMaterialIndex
        {
            public MeshRenderer Mesh;
            public int MaterialIndex;
        }

        [field: Space]
        [field: SerializeField]
        public ThemeNoteType NoteType { get; private set; }
        [field: SerializeField]
        public bool StarPowerVariant { get; private set; }

        [Space]
        [SerializeField]
        private MeshMaterialIndex[] _coloredMaterials;
        [SerializeField]
        private MeshMaterialIndex[] _coloredMaterialsNoStarPower;

        [field: Space]
        [field: SerializeField]
        public bool AddExtraGlow { get; private set; }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                transform.position.AddX(-0.4f),
                transform.position.AddX(0.4f));
        }

        /// <summary>
        /// Warning! This can be slow. Cache values if needed repeatedly.
        /// </summary>
        public IEnumerable<Material> GetColoredMaterials()
        {
            return _coloredMaterials.Select(i => i.Mesh.materials[i.MaterialIndex]);
        }

        /// <summary>
        /// Warning! This can be slow. Cache values if needed repeatedly.
        /// </summary>
        public IEnumerable<Material> GetColoredMaterialsNoStarPower()
        {
            return _coloredMaterialsNoStarPower.Select(i => i.Mesh.materials[i.MaterialIndex]);
        }
    }
}