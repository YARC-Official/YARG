using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        protected GameObject NormalBackground;
        [SerializeField]
        protected GameObject SelectedBackground;
        [SerializeField]
        protected GameObject CategoryBackground;

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

        protected virtual void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            NormalBackground.SetActive(false);
            SelectedBackground.SetActive(false);
            CategoryBackground.SetActive(false);

            switch (type)
            {
                case BaseViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        NormalBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Category:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        CategoryBackground.SetActive(true);
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