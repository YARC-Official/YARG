using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Career;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class CareerLibrary : MusicLibraryMenu
    {
        protected override bool SearchAllowed => false;

        private SongCategory[] _careerSongs;

        protected override void Awake()
        {
            base.Awake();

            // Ensure no categories are collapsed
            _collapsedHeaders.Clear();
        }


        // TODO: Rework MusicLibraryMenu's OnEnable so we don't have to recapitulate so much stuff here
        protected override void OnEnable()
        {
            SetSidebarDifficultiesVisible(true);
            _heldInputs.Clear();


        }

        // We don't want filters in this version
        protected override void InitializeFilters()
        {

        }

        public void InitializeCareer(CareerBase career)
        {
            
        }

        public override void Refresh()
        {
            if (IsNavigationSchemeBlocked())
            {
                _needsNavigationSchemeRefresh = true;
                return;
            }

            SetNavigationScheme();
        }

        public override void SetNavigationScheme(bool reset = false)
        {
            if (reset)
            {
                Navigator.Instance.PopScheme();
            }

            // TODO: This actually needs to change based on whether the tier is a forced setlist or individual songs
            _sidebar.UpdatePlayButtonLabel(false);

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new List<NavigationScheme.Entry>
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Confirm, hide: true),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back, hide: true)
            }, false));
        }

        private void Confirm()
        {
            CurrentSelection?.PrimaryButtonClick();
        }

        protected override List<ViewType> CreateViewList()
        {
            var list = new List<ViewType>();

            // If `_careerSongs` is null, then this function is being called during very first initialization,
            // which means the song list hasn't been constructed yet.
            if (_careerSongs is null || SongContainer.Count <= 0)
            {
                return list;
            }

            if (!_sortedSongs.Any(section => section.Songs.Length > 0))
            {
                list.Add(new SortHeaderViewType(Localize.Key("Menu.MusicLibrary.NoSongsMatchCriteria"), 0, null, Array.Empty<SongEntry>()));
                return list;
            }

            foreach (var section in _careerSongs)
            {
                var displayName = section.Category;
                list.Add(new SortHeaderViewType(displayName, section.Songs.Length, null, section.Songs));

                foreach (var song in section.Songs)
                {
                    list.Add(new SongViewType(this, song));
                }
            }

            return list;
        }
    }
}