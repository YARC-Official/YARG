using TMPro;
using UnityEngine;

namespace YARG.Localization
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizeText : MonoBehaviour
    {
        [SerializeField]
        private string _localizationKey;

        private void Awake()
        {
            GetComponent<TextMeshProUGUI>().text = Localize.Key(_localizationKey);
            enabled = false;
        }
    }
}