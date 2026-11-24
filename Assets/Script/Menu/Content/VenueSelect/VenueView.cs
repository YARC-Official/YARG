using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using YARG.Menu.ListMenu;
using YARG.Settings;
using YARG.Venue;

namespace YARG.Menu.Content.VenueSelect
{
    public class VenueView : ViewObject<VenueViewType>
    {
        [SerializeField]
        private Image _pinImage;
        [SerializeField]
        private Image _locationImage;

        [Space]
        [SerializeField]
        private Sprite _pinSprite;
        [SerializeField]
        private Sprite _remoteSprite;
        [SerializeField]
        private Sprite _remoteDownloadedSprite;
        [SerializeField]
        private Sprite _localSprite;

        private const float DISABLED_ALPHA = 0.6f;

        private CanvasGroup _rowCanvasGroup;

        public void PrimaryButtonClick() => ViewType?.ViewClick();

        public override void Show(bool selected, VenueViewType viewType)
        {
            base.Show(selected, viewType);

            // Set the sprites
            if (viewType != null)
            {
                var venueInfo = viewType.Descriptor.Info;

                if (VenueRegistry.IsPinned(venueInfo))
                {
                    _pinImage.sprite = _pinSprite;
                    _pinImage.gameObject.SetActive(true);
                }
                else
                {
                    _pinImage.gameObject.SetActive(false);
                }

                SetSourceSprite(viewType).Forget();
            }

            // ViewObject resets the alpha in Show, so the disabled look has to be applied afterwards
            if (viewType != null &&
                VenueRegistry.GetAvailabilityState(viewType.Descriptor) == VenueAvailabilityState.Disabled)
            {
                RowCanvasGroup.alpha = DISABLED_ALPHA;
            }
        }

        private CanvasGroup RowCanvasGroup => _rowCanvasGroup != null ? _rowCanvasGroup : _rowCanvasGroup = GetComponent<CanvasGroup>();

        private async UniTaskVoid SetSourceSprite(VenueViewType viewType)
        {
            AsyncOperationHandle<long> sizeHandle = default;
            try
            {
                var venueInfo = viewType.Descriptor.Info;

                if (venueInfo.Source == VenueEntrySource.Addressable)
                {
                    // Check if already downloaded
                    sizeHandle = Addressables.GetDownloadSizeAsync(venueInfo.Identifier);
                    var size = await sizeHandle.ToUniTask();

                    if (sizeHandle.Status == AsyncOperationStatus.Succeeded && size == 0)
                    {
                        _locationImage.sprite = _remoteDownloadedSprite;
                    }
                    else
                    {
                        _locationImage.sprite = _remoteSprite;
                    }
                }
                else
                {
                    // Must be local
                    _locationImage.sprite = _localSprite;
                }
            }
            catch (OperationCanceledException)
            {
                // Task was canceled, but we don't care
            }
            finally
            {
                if (sizeHandle.IsValid())
                {
                    sizeHandle.Release();
                }
            }
        }
    }
}
