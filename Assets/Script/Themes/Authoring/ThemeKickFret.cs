using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class ThemeKickFret : MonoBehaviour
    {
        [Space]
        [SerializeField]
        private MeshMaterialIndex[] _coloredMaterials;

        [field: Space]
        [field: SerializeField]
        public Animator Animator { get; private set; }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;

            var pos = transform.position;
            Gizmos.DrawLine(pos.AddZ(-0.25f), pos.AddZ(0.25f));
        }

        /// <summary>
        /// Warning! This can be slow. Cache values if needed repeatedly.
        /// </summary>
        public IEnumerable<Material> GetColoredMaterials()
        {
            return _coloredMaterials.Select(i => i.Mesh.materials[i.MaterialIndex]);
        }
    }
}