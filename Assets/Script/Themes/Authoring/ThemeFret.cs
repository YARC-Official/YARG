using UnityEngine;

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class ThemeFret : MonoBehaviour
    {
        private const float FRET_SIZE = 2f / 5f;

        [field: Space]
        [field: SerializeField]
        public MeshRenderer ColoredMaterialRenderer { get; private set; }

        [field: SerializeField]
        public int ColoredMaterialIndex { get; private set; }
        [field: SerializeField]
        public int ColoredInnerMaterialIndex { get; private set; }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(FRET_SIZE, 0f, FRET_SIZE));
        }
    }
}