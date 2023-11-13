using TMPro;
using UnityEngine;

namespace YARG.Menu.Persistent
{
    public class DevWatermark : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _watermarkText;

        private void Start()
        {
#if UNITY_EDITOR
            _watermarkText.text = $"<b>YARG {GlobalVariables.CURRENT_VERSION}</b> Unity Editor";
#elif YARG_TEST_BUILD
            _watermarkText.text = $"<b>YARG {GlobalVariables.CURRENT_VERSION}</b> Development Build";
#elif YARG_NIGHTLY_BUILD
            _watermarkText.text = $"<b>YARG {GlobalVariables.CURRENT_VERSION}</b> Nightly Build";
#else
            gameObject.SetActive(false);
#endif
        }
    }
}