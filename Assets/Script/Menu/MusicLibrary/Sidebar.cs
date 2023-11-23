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

            if (viewType is CategoryViewType categoryViewType)
            {
                // Hide album art
                _albumCover.texture = null;
                _albumCover.color = Color.clear;
                _album.text = string.Empty;

                _source.text = categoryViewType.SourceCountText;
                _charter.text = categoryViewType.CharterCountText;
                _genre.text = categoryViewType.GenreCountText;

                _year.text = string.Empty;
                _length.text = string.Empty;

                // Hide all difficulty rings
                foreach (var difficultyRing in _difficultyRings)
                {
                    difficultyRing.gameObject.SetActive(false);
                }

                return;
            }

            if (viewType is not SongViewType songViewType)
            {
                return;
            }

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


            _difficultyRings[0].SetInfo("guitar", "FiveFretGuitar", parts.GetValues(Instrument.FiveFretGuitar));
            _difficultyRings[1].SetInfo("bass", "FiveFretBass", parts.GetValues(Instrument.FiveFretBass));

            // 5-lane or 4-lane
            if (parts.GetDrumType() == DrumsType.FiveLane)
            {
                _difficultyRings[2].SetInfo("ghDrums", "FiveLaneDrums", parts.GetValues(Instrument.FiveLaneDrums));
            }
            else
            {
                _difficultyRings[2].SetInfo("drums", "FourLaneDrums", parts.GetValues(Instrument.FourLaneDrums));
            }

            _difficultyRings[3].SetInfo("keys", "Keys", parts.GetValues(Instrument.Keys));

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
                    parts.GetValues(Instrument.Harmony)
                );
            }
            else
            {
                _difficultyRings[4].SetInfo("vocals", "Vocals", parts.GetValues(Instrument.Vocals));
            }

            // Protar or Co-op
            if (parts.HasInstrument(Instrument.ProGuitar_17Fret) || parts.HasInstrument(Instrument.ProGuitar_22Fret))
            {
                var values = parts.GetValues(Instrument.ProGuitar_17Fret);
                if (values.intensity == -1)
                    values = parts.GetValues(Instrument.ProGuitar_22Fret);
                _difficultyRings[5].SetInfo("realGuitar", "ProGuitar", values);
            }
            else
            {
                _difficultyRings[5].SetInfo("guitarCoop", "FiveFretCoopGuitar", parts.GetValues(Instrument.FiveFretCoopGuitar));
            }

            // ProBass or Rhythm
            if (parts.HasInstrument(Instrument.ProBass_17Fret) || parts.HasInstrument(Instrument.ProBass_22Fret))
            {
                var values = parts.GetValues(Instrument.ProBass_17Fret);
                if (values.intensity == -1)
                    values = parts.GetValues(Instrument.ProBass_22Fret);
                _difficultyRings[6].SetInfo("realBass", "ProBass", values);
            }
            else
            {
                _difficultyRings[6].SetInfo("rhythm", "FiveFretRhythm", parts.GetValues(Instrument.FiveFretRhythm));
            }

            _difficultyRings[7].SetInfo("trueDrums", "TrueDrums", new PartValues(-1));
            _difficultyRings[8].SetInfo("realKeys", "ProKeys", parts.GetValues(Instrument.ProKeys));
            _difficultyRings[9].SetInfo("band", "Band", parts.GetValues(Instrument.Band));
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
                "source"  => songEntry.Source,
                "album"   => songEntry.Album,
                "year"    => songEntry.Year,
                "charter" => songEntry.Charter,
                "genre"   => songEntry.Genre,
                _         => throw new Exception("Unreachable")
            };

            _musicLibraryMenu.SetSearchInput($"{type}:{value}");
        }
    }
}