using System;
using UnityEngine;
using YARG.Core.Song;
using YARG.Menu.Dialogs.Components;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Persistent;
using YARG.Song;

namespace YARG.Menu.Dialogs
{
    public class LibrarySearchDialog : Dialog
    {
        [SerializeField]
        private SongSearchingField _searchField;
        [SerializeField]
        private DialogViewList _viewList;

        private SongCategory[] _sortedSongs;

        public Action<SongEntry> SelectAction;

        protected override void OnOpen()
        {
            _searchField.OnSearchQueryUpdated += OnSearchChanged;
        }

        public override void Submit()
        {
            SelectAction?.Invoke(_viewList.CurrentSelection.SongEntry);
            DialogManager.Instance.ClearDialog();
        }

        private void OnSearchChanged(bool force)
        {
            _sortedSongs = _searchField.Search(SortAttribute.Name);
            _viewList.UpdateCategories(_sortedSongs);
        }

        protected override void OnBeforeClose()
        {
            _searchField.OnSearchQueryUpdated -= OnSearchChanged;
            _searchField.ClearFilterQueries();
        }

        // Currently unused click handler
        private void OnSongSelected(SongEntry song)
        {
            SelectAction?.Invoke(song);
            DialogManager.Instance.ClearDialog();
        }
    }
}