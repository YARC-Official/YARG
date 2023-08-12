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
            if (GlobalVariables.CurrentVersion.IsPrerelease)
            {
                watermarkText.text = $"<b>YARG {GlobalVariables.CurrentVersion}</b>  Development Build";
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