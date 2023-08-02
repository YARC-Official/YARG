using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.Persistent;
using YARG.Serialization;
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

        [Space]
        [SerializeField]
        private GameObject difficultyRingPrefab;

        private readonly List<DifficultyRing> _difficultyRings = new();
        private CancellationTokenSource _cancellationToken;

        public void Init()
        {
            // Spawn 10 difficulty rings
            // for (int i = 0; i < 10; i++) {
            // 	var go = Instantiate(difficultyRingPrefab, _difficultyRingsContainer);
            // 	difficultyRings.Add(go.GetComponent<DifficultyRing>());
            // }
            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(difficultyRingPrefab, _difficultyRingsTopContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }

            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(difficultyRingPrefab, _difficultyRingsBottomContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }
        }

        public async UniTask UpdateSidebar()
        {
            // Cancel album art
            if (_cancellationToken != null)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
                _cancellationToken = null;
            }

            if (MusicLibraryMenu.Instance.ViewList.Count <= 0)
            {
                return;
            }

            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];

            if (viewType is CategoryViewType categoryViewType)
            {
                // Hide album art
                _albumCover.texture = null;
                _albumCover.color = Color.clear;
                _album.text = string.Empty;

                int sourceCount = categoryViewType.CountOf(i => i.Source);
                _source.text = $"{sourceCount} sources";

                int charterCount = categoryViewType.CountOf(i => i.Charter);
                _charter.text = $"{charterCount} charters";

                int genreCount = categoryViewType.CountOf(i => i.Genre);
                _genre.text = $"{genreCount} genres";

                _year.text = string.Empty;
                _length.text = string.Empty;
                HelpBar.Instance.SetInfoText(string.Empty);

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

            var songEntry = songViewType.SongEntry;

            _album.text = songEntry.Album;
            _source.text = SongSources.SourceToGameName(songEntry.Source);
            _charter.text = songEntry.Charter;
            _genre.text = songEntry.Genre;
            _year.text = songEntry.Year;
            HelpBar.Instance.SetInfoText(
                RichTextUtils.StripRichTextTagsExclude(songEntry.LoadingPhrase, RichTextUtils.GOOD_TAGS));

            // Format and show length
            if (songEntry.SongLengthTimeSpan.Hours > 0)
            {
                _length.text = songEntry.SongLengthTimeSpan.ToString(@"h\:mm\:ss");
            }
            else
            {
                _length.text = songEntry.SongLengthTimeSpan.ToString(@"m\:ss");
            }

            UpdateDifficulties(songEntry);

            // Finally, update album cover
            await LoadAlbumCover();
        }

        private void UpdateDifficulties(SongEntry songEntry)
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

            _difficultyRings[0].SetInfo(songEntry, Instrument.FiveFretGuitar);
            _difficultyRings[1].SetInfo(songEntry, Instrument.FiveFretBass);

            // 5-lane or 4-lane
            if (songEntry.DrumType == DrumType.FiveLane)
            {
                _difficultyRings[2].SetInfo(songEntry, Instrument.FiveLaneDrums);
            }
            else
            {
                _difficultyRings[2].SetInfo(songEntry, Instrument.FourLaneDrums);
            }

            _difficultyRings[3].SetInfo(songEntry, Instrument.Keys);

            // Mic (with mic count)
            if (songEntry.VocalParts == 0)
            {
                _difficultyRings[4].SetInfo(false, "vocals", -1);
            }
            else
            {
                _difficultyRings[4].SetInfo(
                    true,
                    songEntry.VocalParts switch
                    {
                        2    => "twoVocals",
                        >= 3 => "harmVocals",
                        _    => "vocals"
                    },
                    songEntry.PartDifficulties.GetValueOrDefault(Instrument.Vocals, -1)
                );
            }

            // Protar or Co-op
            int realGuitarDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.ProGuitar_17Fret, -1);
            if (songEntry.DrumType == DrumType.FourLane && realGuitarDiff == -1)
            {
                _difficultyRings[5].SetInfo(songEntry, Instrument.FiveFretCoopGuitar);
            }
            else
            {
                _difficultyRings[5].SetInfo(songEntry, Instrument.ProGuitar_17Fret);
            }

            // Pro bass or Rhythm
            int realBassDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.ProBass_17Fret, -1);
            if (songEntry.DrumType == DrumType.FiveLane && realBassDiff == -1)
            {
                _difficultyRings[6].SetInfo(songEntry, Instrument.FiveFretRhythm);
            }
            else
            {
                _difficultyRings[6].SetInfo(songEntry, Instrument.ProBass_17Fret);
            }

            _difficultyRings[7].SetInfo(false, "trueDrums", -1);
            _difficultyRings[8].SetInfo(songEntry, Instrument.ProKeys);

            // Band difficulty
            if (songEntry.BandDifficulty == -1)
            {
                _difficultyRings[9].SetInfo(false, "band", -1);
            }
            else
            {
                _difficultyRings[9].SetInfo(true, "band", songEntry.BandDifficulty);
            }
        }

        public async UniTask LoadAlbumCover()
        {
            // Dispose of the old texture (prevent memory leaks)
            if (_albumCover.texture != null)
            {
                // This might seem weird, but we are destroying the *texture*, not the UI image.
                Destroy(_albumCover.texture);
            }

            // Hide album art until loaded
            _albumCover.texture = null;
            _albumCover.color = Color.clear;

            _cancellationToken = new();

            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            if (songEntry is IniSongEntry)
            {
                string[] possiblePaths =
                {
                    "album.png", "album.jpg", "album.jpeg",
                };

                // Load album art from one of the paths
                foreach (string path in possiblePaths)
                {
                    string fullPath = Path.Combine(songEntry.Location, path);
                    if (File.Exists(fullPath))
                    {
                        await LoadSongIniCover(fullPath);
                        break;
                    }
                }
            }
            else
            {
                await LoadRbConCover(songEntry as ExtractedConSongEntry);
            }
        }

        private async UniTask LoadSongIniCover(string filePath)
        {
            var texture = await TextureLoader.Load(filePath, _cancellationToken.Token);

            if (texture != null)
            {
                // Set album cover
                _albumCover.texture = texture;
                _albumCover.color = Color.white;
                _albumCover.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private async UniTask LoadRbConCover(ExtractedConSongEntry conSongEntry)
        {
            Texture2D texture = null;
            try
            {
                byte[] bytes = conSongEntry.LoadImgFile();
                if (bytes.Length == 0) return;

                texture = await XboxImageTextureGenerator.GetTexture(bytes, _cancellationToken.Token);

                _albumCover.texture = texture;
                _albumCover.color = Color.white;
                _albumCover.uvRect = new Rect(0f, 0f, 1f, -1f);
            }
            catch (OperationCanceledException)
            {
                // Dispose of the texture (prevent memory leaks)
                if (texture != null)
                {
                    // This might seem weird, but we are destroying the *texture*, not the UI image.
                    Destroy(texture);
                }
            }
        }

        public void PrimaryButtonClick()
        {
            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            viewType.PrimaryButtonClick();
        }

        public void SearchFilter(string type)
        {
            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            string value = type switch
            {
                "source"  => songEntry.Source,
                "album"   => songEntry.Album,
                "year"    => songEntry.Year,
                "charter" => songEntry.Charter,
                "genre"   => songEntry.Genre,
                _         => throw new Exception("Unreachable")
            };
            MusicLibraryMenu.Instance.SetSearchInput($"{type}:{value}");
        }
    }
}