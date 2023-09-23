using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    public class SongView : MonoBehaviour
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
        private GameObject _secondaryTextContainer;
        [SerializeField]
        private GameObject _asMadeFamousByTextContainer;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _primaryText;
        [SerializeField]
        private TextMeshProUGUI[] _secondaryText;
        [SerializeField]
        private TextMeshProUGUI _sideText;
        [SerializeField]
        private Image _icon;

        private int _relativeSongIndex;
        private CancellationTokenSource _cancellationTokenSource;

        public void Init(int relativeSongIndex)
        {
            _relativeSongIndex = relativeSongIndex;
        }

        public void UpdateView()
        {
            int realIndex = MusicLibraryMenu.Instance.SelectedIndex + _relativeSongIndex;
            bool selected = _relativeSongIndex == 0;

            if (realIndex < 0 || realIndex >= MusicLibraryMenu.Instance.ViewList.Count)
            {
                _canvasGroup.alpha = 0f;
                return;
            }

            _canvasGroup.alpha = 1f;

            var viewType = MusicLibraryMenu.Instance.ViewList[realIndex];

            // Set text
            _primaryText.text = viewType.GetPrimaryText(selected);
            _sideText.text = viewType.GetSideText(selected);

            // Set secondary text (there is multiple)
            foreach (var text in _secondaryText)
            {
                text.text = viewType.GetSecondaryText(selected);
            }

            // Set icon
            if (_cancellationTokenSource is { IsCancellationRequested: false })
            {
                _cancellationTokenSource.Cancel();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            SetIcon(viewType, _cancellationTokenSource.Token).Forget();

            // Set secondary text type
            _secondaryTextContainer.SetActive(!viewType.UseAsMadeFamousBy);
            _asMadeFamousByTextContainer.SetActive(viewType.UseAsMadeFamousBy);

            SetBackground(viewType.Background, selected);
        }

        private async UniTask SetIcon(ViewType type, CancellationToken token)
        {
            _icon.gameObject.SetActive(false);

            try
            {
                var icon = await type.GetIcon();

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

        private void SetBackground(ViewType.BackgroundType type, bool selected)
        {
            _normalBackground.SetActive(false);
            _selectedBackground.SetActive(false);
            _categoryBackground.SetActive(false);

            switch (type)
            {
                case ViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        _selectedBackground.SetActive(true);
                    }
                    else
                    {
                        _normalBackground.SetActive(true);
                    }

                    break;
                case ViewType.BackgroundType.Category:
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

        public void SecondaryTextClick()
        {
            int realIndex = MusicLibraryMenu.Instance.SelectedIndex + _relativeSongIndex;
            var viewType = MusicLibraryMenu.Instance.ViewList[realIndex];

            viewType.SecondaryTextClick();
        }

        public void IconClick()
        {
            int realIndex = MusicLibraryMenu.Instance.SelectedIndex + _relativeSongIndex;
            var viewType = MusicLibraryMenu.Instance.ViewList[realIndex];

            viewType.IconClick();
        }
    }
}