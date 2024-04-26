using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        private CancellationTokenSource _iconCancellationToken;

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

            // Set icon
            if (_iconCancellationToken is { IsCancellationRequested: false })
            {
                _iconCancellationToken.Cancel();
            }
            _iconCancellationToken = new CancellationTokenSource();
            SetIcon(viewType, _iconCancellationToken.Token);
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

        private void SetIcon(TViewType type, CancellationToken token)
        {
            _icon.gameObject.SetActive(false);

            try
            {
                var icon = type.GetIcon();

                token.ThrowIfCancellationRequested();

                if (icon == null)
                {
                    _icon.gameObject.SetActive(false);
                }
                else
                {
                    _icon.gameObject.SetActive(true);
                    _icon.sprite = icon;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void IconClick()
        {
            ViewType.IconClick();
        }
    }
}