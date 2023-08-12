using TMPro;
using UnityEngine;

namespace YARG.Menu.Persistent
{
    public class DevWatermark : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI watermarkText;

        void Start()
        {
            if (GlobalVariables.CurrentVersion.IsPrerelease && GlobalVariables.CurrentVersion.PrereleaseText.Contains("dev"))
            {
                watermarkText.text = $"<b>YARG {GlobalVariables.CurrentVersion}</b>  Development Build";
                watermarkText.gameObject.SetActive(true);
            }
            else if (GlobalVariables.CurrentVersion.IsPrerelease)
            {
                watermarkText.text = $"<b>YARG {GlobalVariables.CurrentVersion}</b>  Pre-Release";
                watermarkText.gameObject.SetActive(true);
            }
            else
            {
                watermarkText.text = "";
                gameObject.SetActive(false);
            }

            // disable script
            enabled = false;
        }
    }
}