using TMPro;
using UnityEngine;

namespace YARG.Menu
{
    public class DevWatermark : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI watermarkText;

        void Start()
        {
            // check if GlobalVariables.CurrentVersion ends with "b"
            if (GlobalVariables.CurrentVersion.Beta)
            {
                watermarkText.text = $"<b>YARG {GlobalVariables.CurrentVersion}</b>  Developer Build";
                watermarkText.gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }

            // disable script
            enabled = false;
        }
    }
}