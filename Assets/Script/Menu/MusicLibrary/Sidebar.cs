using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
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

        public void Initialize(MusicLibraryMenu musicLibraryMenu)
        {
            _musicLibraryMenu = musicLibraryMenu;

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
            _genre.text = categoryViewType.GenreCountText;
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
            _genre.text = string.Empty;

            // Hide all difficulty rings
            foreach (var difficultyRing in _difficultyRings)
            {
                difficultyRing.gameObject.SetActive(false);
            }
        }

        private void ShowSongInfo(SongViewType songViewType)
        {
            var songEntry = songViewType.SongMetadata;

            _album.text = songEntry.Album;
            _source.text = SongSources.SourceToGameName(songEntry.Source);
            _charter.text = songEntry.Charter;
            _genre.text = songEntry.Genre;
            _year.text = songEntry.Year;

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

            UpdateDifficulties(songEntry.Parts);

            _cancellationToken = new();
            // Finally, update album cover
            LoadAlbumCover();
        }

        private void UpdateDifficulties(AvailableParts parts)
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


            _difficultyRings[0].SetInfo("guitar", "FiveFretGuitar", parts[Instrument.FiveFretGuitar]);
            _difficultyRings[1].SetInfo("bass", "FiveFretBass", parts[Instrument.FiveFretBass]);

            // 5-lane or 4-lane
            if (parts.GetDrumType() == DrumsType.FiveLane)
            {
                _difficultyRings[2].SetInfo("ghDrums", "FiveLaneDrums", parts[Instrument.FiveLaneDrums]);
            }
            else
            {
                _difficultyRings[2].SetInfo("drums", "FourLaneDrums", parts[Instrument.FourLaneDrums]);
            }

            _difficultyRings[3].SetInfo("keys", "Keys", parts[Instrument.Keys]);

            if (parts.HasInstrument(Instrument.Harmony))
            {
                _difficultyRings[4].SetInfo(
                    parts.VocalsCount switch
                    {
                        2 => "twoVocals",
                        >= 3 => "harmVocals",
                        _ => "vocals"
                    },
                    "Harmony",
                    parts[Instrument.Harmony]
                );
            }
            else
            {
                _difficultyRings[4].SetInfo("vocals", "Vocals", parts[Instrument.Vocals]);
            }

            // Protar or Co-op
            if (parts.HasInstrument(Instrument.ProGuitar_17Fret) || parts.HasInstrument(Instrument.ProGuitar_22Fret))
            {
                var values = parts[Instrument.ProGuitar_17Fret];
                if (values.Intensity == -1)
                    values = parts[Instrument.ProGuitar_22Fret];
                _difficultyRings[5].SetInfo("realGuitar", "ProGuitar", values);
            }
            else
            {
                _difficultyRings[5].SetInfo("guitarCoop", "FiveFretCoopGuitar", parts[Instrument.FiveFretCoopGuitar]);
            }

            // ProBass or Rhythm
            if (parts.HasInstrument(Instrument.ProBass_17Fret) || parts.HasInstrument(Instrument.ProBass_22Fret))
            {
                var values = parts[Instrument.ProBass_17Fret];
                if (values.Intensity == -1)
                    values = parts[Instrument.ProBass_22Fret];
                _difficultyRings[6].SetInfo("realBass", "ProBass", values);
            }
            else
            {
                _difficultyRings[6].SetInfo("rhythm", "FiveFretRhythm", parts[Instrument.FiveFretRhythm]);
            }

            _difficultyRings[7].SetInfo("trueDrums", "TrueDrums", new PartValues(-1));
            _difficultyRings[8].SetInfo("realKeys", "ProKeys", parts[Instrument.ProKeys]);
            _difficultyRings[9].SetInfo("band", "Band", parts[Instrument.Band]);
        }

        public async void LoadAlbumCover()
        {
            var viewType = _musicLibraryMenu.CurrentSelection;

            if (viewType is not SongViewType songViewType) return;

            var originalTexture = _albumCover.texture;

            // Load the new one
            await songViewType.SongMetadata.SetRawImageToAlbumCover(_albumCover, _cancellationToken.Token);

            // Dispose of the old texture (prevent memory leaks)
            if (originalTexture != null)
            {
                Destroy(originalTexture);
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

            var songEntry = songViewType.SongMetadata;

            string value = type switch
            {
                "source"  => songEntry.Source.SortStr,
                "album"   => songEntry.Album.SortStr,
                "year"    => songEntry.Year,
                "charter" => songEntry.Charter.SortStr,
                "genre"   => songEntry.Genre.SortStr,
                _         => throw new Exception("Unreachable")
            };

            _musicLibraryMenu.SetSearchInput($"{type}:{value}");
        }
    }
}