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
            var title = Localize.Key(LOCALIZATION_PATH, _localizationKey, "Title");
            var text = Localize.Key(LOCALIZATION_PATH, _localizationKey, "Text");

            _coordinator.Show(title, text);
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
