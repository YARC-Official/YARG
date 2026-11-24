using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Venue;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Venue;

namespace YARG.Menu.Content.VenueSelect
{
    public enum VenueFilter
    {
        All,
        Remote,
        Local,
        Disabled,
    }

    public class VenueSelectMenu : ListMenu<VenueViewType, VenueView>
    {
        [SerializeField]
        private VenuePreviewPanel _previewPanel;
        [SerializeField]
        private TextMeshProUGUI _summaryText;
        [SerializeField]
        private TextMeshProUGUI _filterText;
        [SerializeField]
        private GameObject _bannerContainer;
        [SerializeField]
        private TextMeshProUGUI _bannerText;
        [SerializeField]
        private GameObject _emptyStateContainer;
        [SerializeField]
        private ColoredButton _emptyStateOpenFolderButton;
        [SerializeField]
        private ColoredButton _filterButton;

        private readonly List<VenueDescriptor> _allVenues = new();

        private VenueBrowserSession _session;
        private CancellationTokenSource _lifecycleCts;
        private VenueFilter _filter = VenueFilter.All;
        private bool _schemePushed;
        private bool _refreshOnFocus;
        private int _loadGeneration;

        protected override int ExtraListViewPadding => 10;

        protected override void Awake()
        {
            base.Awake();

            if (_previewPanel != null)
            {
                _previewPanel.PinClicked += TogglePinSelected;
                _previewPanel.AvailabilityClicked += CycleAvailabilitySelected;
                _previewPanel.OpenFolderClicked += OpenVenueFolder;
            }

            if (_emptyStateOpenFolderButton != null)
            {
                _emptyStateOpenFolderButton.OnClick.AddListener(OpenVenueFolder);
            }

            if (_filterButton != null)
            {
                _filterButton.OnClick.AddListener(CycleFilter);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Rows from the previous session belong to a venue-meta handle that is already released
            _allVenues.Clear();
            RequestViewListUpdate();

            UpdateNavigationScheme();

            _lifecycleCts = new CancellationTokenSource();
            ReloadVenuesAsync().Forget();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            CancelLifecycle();
            DisposeSession();
            PopNavigationScheme();

            if (_previewPanel != null)
            {
                _previewPanel.Clear();
            }
        }

        private void OnDestroy()
        {
            if (_previewPanel != null)
            {
                _previewPanel.PinClicked -= TogglePinSelected;
                _previewPanel.AvailabilityClicked -= CycleAvailabilitySelected;
                _previewPanel.OpenFolderClicked -= OpenVenueFolder;
            }

            CancelLifecycle();
            DisposeSession();
            PopNavigationScheme();
        }

        protected override void OnSelectedIndexChanged()
        {
            base.OnSelectedIndexChanged();

            UpdatePreview();
            UpdateNavigationScheme();
        }

        protected override List<VenueViewType> CreateViewList()
        {
            var viewList = new List<VenueViewType>();

            foreach (var venue in _allVenues)
            {
                if (!MatchesFilter(venue))
                {
                    continue;
                }

                viewList.Add(new VenueViewType(venue, OnRowClicked));
            }

            return viewList;
        }

        private async UniTaskVoid ReloadVenuesAsync()
        {
            var cts = _lifecycleCts;
            if (cts == null)
            {
                return;
            }

            int generation = ++_loadGeneration;

            VenueBrowserSession session;
            try
            {
                session = await VenueRegistry.LoadBrowserSessionAsync();
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to load venues for the Venue Select menu.");
                session = null;
            }

            // The menu was closed, or a newer load took over while this one was in flight
            if (session == null || cts.IsCancellationRequested || generation != _loadGeneration)
            {
                session?.Dispose();
                return;
            }

            // Swapped in only now, so the visible rows never point at released metadata
            DisposeSession();
            _session = session;

            _allVenues.Clear();
            _allVenues.AddRange(session.Venues);

            RefreshVenues(false);
        }

        private void RefreshVenues(bool preserveSelection)
        {
            var previous = preserveSelection ? CurrentSelection?.Descriptor : null;

            RequestViewListUpdate();

            if (previous == null || !SetIndexTo(view => ReferenceEquals(view.Descriptor, previous)))
            {
                // Fall back to the pinned venue, then to the top of the list
                if (!SetIndexTo(view => VenueRegistry.IsPinned(view.Descriptor.Info)))
                {
                    SelectedIndex = 0;
                }
            }

            UpdateHeader();
            UpdateBanner();
            UpdateEmptyState();
            UpdatePreview();
            UpdateNavigationScheme();
        }

        private void OnRowClicked(VenueDescriptor venue)
        {
            SetIndexTo(view => ReferenceEquals(view.Descriptor, venue));
        }

        private bool MatchesFilter(VenueDescriptor venue)
        {
            return _filter switch
            {
                VenueFilter.Remote => venue.Info.Source == VenueEntrySource.Addressable,
                VenueFilter.Local => venue.Info.Source is VenueEntrySource.LocalFile or VenueEntrySource.LauncherFile,
                VenueFilter.Disabled => VenueRegistry.IsHidden(venue.Info),
                _ => true,
            };
        }

        private void UpdateHeader()
        {
            int enabledCount = 0;
            string pinnedName = null;

            var pinned = SettingsManager.Settings.PinnedVenue;
            foreach (var venue in _allVenues)
            {
                if (!VenueRegistry.IsHidden(venue.Info))
                {
                    enabledCount++;
                }

                if (pinned.Source != VenueEntrySource.None && venue.Info == pinned)
                {
                    pinnedName = venue.DisplayName;
                }
            }

            if (pinned.Source == VenueEntrySource.None)
            {
                _summaryText.text = Localize.KeyFormat("Menu.Content.VenueSelect.Summary",
                    enabledCount, _allVenues.Count);
            }
            else
            {
                _summaryText.text = Localize.KeyFormat("Menu.Content.VenueSelect.SummaryPinned",
                    enabledCount, _allVenues.Count, pinnedName ?? pinned.Identifier);
            }

            _filterText.text = Localize.Key("Menu.Content.VenueSelect.Actions.Filter") + ": "
                + Localize.Key(GetFilterKey());
        }

        private void UpdateBanner()
        {
            string bannerKey = null;

            if (SettingsManager.Settings.DisableGlobalBackgrounds.Value)
            {
                bannerKey = "Menu.Content.VenueSelect.Banners.GlobalDisabled";
            }
            else if (!VenueRegistry.CanUseRemoteVenues())
            {
                bannerKey = "Menu.Content.VenueSelect.Banners.RemoteDisabled";
            }
            else if (SettingsManager.Settings.PinnedVenue.Source == VenueEntrySource.None &&
                !HasRandomRotationCandidate())
            {
                bannerKey = "Menu.Content.VenueSelect.Banners.EmptyPool";
            }

            _bannerContainer.SetActive(bannerKey != null);
            if (bannerKey != null)
            {
                _bannerText.text = Localize.Key(bannerKey);
            }
        }

        private bool HasRandomRotationCandidate()
        {
            foreach (var venue in _allVenues)
            {
                // Hint-only venues are rows, but random rotation skips them
                if (VenueRegistry.IsEligibleForRandomPool(venue.Info, venue.ExcludeFromRandomByDefault))
                {
                    return true;
                }
            }

            // Local video and image backgrounds rotate too, but never appear as venue rows
            foreach (var candidate in VenueLoader.GetGlobalLocalCandidates())
            {
                if (candidate.Type != BackgroundType.Yarground)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateEmptyState()
        {
            _emptyStateContainer.SetActive(_allVenues.Count == 0);
        }

        private void UpdatePreview()
        {
            if (_previewPanel == null)
            {
                return;
            }

            _previewPanel.Show(CurrentSelection?.Descriptor);
        }

        private void UpdateNavigationScheme()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            PopNavigationScheme();

            var navigator = Navigator.Instance;
            if (navigator == null)
            {
                return;
            }

            var entries = new List<NavigationScheme.Entry>
            {
                new(MenuAction.Up, "Menu.Common.Up", ctx =>
                {
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex--;
                }),
                new(MenuAction.Down, "Menu.Common.Down", ctx =>
                {
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex++;
                }),
            };

            // Without a selection the venue actions have nothing to act on
            if (CurrentSelection != null)
            {
                var selectedVenue = CurrentSelection.Descriptor;
                entries.Add(new NavigationScheme.Entry(MenuAction.Green,
                    VenueViewType.GetPinActionKey(selectedVenue), TogglePinSelected));
                entries.Add(new NavigationScheme.Entry(MenuAction.Orange,
                    VenueViewType.GetAvailabilityActionKey(selectedVenue), CycleAvailabilitySelected));
            }

            entries.Add(new NavigationScheme.Entry(MenuAction.Blue,
                "Menu.Content.VenueSelect.Actions.Filter", CycleFilter));
            entries.Add(new NavigationScheme.Entry(MenuAction.Yellow,
                "Menu.Content.VenueSelect.Actions.OpenFolder", OpenVenueFolder));
            entries.Add(new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back));

            _ = navigator.PushScheme(new NavigationScheme(entries, true));
            _schemePushed = true;
        }

        private void PopNavigationScheme()
        {
            if (!_schemePushed)
            {
                return;
            }

            _schemePushed = false;
            Navigator.Instance?.PopScheme();
        }

        private void TogglePinSelected()
        {
            var venue = CurrentSelection?.Descriptor;
            if (venue == null)
            {
                return;
            }

            VenueRegistry.TogglePin(venue.Info);
            RefreshVenues(true);
        }

        private void CycleAvailabilitySelected()
        {
            var venue = CurrentSelection?.Descriptor;
            if (venue == null)
            {
                return;
            }

            VenueRegistry.CycleAvailability(venue);
            RefreshVenues(true);
        }

        private void CycleFilter()
        {
            _filter = _filter switch
            {
                VenueFilter.All      => VenueFilter.Remote,
                VenueFilter.Remote   => VenueFilter.Local,
                VenueFilter.Local    => VenueFilter.Disabled,
                VenueFilter.Disabled => VenueFilter.All,
                _                    => VenueFilter.All,
            };

            RefreshVenues(true);
        }

        private string GetFilterKey()
        {
            return _filter switch
            {
                VenueFilter.Remote   => "Menu.Content.VenueSelect.Filters.Remote",
                VenueFilter.Local    => "Menu.Content.VenueSelect.Filters.Local",
                VenueFilter.Disabled => "Menu.Content.VenueSelect.Filters.Disabled",
                _                    => "Menu.Content.VenueSelect.Filters.All",
            };
        }

        private void OpenVenueFolder()
        {
            FileExplorerHelper.OpenFolder(VenueLoader.VenueFolder);

            // Picking up newly dropped files must not require leaving and re-entering the menu
            _refreshOnFocus = true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !_refreshOnFocus || !isActiveAndEnabled)
            {
                return;
            }

            _refreshOnFocus = false;
            ReloadVenuesAsync().Forget();
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }

        private void CancelLifecycle()
        {
            if (_lifecycleCts == null)
            {
                return;
            }

            _lifecycleCts.Cancel();
            _lifecycleCts.Dispose();
            _lifecycleCts = null;
        }

        private void DisposeSession()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
