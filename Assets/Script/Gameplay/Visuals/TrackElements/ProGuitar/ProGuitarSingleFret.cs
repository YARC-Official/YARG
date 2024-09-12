using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public class ProGuitarSingleFret : MonoBehaviour
    {
        [SerializeField]
        private GameObject[] _heights;

        private void Awake()
        {
            YargLogger.Assert(_heights.Length == ProGuitarNoteElement.HEIGHT_VARIATIONS);
        }

        public void Initialize(int height)
        {
            foreach (var h in _heights)
            {
                h.SetActive(false);
            }

            _heights[height].SetActive(true);
        }
    }
}