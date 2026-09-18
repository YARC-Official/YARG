using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Localization;

namespace YARG.Menu.Tooltips
{
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string LOCALIZATION_PATH = "Menu.ProfileList.Tooltip";

        [SerializeField]
        private string _localizationKey;
        
        private TooltipCoordinator _coordinator;

        private void Awake()
        {
            _coordinator = GetComponentInParent<TooltipCoordinator>();
        }

        public void Show()
        {
            var (titleParams, textParams) = GetParameters();

            var title = Localize.KeyFormat((LOCALIZATION_PATH, _localizationKey, "Title"), titleParams);
            var text = Localize.KeyFormat((LOCALIZATION_PATH, _localizationKey, "Text"), textParams);

            _coordinator.Show(title, text);
        }

        protected virtual (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (new List<string>(), new List<string>());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _coordinator.Enter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _coordinator.Exit(this, eventData);
        }
    }
}
