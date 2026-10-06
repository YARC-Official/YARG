using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Career;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Types;
using YARG.Song;
using Object = UnityEngine.Object;

namespace YARG.Settings.Metadata
{
    public class CareerPresetSubTab : PresetSubTab<CareerPreset>
    {
        private GameObject _careerInfoPrefab;
        private GameObject _careerTierPrefab;
        private GameObject _careerSongPrefab;

        public CareerPresetSubTab(CustomContent<CareerPreset> customContent, IPreviewBuilder previewBuilder = null,
            bool hasDescriptions = true) : base(customContent, previewBuilder, hasDescriptions)
        {
        }

        public override void BuildSettingTab(Transform container, NavigationGroup navGroup)
        {
            if (_presetRef == null)
            {
                return;
            }

            _fieldIndex = 0;

            // Header: Career Information
            _ = SpawnHeader(container, "CareerInformation");
            _fieldIndex++;

            // Editable CareerPreset metadata fields
            CreateField(container, navGroup, nameof(CareerPreset), nameof(CareerPreset.Description),
                new StringSetting(_presetRef.Description ?? string.Empty, val =>
                {
                    _presetRef.Description = val;
                    Save();
                }));

            CreateField(container, navGroup, nameof(CareerPreset), nameof(CareerPreset.BackgroundImageName),
                new FileInfoSetting(_presetRef.BackgroundImageName, _presetRef,
                    nameof(CareerPreset.BackgroundImageName), val =>
                    {
                        _presetRef.BackgroundImageName = val;
                        Save();
                    }));

            CreateField(container, navGroup, nameof(CareerPreset), nameof(CareerPreset.Source),
                new StringSetting(_presetRef.Source ?? string.Empty, val =>
                {
                    _presetRef.Source = val;
                    Save();
                }));

            // Header: Career Tiers
            var tiersHeader = SpawnCareerInfo(container);
            Action onAddTier = ReadOnlyFields
                ? null
                : () =>
                {
                    DialogManager.Instance.ShowRenameDialog("New Tier Name", newTierName =>
                    {
                        if (string.IsNullOrWhiteSpace(newTierName)) return;
                        var newTier = new CareerTier
                        {
                            Id = Guid.NewGuid(),
                            Name = newTierName.Trim(),
                            Description = string.Empty,
                            UnlockType = UnlockType.StarCount,
                            UnlockCriteria = 0,
                            VenueSize = VenueSize.NotSpecified,
                            CompletionMode = CareerCompletionMode.SingleSong,
                            Songs = Array.Empty<CareerSong>(),
                        };
                        _presetRef.AddTier(newTier);
                        SaveAndRefresh();
                    });
                };
            tiersHeader.Initialize(Localize.Key("Settings.Header.CareerTiers"), onAddTier, null, null, null, null);
            _fieldIndex++;

            var tiers = _presetRef.Tiers;
            if (tiers == null || tiers.Count == 0)
            {
                var emptyTierGo = SpawnSubHeader(container, Localize.Key("Settings.Header.NoTiers"));
                _fieldIndex++;
                return;
            }

            for (int t = 0; t < tiers.Count; t++)
            {
                int tierIndex = t;
                var tier = tiers[tierIndex];

                string tierTitle = $"Tier {tierIndex + 1}: {tier.Name}";
                string tierDetails =
                    $" [{(tier.IsBonus ? "Bonus | " : "")}{tier.UnlockCriteria} {tier.UnlockType} | {tier.VenueSize} | {tier.CompletionMode}]";

                var tierHeader = SpawnCareerTier(container);
                Action onAddSong = ReadOnlyFields ? null : () => ShowSongPickerDialog(tier);
                Action onEditTier = ReadOnlyFields ? null : () => ShowEditTierDialog(tier);
                Action onUpTier = (ReadOnlyFields || tierIndex == 0)
                    ? null
                    : () =>
                    {
                        _presetRef.MoveTier(tierIndex, tierIndex - 1);
                        SaveAndRefresh();
                    };
                Action onDownTier = (ReadOnlyFields || tierIndex >= tiers.Count - 1)
                    ? null
                    : () =>
                    {
                        _presetRef.MoveTier(tierIndex, tierIndex + 1);
                        SaveAndRefresh();
                    };
                Action onRemoveTier = ReadOnlyFields
                    ? null
                    : () =>
                    {
                        ShowCompactConfirmation("Delete Tier", $"Are you sure you want to delete '{tier.Name}'?",
                            "Menu.Common.Delete",
                            MenuData.Colors.CancelButton, () =>
                            {
                                _presetRef.RemoveTierAt(tierIndex);
                                SaveAndRefresh();
                            });
                    };

                tierHeader.Initialize(tierTitle + tierDetails, onAddSong, onEditTier, onUpTier, onDownTier,
                    onRemoveTier);
                _fieldIndex++;

                // Render songs in this tier
                var songs = tier.Songs;
                if (songs == null || songs.Length == 0)
                {
                    var emptySongGo = SpawnSubHeader(container, "      (No songs in this tier)");
                    DimHeaderBackground(emptySongGo, 0.2f);
                    _fieldIndex++;
                }
                else
                {
                    for (int s = 0; s < songs.Length; s++)
                    {
                        int songIndex = s;
                        var song = songs[songIndex];

                        if (song.SongEntry == null)
                        {
                            song.RefreshSongEntry();
                        }

                        string songName = song.SongEntry != null
                            ? song.SongEntry.Name.Original
                            : (!string.IsNullOrEmpty(song.Description)
                                ? song.Description
                                : (song.ShortName ??
                                    song.SongTuple?.Title ?? song.SongId?.ToString() ?? "Unknown Song"));
                        string artistName = song.SongEntry != null
                            ? song.SongEntry.Artist.Original
                            : (song.SongTuple?.Artist ?? "Unknown Artist");
                        string songLabel = $"      {songIndex + 1}. {artistName} - {songName} [{song.Identifier}]";

                        var songHeader = SpawnCareerSong(container);
                        Action onEditSong = ReadOnlyFields ? null : () => ShowEditSongDialog(tier, song, songIndex);
                        Action onUpSong = (ReadOnlyFields || songIndex == 0)
                            ? null
                            : () =>
                            {
                                tier.MoveSong(songIndex, songIndex - 1);
                                SaveAndRefresh();
                            };
                        Action onDownSong = (ReadOnlyFields || songIndex >= songs.Length - 1)
                            ? null
                            : () =>
                            {
                                tier.MoveSong(songIndex, songIndex + 1);
                                SaveAndRefresh();
                            };
                        Action onRemoveSong = ReadOnlyFields
                            ? null
                            : () =>
                            {
                                tier.RemoveSongAt(songIndex);
                                SaveAndRefresh();
                            };

                        songHeader.Initialize(songLabel, null, onEditSong, onUpSong, onDownSong, onRemoveSong);
                        _fieldIndex++;
                    }
                }
            }
        }

        private void Save()
        {
            if (_presetRef != null && !_presetRef.DefaultPreset)
            {
                var path = CustomContentManager.Careers.SavePresetFile(_presetRef);
                // Prevent unwanted refresh by file watcher
                PresetsTab.IgnorePathUpdate(path);
                SettingsMenu.Instance?.OnSettingChanged();
            }
        }

        private void SaveAndRefresh()
        {
            Save();
            SettingsMenu.Instance?.RefreshSettingsKeepPosition();
        }

        private void ShowEditTierDialog(CareerTier tier)
        {
            var dialog = DialogManager.Instance.ShowList($"Edit Tier: {tier.Name}");

            dialog.AddListButton($"Name: {tier.Name}", () =>
            {
                DialogManager.Instance.ClearDialog();
                DialogManager.Instance.ShowRenameDialog(tier.Name, newName =>
                {
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    tier.Name = newName.Trim();
                    SaveAndRefresh();
                });
            }, closeOnClick: false);

            dialog.AddListButton(
                $"Description: {(string.IsNullOrEmpty(tier.Description) ? "(None)" : tier.Description)}", () =>
                {
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowRenameDialog(tier.Description ?? string.Empty, newDesc =>
                    {
                        tier.Description = newDesc?.Trim();
                        SaveAndRefresh();
                    });
                }, closeOnClick: false);

            dialog.AddListButton($"Unlock Type: {tier.UnlockType}", () =>
            {
                DialogManager.Instance.ClearDialog();
                var unlockDialog = DialogManager.Instance.ShowList("Select Unlock Type");
                unlockDialog.AddListButton("Star Count", () =>
                {
                    tier.UnlockType = UnlockType.StarCount;
                    SaveAndRefresh();
                });
                unlockDialog.AddListButton("Completion Count", () =>
                {
                    tier.UnlockType = UnlockType.CompletionCount;
                    SaveAndRefresh();
                });
            }, closeOnClick: false);

            dialog.AddListButton($"Unlock Criteria: {tier.UnlockCriteria}", () =>
            {
                DialogManager.Instance.ClearDialog();
                DialogManager.Instance.ShowRenameDialog(tier.UnlockCriteria.ToString(), newCriteriaStr =>
                {
                    if (int.TryParse(newCriteriaStr, out int criteriaVal))
                    {
                        tier.UnlockCriteria = Mathf.Max(0, criteriaVal);
                        SaveAndRefresh();
                    }
                });
            }, closeOnClick: false);

            dialog.AddListButton($"Is Bonus Tier: {(tier.IsBonus ? "Yes" : "No")}", () =>
            {
                tier.IsBonus = !tier.IsBonus;
                SaveAndRefresh();
            });

            dialog.AddListButton($"Venue Size: {tier.VenueSize}", () =>
            {
                DialogManager.Instance.ClearDialog();
                var venueDialog = DialogManager.Instance.ShowList("Select Venue Size");
                foreach (VenueSize size in Enum.GetValues(typeof(VenueSize)))
                {
                    var capturedSize = size;
                    venueDialog.AddListButton(capturedSize.ToString(), () =>
                    {
                        tier.VenueSize = capturedSize;
                        SaveAndRefresh();
                    });
                }
            }, closeOnClick: false);

            dialog.AddListButton($"Venue Hint: {(string.IsNullOrEmpty(tier.VenueHint) ? "(None)" : tier.VenueHint)}",
                () =>
                {
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowRenameDialog(tier.VenueHint ?? string.Empty, newHint =>
                    {
                        tier.VenueHint = newHint?.Trim();
                        SaveAndRefresh();
                    });
                }, closeOnClick: false);

            dialog.AddListButton($"Completion Mode: {tier.CompletionMode}", () =>
            {
                DialogManager.Instance.ClearDialog();
                var modeDialog = DialogManager.Instance.ShowList("Select Completion Mode");
                foreach (CareerCompletionMode mode in Enum.GetValues(typeof(CareerCompletionMode)))
                {
                    var capturedMode = mode;
                    modeDialog.AddListButton(capturedMode.ToString(), () =>
                    {
                        tier.CompletionMode = capturedMode;
                        SaveAndRefresh();
                    });
                }
            }, closeOnClick: false);

            dialog.AddListButton(
                $"Custom Unlock Text: {(string.IsNullOrEmpty(tier.CustomUnlockText) ? "(None)" : tier.CustomUnlockText)}",
                () =>
                {
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowRenameDialog(tier.CustomUnlockText ?? string.Empty, newText =>
                    {
                        tier.CustomUnlockText = newText?.Trim();
                        SaveAndRefresh();
                    });
                }, closeOnClick: false);

            dialog.AddListButton($"Completion Bonus: {tier.CompletionBonus}", () =>
            {
                DialogManager.Instance.ClearDialog();
                var bonusDialog = DialogManager.Instance.ShowList("Select Completion Bonus");
                foreach (CompletionBonusType bonus in Enum.GetValues(typeof(CompletionBonusType)))
                {
                    var capturedBonus = bonus;
                    bonusDialog.AddListButton(capturedBonus.ToString(), () =>
                    {
                        tier.CompletionBonus = capturedBonus;
                        SaveAndRefresh();
                    });
                }
            }, closeOnClick: false);

            dialog.AddListButton(
                $"Completion Bonus Text: {(string.IsNullOrEmpty(tier.CompletionBonusText) ? "(None)" : tier.CompletionBonusText)}",
                () =>
                {
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowRenameDialog(tier.CompletionBonusText ?? string.Empty, newText =>
                    {
                        tier.CompletionBonusText = newText?.Trim();
                        SaveAndRefresh();
                    });
                }, closeOnClick: false);

            var mediaButton = dialog.AddListButton(
                $"Media File: {tier.MediaFilename?.Name ?? "(None)"}", () =>
                {
                    DialogManager.Instance.ClearDialog();
                    string[] extensions =
                    {
                        "png",
                        "jpg",
                        "jpeg"
                    };

                    FileExplorerHelper.OpenChooseFile("", extensions, mediaPath =>
                    {
                        var mediaSetting = new FileInfoSetting(tier.MediaFilename, _presetRef,
                            nameof(CareerTier.MediaFilename), mediaFile =>
                            {
                                tier.MediaFilename = mediaFile;
                                SaveAndRefresh();
                            });
                        mediaSetting.Value = new FileInfo(mediaPath);
                    });
                }, closeOnClick: false);

            dialog.AddListButton("Clear Media File", () =>
            {
                tier.MediaFilename = null;
                mediaButton.Text.text = $"Media File: {tier.MediaFilename?.Name ?? "(None)"}";
                SaveAndRefresh();
            }, closeOnClick: false);
        }

        private async void ShowSongPickerDialog(CareerTier tier, string filterQuery = null)
        {
            if (SongContainer.Count == 0)
            {
                ToastManager.ToastError("No Songs in Library!");
                return;
            }

            SongEntry selected = null;
            var pickerDialog =
                DialogManager.Instance.ShowLibrarySearchDialog("Select a Song to Add",
                    songEntry => { selected = songEntry; });

            await pickerDialog.WaitUntilClosed();

            if (selected == null)
            {
                ToastManager.ToastInformation("No Song Selected");
                return;
            }

            ShowIdentifierChoiceDialog(tier, selected);
        }

        private void ShowIdentifierChoiceDialog(CareerTier tier, SongEntry song)
        {
            var dialog = DialogManager.Instance.ShowList($"Add: {song.Artist.Original} - {song.Name.Original}");

            if (!string.IsNullOrEmpty(song.YargGuid))
            {
                dialog.AddListButton("Song ID (Use for YARG songs)", () =>
                {
                    tier.AddSong(CareerSong.FromSongEntry(song, CareerSongIdentifier.SongId));
                    SaveAndRefresh();
                });
            }

            if (song is RBCONEntry rbconEntry && !string.IsNullOrEmpty(rbconEntry.RBSongId))
            {
                dialog.AddListButton("Song Short Name (Use for RBCON songs)", () =>
                {
                    tier.AddSong(CareerSong.FromSongEntry(song, CareerSongIdentifier.ShortName));
                    SaveAndRefresh();
                });
            }

            dialog.AddListButton("Song Hash", () =>
            {
                tier.AddSong(CareerSong.FromSongEntry(song, CareerSongIdentifier.SongHash));
                SaveAndRefresh();
            });

            dialog.AddListButton("Song Tuple (Artist, Title, Source, Charter)", () =>
            {
                tier.AddSong(CareerSong.FromSongEntry(song, CareerSongIdentifier.SongTuple));
                SaveAndRefresh();
            });
        }

        private void ShowEditSongDialog(CareerTier tier, CareerSong song, int songIndex)
        {
            var dialog = DialogManager.Instance.ShowList("Edit Career Song");

            string currentSongName = song.SongEntry != null
                ? song.SongEntry.Name.Original
                : (!string.IsNullOrEmpty(song.Description)
                    ? song.Description
                    : (song.ShortName ?? song.SongTuple?.Title ?? "Unknown"));
            string currentArtist = song.SongEntry != null
                ? song.SongEntry.Artist.Original
                : (song.SongTuple?.Artist ?? "Unknown");

            dialog.AddListButton($"Song: {currentArtist} - {currentSongName}", null);
            dialog.AddListButton($"Current Identifier: {song.Identifier}", null);

            if (!string.IsNullOrEmpty(song.SongEntry?.YargGuid))
            {
                dialog.AddListButton("Change Identifier to: Song ID (GUID)", () =>
                {
                    song.UpdateIdentifier(CareerSongIdentifier.SongId);
                    SaveAndRefresh();
                });
            }

            dialog.AddListButton("Change Identifier to: Song Hash", () =>
            {
                song.UpdateIdentifier(CareerSongIdentifier.SongHash);
                SaveAndRefresh();
            });

            dialog.AddListButton("Change Identifier to: Song Tuple", () =>
            {
                song.UpdateIdentifier(CareerSongIdentifier.SongTuple);
                SaveAndRefresh();
            });

            if (song.SongEntry is RBCONEntry rbconEntry && !string.IsNullOrEmpty(rbconEntry.RBSongId))
            {
                dialog.AddListButton("Change Identifier to: Short Name", () =>
                {
                    song.UpdateIdentifier(CareerSongIdentifier.ShortName);
                    SaveAndRefresh();
                });
            }

            dialog.AddListButton(
                $"Edit Fallback Description: {(string.IsNullOrEmpty(song.Description) ? "(None)" : song.Description)}",
                () =>
                {
                    DialogManager.Instance.ClearDialog();
                    DialogManager.Instance.ShowRenameDialog(song.Description ?? string.Empty, newDesc =>
                    {
                        song.Description = newDesc?.Trim();
                        SaveAndRefresh();
                    });
                }, closeOnClick: false);

            dialog.AddListButton("Delete Song", () =>
            {
                tier.RemoveSongAt(songIndex);
                SaveAndRefresh();
            });
        }

        /// <summary>
        /// Spawns a career header into <see cref="container"/>. Caller is responsible for calling Initialize on the returned header.
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private CareerHeader SpawnCareerInfo(Transform container)
        {
            if (_careerInfoPrefab == null)
            {
                _careerInfoPrefab = Addressables.LoadAssetAsync<GameObject>("SettingTab/CareerHeader")
                    .WaitForCompletion();
            }

            var go = Object.Instantiate(_careerInfoPrefab, container);
            var header = go.GetComponent<CareerHeader>();
            return header;
        }

        /// <summary>
        /// Spawns a career tier into <see cref="container"/>. Caller is responsible for calling Initialize on the returned tier.
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private CareerHeader SpawnCareerTier(Transform container)
        {
            if (_careerTierPrefab == null)
            {
                _careerTierPrefab =
                    Addressables.LoadAssetAsync<GameObject>("SettingTab/CareerTier").WaitForCompletion();
            }

            var go = Object.Instantiate(_careerTierPrefab, container);
            var header = go.GetComponent<CareerHeader>();
            return header;
        }

        /// <summary>
        /// Spawns a career song into <see cref="container"/>. Caller is responsible for calling Initialize on the returned song.
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private CareerHeader SpawnCareerSong(Transform container)
        {
            if (_careerSongPrefab == null)
            {
                _careerSongPrefab =
                    Addressables.LoadAssetAsync<GameObject>("SettingTab/CareerSong").WaitForCompletion();
            }

            var go = Object.Instantiate(_careerSongPrefab, container);
            var header = go.GetComponent<CareerHeader>();
            return header;
        }

        private static void DimHeaderBackground(GameObject headerGo, float alphaMultiplier)
        {
            var image = headerGo.GetComponent<Image>();
            if (image != null)
            {
                var c = image.color;
                image.color = new Color(c.r, c.g, c.b, c.a * alphaMultiplier);
            }
        }
    }
}