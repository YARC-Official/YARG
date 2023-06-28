using UnityEngine;

namespace YARG.Util
{
    public class DeleteAfter : MonoBehaviour
    {
        [SerializeField]
        private float deleteAfter = 1f;

        private void Start()
        {
            Destroy(gameObject, deleteAfter);
        }
    }
}