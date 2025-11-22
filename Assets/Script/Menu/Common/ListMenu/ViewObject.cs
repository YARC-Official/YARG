using System.Collections.Generic;
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
        protected CanvasGroup _canvasGroup;

        [Space]
        [SerializeField]
        protected GameObject _normalBackground;
        [SerializeField]
        protected GameObject _selectedBackground;
        [SerializeField]
        protected GameObject _categoryBackground;

        [Space]
        [SerializeField]
        protected Image _icon;
        [SerializeField]
        protected List<TextMeshProUGUI> _primaryText;
        [SerializeField]
        protected List<TextMeshProUGUI> _secondaryText;

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
            if (_primaryText != null)
            {
                foreach (var i in _primaryText)
                {
                    if (i != null)
                    {
                        i.text = viewType.GetPrimaryText(selected);
                    }
                }
            }

            if (_secondaryText != null)
            {
                foreach (var i in _secondaryText)
                {
                    if (i != null)
                    {
                        i.text = viewType.GetSecondaryText(selected);
                    }
                }
            }

            if (_icon != null)
            {
                _icon.sprite = viewType.GetIcon();
                _icon.gameObject.SetActive(_icon.sprite != null);
            }
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