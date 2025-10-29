using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers.Extensions;
using YARG.Menu.Persistent;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class Sidebar : MonoBehaviour
    {
        [SerializeField]
        private Transform _difficultyRingsTopContainer;
        [SerializeField]
        private Transform _difficultyRingsBottomContainer;

        [SerializeField]
        private TextMeshProUGUI _album;
        [SerializeField]
        private TextMeshProUGUI _source;
        [SerializeField]
        private TextMeshProUGUI _charter;
        [SerializeField]
        private TextMeshProUGUI _genre;
        [SerializeField]
        private TextMeshProUGUI _genreDivider;
        [SerializeField]
        private TextMeshProUGUI _subgenre;
        [SerializeField]
        private TextMeshProUGUI _year;
        [SerializeField]
        private TextMeshProUGUI _length;
        [SerializeField]
        private RawImage _albumCover;

        [FormerlySerializedAs("difficultyRingPrefab")]
        [Space]
        [SerializeField]
        private GameObject _difficultyRingPrefab;

        private readonly List<DifficultyRing> _difficultyRings = new();
        private CancellationTokenSource _cancellationToken;
        private ViewType _currentView;

        private MusicLibraryMenu _musicLibraryMenu;
        private SongSearchingField _songSearchingField;

        private const int FULL_GENRE_FIELD_WIDTH = 325;
        private const int GENRE_DIVIDER_WIDTH = 10;

        public void Initialize(MusicLibraryMenu musicLibraryMenu, SongSearchingField songSearchingField)
        {
            _musicLibraryMenu = musicLibraryMenu;
            _songSearchingField = songSearchingField;

            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(_difficultyRingPrefab, _difficultyRingsTopContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }

            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(_difficultyRingPrefab, _difficultyRingsBottomContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }
        }

        public void UpdateSidebar()
        {
            if (_musicLibraryMenu.ViewList.Count <= 0)
            {
                return;
            }

            var viewType = _musicLibraryMenu.CurrentSelection;
            if (_currentView != null && _currentView == viewType)
                return;

            _currentView = viewType;

            // Cancel album art
            if (_cancellationToken != null)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
                _cancellationToken = null;
            }

            switch (viewType)
            {
                case SongViewType songViewType:
                    ShowSongInfo(songViewType);
                    break;
                case CategoryViewType categoryViewType:
                    ClearSidebar();
                    ShowCategoryInfo(categoryViewType);
                    break;
                default:
                    ClearSidebar();
                    break;
            }
        }

        private void ShowCategoryInfo(CategoryViewType categoryViewType)
        {
            _source.text = categoryViewType.SourceCountText;
            _charter.text = categoryViewType.CharterCountText;
            UpdateGenreAndSubgenre(categoryViewType.GenreCountText, categoryViewType.SubgenreCountText);
        }

        private void ClearSidebar()
        {
            // Hide album art
            _albumCover.texture = null;
            _albumCover.color = Color.clear;
            _album.text = string.Empty;

            _year.text = string.Empty;
            _length.text = string.Empty;

            _source.text = string.Empty;
            _charter.text = string.Empty;
            UpdateGenreAndSubgenre(string.Empty, string.Empty);
            _genreDivider.gameObject.SetActive(false);

            // Hide all difficulty rings
            foreach (var difficultyRing in _difficultyRings)
            {
                difficultyRing.gameObject.SetActive(false);
            }
        }

        private void ShowSongInfo(SongViewType songViewType)
        {
            var songEntry = songViewType.SongEntry;

            _album.text = songEntry.Album;
            _source.text = SongSources.SourceToGameName(songEntry.Source);
            _charter.text = songEntry.Charter;
            UpdateGenreAndSubgenre(songEntry.Genre, songEntry.Subgenre);
            _year.text = songEntry.ParsedYear;

            // Format and show length
            var time = TimeSpan.FromMilliseconds(songEntry.SongLengthMilliseconds);
            if (time.Hours > 0)
            {
                _length.text = time.ToString(@"h\:mm\:ss");
            }
            else
            {
                _length.text = time.ToString(@"m\:ss");
            }

            UpdateDifficulties(songEntry);

            _cancellationToken = new();
            _albumCover.LoadAlbumCover(songEntry, _cancellationToken.Token);
        }

        private void UpdateDifficulties(SongEntry entry)
        {
            // Show all difficulty rings
            foreach (var difficultyRing in _difficultyRings)
            {
                difficultyRing.gameObject.SetActive(true);
            }

            /*

                Guitar               ; Bass               ; 4 or 5 lane ; Keys     ; Mic (dependent on mic count)
                Pro Guitar or Co-op  ; Pro Bass or Rhythm ; True Drums  ; Pro Keys ; Band

            */


            _difficultyRings[0].SetInfo("guitar", Instrument.FiveFretGuitar, entry[Instrument.FiveFretGuitar]);
            _difficultyRings[1].SetInfo("bass", Instrument.FiveFretBass, entry[Instrument.FiveFretBass]);

            // 5-lane or 4-lane
            if (entry.HasInstrument(Instrument.FiveLaneDrums))
            {
                _difficultyRings[2].SetInfo("ghDrums", Instrument.FiveLaneDrums, entry[Instrument.FiveLaneDrums]);
            }
            else
            {
                _difficultyRings[2].SetInfo("drums", Instrument.FourLaneDrums, entry[Instrument.FourLaneDrums]);
            }

            _difficultyRings[3].SetInfo("keys", Instrument.Keys, entry[Instrument.Keys]);

            if (entry.HasInstrument(Instrument.Harmony))
            {
                _difficultyRings[4].SetInfo(
                    entry.VocalsCount switch
                    {
                        2 => "twoVocals",
                        >= 3 => "harmVocals",
                        _ => "vocals"
                    },
                    Instrument.Harmony,
                    entry[Instrument.Harmony]
                );
            }
            else
            {
                _difficultyRings[4].SetInfo("vocals", Instrument.Vocals, entry[Instrument.Vocals]);
            }

            // Protar or Co-op
            if (entry.HasInstrument(Instrument.ProGuitar_17Fret) || entry.HasInstrument(Instrument.ProGuitar_22Fret))
            {
                var values = entry[Instrument.ProGuitar_17Fret];
                var instrument = Instrument.ProGuitar_17Fret;
                if (values.Intensity == -1 && entry.HasInstrument(Instrument.ProGuitar_22Fret))
                {
                    values = entry[Instrument.ProGuitar_22Fret];
                    instrument = Instrument.ProGuitar_22Fret;
                }
                _difficultyRings[5].SetInfo("realGuitar", instrument, values);
            }
            else
            {
                _difficultyRings[5].SetInfo("guitarCoop", Instrument.FiveFretCoopGuitar, entry[Instrument.FiveFretCoopGuitar]);
            }

            // ProBass or Rhythm
            if (entry.HasInstrument(Instrument.ProBass_17Fret) || entry.HasInstrument(Instrument.ProBass_22Fret))
            {
                var values = entry[Instrument.ProBass_17Fret];
                var instrument = Instrument.ProBass_17Fret;
                if (values.Intensity == -1 && entry.HasInstrument(Instrument.ProBass_22Fret))
                {
                    values = entry[Instrument.ProBass_22Fret];
                    instrument = Instrument.ProBass_22Fret;
                }
                _difficultyRings[6].SetInfo("realBass", instrument, values);
            }
            else
            {
                _difficultyRings[6].SetInfo("rhythm", Instrument.FiveFretRhythm, entry[Instrument.FiveFretRhythm]);
            }

            _difficultyRings[7].SetInfo("eliteDrums", Instrument.EliteDrums, entry[Instrument.EliteDrums]);
            _difficultyRings[8].SetInfo("realKeys", Instrument.ProKeys, entry[Instrument.ProKeys]);
            _difficultyRings[9].SetInfo("band", Instrument.Band, entry[Instrument.Band]);
        }

        private void UpdateGenreAndSubgenre(string genreText, string subgenreText)
        {
            _genre.text = genreText;
            _genre.rectTransform.sizeDelta = new(
                _genre.GetPreferredValues().x,
                _genre.rectTransform.sizeDelta.y
            );

            if (string.IsNullOrEmpty(subgenreText))
            {
                _genreDivider.gameObject.SetActive(false);
                _subgenre.gameObject.SetActive(false);
            }
            else
            {
                _genreDivider.gameObject.SetActive(true);
                _subgenre.gameObject.SetActive(true);
                _subgenre.text = subgenreText;
                _subgenre.rectTransform.sizeDelta = new(
                    Math.Min(FULL_GENRE_FIELD_WIDTH - _genre.rectTransform.sizeDelta.x - GENRE_DIVIDER_WIDTH, _subgenre.GetPreferredValues().x),
                    _subgenre.rectTransform.sizeDelta.y
                );
            }
        }

        public void PrimaryButtonClick()
        {
            _musicLibraryMenu.CurrentSelection.PrimaryButtonClick();
        }

        public void SearchFilter(string type)
        {
            var viewType = _musicLibraryMenu.CurrentSelection;

            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            switch (type)
            {
                case "source":
                    _songSearchingField.SetSearchInput(SortAttribute.Source, $"\"{songEntry.Source.SearchStr}\"");
                    break;
                case "album":
                    _songSearchingField.SetSearchInput(SortAttribute.Album, $"\"{songEntry.Album.SearchStr}\"");
                    break;
                case "year":
                    _songSearchingField.SetSearchInput(SortAttribute.Year, $"\"{songEntry.ParsedYear}\"");
                    break;
                case "charter":
                    _songSearchingField.SetSearchInput(SortAttribute.Charter, $"\"{songEntry.Charter.SearchStr}\"");
                    break;
                case "genre":
                    _songSearchingField.SetSearchInput(SortAttribute.Genre, $"\"{songEntry.Genre.SearchStr}\"");
                    break;
                case "subgenre":
                    _songSearchingField.SetSearchInput(SortAttribute.Subgenre, $"\"{songEntry.Subgenre.SearchStr}\"");
                    break;
            }
        }
    }
}