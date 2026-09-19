using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Localization;

namespace YARG.Menu.Tooltips
{
    // If your tooltip doesn't need any parameterization ({0}, {1}, etc.), then add this as a component directly
    // and just populate the localizationKey field.
    //
    // If you do need parameterization, make a subclass with SerializeFields for all the necessary information to
    // derive the parameters and override GetParameters() to return them.
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
