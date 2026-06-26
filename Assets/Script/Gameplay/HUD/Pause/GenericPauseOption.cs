using TMPro;
using UnityEngine;
using YARG.Localization;

namespace YARG.Gameplay.HUD
{
    public class GenericPauseOption : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _optionText;

        [SerializeField]
        private string _localizationKey;

        public void Awake()
        {
            if (_localizationKey == string.Empty)
            {
                return;
            }
            _optionText.text = Localize.Key(_localizationKey);
        }
    }
}