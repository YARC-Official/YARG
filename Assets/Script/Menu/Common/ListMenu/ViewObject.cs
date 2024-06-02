﻿using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.ListMenu
{
    public class ViewObject<TViewType> : MonoBehaviour
        where TViewType : BaseViewType
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Space]
        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _selectedBackground;
        [SerializeField]
        private GameObject _categoryBackground;

        [Space]
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private List<TextMeshProUGUI> _primaryText;
        [SerializeField]
        private List<TextMeshProUGUI> _secondaryText;

        protected bool Showing { get; private set; }

        protected TViewType ViewType;

        public virtual void Show(bool selected, TViewType viewType)
        {
            Showing = true;
            ViewType = viewType;

            // Set background
            _canvasGroup.alpha = 1f;
            SetBackground(selected, viewType.Background);

            // Set text
            foreach(var i in _primaryText)
            {
                i.text = viewType.GetPrimaryText(selected);
            }
            foreach(var i in _secondaryText)
            {
                i.text = viewType.GetSecondaryText(selected);
            }

            _icon.sprite = viewType.GetIcon();
            _icon.gameObject.SetActive(_icon.sprite != null);
        }

        public virtual void Hide()
        {
            Showing = false;
            _canvasGroup.alpha = 0f;
        }

        private void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            _normalBackground.SetActive(false);
            _selectedBackground.SetActive(false);
            _categoryBackground.SetActive(false);

            switch (type)
            {
                case BaseViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        _selectedBackground.SetActive(true);
                    }
                    else
                    {
                        _normalBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Category:
                    if (selected)
                    {
                        _selectedBackground.SetActive(true);
                    }
                    else
                    {
                        _categoryBackground.SetActive(true);
                    }

                    break;
            }
        }

        public void IconClick()
        {
            ViewType.IconClick();
        }
    }
}