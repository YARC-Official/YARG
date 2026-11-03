using System.Collections.Generic;
using System.Threading;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YARG.Menu.ListMenu
{
    public class ViewObject<TViewType> : MonoBehaviour
        where TViewType : BaseViewType
    {
        protected virtual bool AllowMarqueeScroll => false;
        protected virtual Type[] AllowedMarqueeScrollTypes => Array.Empty<Type>();

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

        private bool _marqueeActive;
        private TextMarquee _primaryMarquee;
        private TextMarquee _secondaryMarquee;

        protected bool Showing { get; private set; }

        protected TViewType ViewType;

        public virtual void Show(bool selected, TViewType viewType)
        {
            Showing = true;
            ViewType = viewType;

            // Set background
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
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

            SetIcon(viewType.GetIcon());
            if (AllowMarqueeScroll)
                SetMarqueeActive(selected && IsMarqueeScrollTypeAllowed(viewType));
        }

        public virtual void Hide()
        {
            if (_marqueeActive)
                SetMarqueeActive(false);

            Showing = false;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void LateUpdate()
        {
            if (!_marqueeActive) return;

            _primaryMarquee.Update();
            _secondaryMarquee.Update();
        }

        private bool IsMarqueeScrollTypeAllowed(TViewType viewType)
        {
            Type viewTypeType = viewType.GetType();
            foreach (Type allowedType in AllowedMarqueeScrollTypes)
            {
                if (viewTypeType == allowedType)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetMarqueeActive(bool active)
        {
            _marqueeActive = active;

            _primaryMarquee ??= new TextMarquee(_primaryText[0]);
            _secondaryMarquee ??= new TextMarquee(_secondaryText[0]);
            _primaryMarquee.SetActive(active);
            _secondaryMarquee.SetActive(active);
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

#nullable enable
        protected void SetIcon(Sprite? sprite)
#nullable disable
        {
            _icon.sprite = sprite;
            _icon.gameObject.SetActive(sprite != null);
        }

        public void IconClick()
        {
            ViewType.IconClick();
        }
    }
}
