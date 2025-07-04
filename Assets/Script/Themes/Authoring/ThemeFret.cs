using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Helpers.Authoring;

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class ThemeFret : MonoBehaviour
    {
        private const float FRET_SIZE = 2f / 5f;

        [Space]
        [SerializeField]
        private MeshMaterialIndex[] _coloredMaterials;
        [SerializeField]
        private MeshMaterialIndex[] _innerMaterials;

        [field: Space]
        [field: SerializeField]
        public EffectGroup HitEffect { get; private set; }
        [field: SerializeField]
        public EffectGroup OpenHitEffect { get; private set; }
        [field: SerializeField]
        public EffectGroup MissEffect { get; private set; }
        [field: SerializeField]
        public EffectGroup OpenMissEffect { get; private set; }
        [field: SerializeField]
        public EffectGroup SustainEffect { get; private set; }
        [field: SerializeField]
        public EffectGroup PressedEffect { get; private set; }

        [field: Space]
        [field: SerializeField]
        public Animator Animator { get; private set; }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(FRET_SIZE, 0f, FRET_SIZE));
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
        public IEnumerable<Material> GetInnerColoredMaterials()
        {
            return _innerMaterials.Select(i => i.Mesh.materials[i.MaterialIndex]);
        }
    }
}