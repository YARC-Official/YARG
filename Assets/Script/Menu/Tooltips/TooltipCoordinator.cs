using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private Vector2 _pointerPosition;


        public void Show(string title, string text)
        {
            tooltipTitlePanel.text = title;
            tooltipTextPanel.text = text;
        }

        public void Clear()
        {
            Show(string.Empty, string.Empty);
        }

        public void Enter(TooltipTrigger trigger, PointerEventData eventData)
        {
            _pointerPosition = eventData.position;

            if (trigger == _currentTrigger)
            {
                return;
            }

            _currentTrigger = trigger;
            trigger.Show();
        }

        public void Exit(TooltipTrigger trigger, PointerEventData eventData)
        {
            _pointerPosition = eventData.position;

            if (_currentTrigger != trigger)
            {
                return;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (EventSystem.current is null)
            {
                Clear();
                _currentTrigger = null;
                return;
            }

            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = _pointerPosition
            };

            var trigger = FindTooltipTrigger(pointerEventData);

            if (trigger == _currentTrigger)
            {
                return;
            }

            _currentTrigger = trigger;

            if (trigger is null)
            {
                Clear();
            }
            else
            {
                trigger.Show();
            }
        }

        public void TriggerDisabled(TooltipTrigger trigger)
        {
            if (_currentTrigger == trigger)
            {
                Refresh();
            }
        }

#nullable enable
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
#nullable disable
    }
}
