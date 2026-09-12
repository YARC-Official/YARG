using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Windows;

namespace YARG.Menu.Tooltips
{
    public class TooltipCoordinator : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI tooltipTitlePanel;
        [SerializeField]
        private TextMeshProUGUI tooltipTextPanel;

        private TooltipTrigger _currentTrigger;

        public void Show(string title, string text)
        {
            tooltipTitlePanel.text = title;
            tooltipTextPanel.text = text;
        }

        public void Clear()
        {
            Show(string.Empty, string.Empty);
        }

        public void Enter(TooltipTrigger trigger)
        {
            if (trigger == _currentTrigger)
            {
                return;
            }

            _currentTrigger = trigger;
            trigger.Show();
        }

        public void Exit(TooltipTrigger trigger, PointerEventData eventData)
        {
            if (_currentTrigger != trigger)
            {
                return;
            }

            _currentTrigger = FindTooltipTrigger(eventData);

            if (_currentTrigger is null)
            {
                Clear();
            }
            else
            {
                _currentTrigger.Show();
            }
        }

        private TooltipTrigger? FindTooltipTrigger(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();

            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                var trigger = result.gameObject.GetComponentInParent<TooltipTrigger>();
                if (trigger is not null)
                {
                    return trigger;
                }
            }

            return null;
        }
    }
}
