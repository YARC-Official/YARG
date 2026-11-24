using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Localization;
using YARG.Venue;

namespace YARG.Menu.Content.VenueSelect
{
    public class VenuePreviewPanel : MonoBehaviour
    {
        private const int PREVIEW_LOAD_DEBOUNCE_MILLISECONDS = 60;

        [SerializeField]
        private Image _spritePreviewImage;
        [SerializeField]
        private RawImage _texturePreviewImage;
        [SerializeField]
        private GameObject _placeholderContainer;
        [SerializeField]
        private TextMeshProUGUI _placeholderText;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _titleText;
        [SerializeField]
        private TextMeshProUGUI _subtitleText;
        [SerializeField]
        private TextMeshProUGUI _descriptionText;
        [SerializeField]
        private TextMeshProUGUI _statusText;

        public event Action PinClicked;
        public event Action AvailabilityClicked;
        public event Action OpenFolderClicked;

        private CancellationTokenSource _previewLoadCts;
        private Texture2D _previewTexture;
        private VenueDescriptor _venue;

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        public void Show(VenueDescriptor venue)
        {
            CancelPendingPreviewLoad();
            ReleasePreviewTexture();

            _venue = venue;
            if (venue == null)
            {
                Clear();
                return;
            }

            _titleText.text = venue.DisplayName;
            _subtitleText.text = BuildSubtitle(venue);
            _descriptionText.text = venue.Description ?? string.Empty;
            _statusText.text = BuildStatus(venue);

            ShowPreview(venue);
        }

        public void Clear()
        {
            CancelPendingPreviewLoad();
            ReleasePreviewTexture();

            _venue = null;

            _titleText.text = string.Empty;
            _subtitleText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _statusText.text = string.Empty;

            _spritePreviewImage.sprite = null;
            _spritePreviewImage.gameObject.SetActive(false);
            _texturePreviewImage.gameObject.SetActive(false);
            _placeholderContainer.SetActive(false);
            _placeholderText.text = string.Empty;
        }

        private void ShowPreview(VenueDescriptor venue)
        {
            if (venue.PreviewSprite != null)
            {
                _spritePreviewImage.sprite = venue.PreviewSprite;
                _spritePreviewImage.gameObject.SetActive(true);
                _texturePreviewImage.gameObject.SetActive(false);
                _placeholderContainer.SetActive(false);
                return;
            }

            _spritePreviewImage.gameObject.SetActive(false);
            _texturePreviewImage.gameObject.SetActive(false);

            // The placeholder stays visible until a sibling image has actually been decoded
            _placeholderText.text = venue.Info.Identifier;
            _placeholderContainer.SetActive(true);

            if (!string.IsNullOrEmpty(venue.PreviewImagePath))
            {
                StartPreviewLoad(venue);
            }
        }

        private void StartPreviewLoad(VenueDescriptor venue)
        {
            var cts = new CancellationTokenSource();
            _previewLoadCts = cts;
            LoadPreviewAsync(venue, cts.Token).Forget();
        }

        private async UniTaskVoid LoadPreviewAsync(VenueDescriptor venue, CancellationToken cancellationToken)
        {
            try
            {
                // Debounced so that rapid list scrolling does not thrash the disk
                bool cancelled = await UniTask.Delay(PREVIEW_LOAD_DEBOUNCE_MILLISECONDS,
                    cancellationToken: cancellationToken).SuppressCancellationThrow();
                if (cancelled)
                {
                    return;
                }

                var texture = await LocalVenueMetadataReader.LoadPreviewTextureAsync(venue.PreviewImagePath,
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_venue, venue))
                {
                    if (texture != null)
                    {
                        Destroy(texture);
                    }

                    return;
                }

                if (texture == null)
                {
                    return;
                }

                ReleasePreviewTexture();

                _previewTexture = texture;
                _texturePreviewImage.texture = texture;

                // Local thumbnails are not guaranteed to be 16:9, so the frame follows the image
                var fitter = _texturePreviewImage.GetComponent<AspectRatioFitter>();
                if (fitter != null && texture.height > 0)
                {
                    fitter.aspectRatio = texture.width / (float) texture.height;
                }

                _texturePreviewImage.gameObject.SetActive(true);
                _placeholderContainer.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                // Selection changed or the menu closed while the texture was being read
            }
        }

        private void CancelPendingPreviewLoad()
        {
            if (_previewLoadCts == null)
            {
                return;
            }

            _previewLoadCts.Cancel();
            _previewLoadCts.Dispose();
            _previewLoadCts = null;
        }

        private void ReleasePreviewTexture()
        {
            if (_texturePreviewImage != null)
            {
                _texturePreviewImage.texture = null;
            }

            if (_previewTexture != null)
            {
                Destroy(_previewTexture);
                _previewTexture = null;
            }
        }

        private void OnPinButtonClicked() => PinClicked?.Invoke();

        private void OnAvailabilityButtonClicked() => AvailabilityClicked?.Invoke();

        private void OnOpenFolderButtonClicked() => OpenFolderClicked?.Invoke();

        private static string BuildSubtitle(VenueDescriptor venue)
        {
            var author = string.IsNullOrWhiteSpace(venue.Author)
                ? Localize.Key("Menu.Content.VenueSelect.UnspecifiedAuthor")
                : venue.Author;

            var source = Localize.Key(VenueViewType.GetSourceKey(venue.Info.Source));

            return author + "  \u00b7  " + source;
        }

        private static string BuildStatus(VenueDescriptor venue)
        {
            if (VenueRegistry.IsPinned(venue.Info))
            {
                return Localize.Key("Menu.Content.VenueSelect.States.Pinned");
            }

            return Localize.Key(VenueRegistry.GetAvailabilityState(venue) switch
            {
                VenueAvailabilityState.Disabled => "Menu.Content.VenueSelect.States.Disabled",
                VenueAvailabilityState.HintOnly => "Menu.Content.VenueSelect.States.HintOnly",
                _                               => "Menu.Content.VenueSelect.States.InRandomRotation",
            });
        }
    }
}
