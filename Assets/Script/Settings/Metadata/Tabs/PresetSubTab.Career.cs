using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly CareerPresetPreviewBuilder _careerPreview;

        public CareerPresetSubTab(CustomContent<CareerPreset> customContent, IPreviewBuilder previewBuilder = null,
            bool hasDescriptions = true) : base(customContent,
            previewBuilder ?? new CareerPresetPreviewBuilder(), hasDescriptions)
        {
            _careerPreview = (CareerPresetPreviewBuilder) PreviewBuilder;
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
                : AddEmptyTierAndEdit;
            tiersHeader.Initialize(Localize.Key("Settings.Header.CareerTiers"), null, null, null, null, null);
            tiersHeader.SetButtonsVisible(false);
            _fieldIndex++;

            var tiers = _presetRef.Tiers;
            if (tiers == null || tiers.Count == 0)
            {
                AddTrailingTierButton(container, navGroup, onAddTier, false);
                return;
            }

            int summaryRowIndex = 0;
            for (int t = 0; t < tiers.Count; t++)
            {
                int tierIndex = t;
                var tier = tiers[tierIndex];

                string tierName = string.IsNullOrWhiteSpace(tier.Name) ? "..." : tier.Name;
                string tierTitle = $"Tier {tierIndex + 1}: {tierName}";
                string tierDetails =
                    $" [{(tier.IsBonus ? "Bonus | " : "")}{tier.UnlockCriteria} {tier.UnlockType} | {FormatSessionLength(tier.CompletionMode)} | {tier.VenueSize}]";

                var tierHeader = SpawnCareerTier(container);
                void RefreshTierSummary()
                {
                    string currentName = string.IsNullOrWhiteSpace(tier.Name) ? "..." : tier.Name;
                    string currentDetails =
                        $" [{(tier.IsBonus ? "Bonus | " : "")}{tier.UnlockCriteria} {tier.UnlockType} | {FormatSessionLength(tier.CompletionMode)} | {tier.VenueSize}]";
                    tierHeader.SetText($"Tier {tierIndex + 1}: {currentName}{currentDetails}");
                    tierHeader.SetDetailsTitle(currentName);
                }
                Action onAddSong = ReadOnlyFields ? null : () => AddEmptySongAndEdit(tier);
                Action onEditTier = ReadOnlyFields ? null : () => ShowEditTierDialog(tier,
                    onInputBlur: RefreshTierSummary);
                Action onUpTier = (ReadOnlyFields || tierIndex == 0)
                    ? null
                    : () =>
                    {
                        _presetRef.MoveTier(tierIndex, tierIndex - 1);
                        SaveAndRefresh(tier);
                    };
                Action onDownTier = (ReadOnlyFields || tierIndex >= tiers.Count - 1)
                    ? null
                    : () =>
                    {
                        _presetRef.MoveTier(tierIndex, tierIndex + 1);
                        SaveAndRefresh(tier);
                    };
                Action onRemoveTier = ReadOnlyFields
                    ? null
                    : () =>
                    {
                        _presetRef.RemoveTierAt(tierIndex);
                        SaveAndRefresh();
                    };

                tierHeader.Initialize(tierTitle + tierDetails, onAddSong, onEditTier, onUpTier, onDownTier,
                    onRemoveTier, marquee: true);
                tierHeader.UseSettingRowStyle(summaryRowIndex++ % 2 != 0);
                // An empty details title is treated by SettingsMenu as a row with
                // no editor, so use the same visible placeholder as the summary.
                tierHeader.SetDetailsTitle(tierName);
                tierHeader.SetRowIdentity(tier);
                tierHeader.SetRowActions(false, onUpTier, onDownTier, onRemoveTier);
                AddCareerRowNavigation(navGroup, tierHeader,
                    () => ShowEditTierDialog(tier, onAddSong, onUpTier, onDownTier, onRemoveTier,
                        RefreshTierSummary));
                _fieldIndex++;

                // Render songs in this tier
                var songs = tier.Songs;
                if (songs == null || songs.Length == 0)
                {
                    // The Add Song action below is the empty-state affordance.
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

                        string songLabel = FormatSongSummary(song, songIndex, out string songDetailsTitle);

                        var songHeader = SpawnCareerSong(container);
                        void RefreshSongSummary()
                        {
                            string label = FormatSongSummary(song, songIndex, out string detailsTitle);
                            songHeader.SetText(label);
                            songHeader.SetDetailsTitle(detailsTitle);
                        }
                        Action onEditSong = ReadOnlyFields ? null : () =>
                            ShowEditSongDialog(tier, song, songIndex, onSummaryChanged: RefreshSongSummary);
                        bool isFirstSongInCareer = tierIndex == 0 && songIndex == 0;
                        bool isLastSongInCareer = tierIndex == tiers.Count - 1 &&
                            songIndex >= songs.Length - 1;

                        Action onUpSong = (ReadOnlyFields || isFirstSongInCareer)
                            ? null
                            : () =>
                            {
                                if (songIndex > 0)
                                {
                                    tier.MoveSong(songIndex, songIndex - 1);
                                }
                                else
                                {
                                    tier.RemoveSongAt(songIndex);
                                    tiers[tierIndex - 1].AddSong(song);
                                }
                                SaveAndRefresh(song);
                            };
                        Action onDownSong = (ReadOnlyFields || isLastSongInCareer)
                            ? null
                            : () =>
                            {
                                if (songIndex < songs.Length - 1)
                                {
                                    tier.MoveSong(songIndex, songIndex + 1);
                                }
                                else
                                {
                                    tier.RemoveSongAt(songIndex);
                                    tiers[tierIndex + 1].InsertSong(0, song);
                                }
                                SaveAndRefresh(song);
                            };
                        Action onRemoveSong = ReadOnlyFields
                            ? null
                            : () =>
                            {
                                tier.RemoveSongAt(songIndex);
                                SaveAndRefresh();
                            };

                        songHeader.Initialize(songLabel, null, onEditSong, onUpSong, onDownSong, onRemoveSong,
                            marquee: true);
                        songHeader.UseSettingRowStyle(summaryRowIndex++ % 2 != 0);
                        songHeader.SetDetailsTitle(songDetailsTitle);
                        songHeader.SetRowIdentity(song);
                        songHeader.SetRowActions(true, onUpSong, onDownSong, onRemoveSong);
                        AddCareerRowNavigation(navGroup, songHeader,
                            () => ShowEditSongDialog(tier, song, songIndex, onUpSong, onDownSong, onRemoveSong,
                                RefreshSongSummary));
                        _fieldIndex++;
                    }
                }

                AddTrailingSongButton(container, navGroup, onAddSong, summaryRowIndex++ % 2 != 0);
            }

            AddTrailingTierButton(container, navGroup, onAddTier, summaryRowIndex % 2 != 0);
        }

        private void AddTrailingTierButton(Transform container, NavigationGroup navGroup, Action onAddTier,
            bool alternate)
        {
            if (onAddTier == null)
            {
                return;
            }

            var addTierRow = SpawnCareerInfo(container);
            addTierRow.Initialize(string.Empty, onAddTier, null, null, null, null);
            addTierRow.ConfigureAsStandaloneAddButton("Add Tier", 24, alternate);

            var navigatable = RuntimeNavigatable.Attach(addTierRow.gameObject, onAddTier);
            addTierRow.TightenStandaloneButtonOutline();
            navGroup.AddNavigatable(navigatable);
            _fieldIndex++;
        }

        private void AddTrailingSongButton(Transform container, NavigationGroup navGroup, Action onAddSong,
            bool alternate)
        {
            if (onAddSong == null)
            {
                return;
            }

            var addSongRow = SpawnCareerInfo(container);
            addSongRow.Initialize(string.Empty, onAddSong, null, null, null, null);
            addSongRow.ConfigureAsStandaloneAddButton("Add Song", 48, alternate);

            var navigatable = RuntimeNavigatable.Attach(addSongRow.gameObject, onAddSong);
            addSongRow.TightenStandaloneButtonOutline();
            navGroup.AddNavigatable(navigatable);
            _fieldIndex++;
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

        private void SaveAndRefresh(object rowIdentity)
        {
            Save();
            SettingsMenu.Instance?.RefreshSettingsAndSelectCareerRow(rowIdentity);
        }

        public void ClearEditor()
        {
            _careerPreview.Show(Array.Empty<CareerPresetPreviewBuilder.Field>());
        }

        private void AddCareerRowNavigation(NavigationGroup navGroup, CareerHeader row, Action showActions)
        {
            row.SetShowDetails(showActions);
            var navigatable = RuntimeNavigatable.Attach(row.gameObject, _careerPreview.FocusSettings);
            var selectionVisual = navigatable.SelectionVisual;
            navigatable.SelectionVisual = selected =>
            {
                selectionVisual?.Invoke(selected);
                if (selected)
                {
                    row.ShowDetails();
                }
            };
            navGroup.AddNavigatable(navigatable);
        }

        private sealed class CareerEnumSetting<T> : DropdownSetting<T> where T : Enum
        {
            private readonly Func<T, string> _formatter;

            public CareerEnumSetting(T value, Action<T> onChange, Func<T, string> formatter = null)
                : base(value, onChange, localizable: false)
            {
                _formatter = formatter;
            }

            public override string ValueToString(T value) =>
                _formatter?.Invoke(value) ?? Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
        }

        private DropdownSetting<T> CreateEnumSetting<T>(T value, Action<T> onChange,
            Func<T, string> formatter = null) where T : Enum
        {
            var setting = new CareerEnumSetting<T>(value, newValue =>
            {
                onChange(newValue);
                Save();
            }, formatter);
            foreach (T possibleValue in Enum.GetValues(typeof(T)))
            {
                setting.Add(possibleValue);
            }
            return setting;
        }

        private static string FormatSessionLength(CareerCompletionMode value) => value switch
        {
            CareerCompletionMode.SingleSong => "Individual Songs",
            CareerCompletionMode.FullTierPlaylist => "Tier Setlist",
            CareerCompletionMode.FullCareerPlaylist => "Career Setlist",
            _ => value.ToString(),
        };

        private static string FormatSongSummary(CareerSong song, int songIndex, out string detailsTitle)
        {
            string songName = song.SongEntry != null
                ? song.SongEntry.Name.Original
                : (!string.IsNullOrEmpty(song.Description)
                    ? song.Description
                    : (song.ShortName ?? song.SongTuple?.Title ?? song.SongId?.ToString() ?? "Unknown Song"));
            string artistName = song.SongEntry != null
                ? song.SongEntry.Artist.Original
                : (song.SongTuple?.Artist ?? "Unknown Artist");

            // Only the active identifier determines whether this row has a
            // selection. Ignore stale values belonging to other modes.
            bool hasSelectedIdentifier = song.Identifier switch
            {
                CareerSongIdentifier.SongId => song.SongId.HasValue && song.SongId.Value != Guid.Empty,
                CareerSongIdentifier.SongHash => song.SongHash.HasValue,
                CareerSongIdentifier.ShortName => !string.IsNullOrWhiteSpace(song.ShortName),
                CareerSongIdentifier.SongTuple => song.SongTuple.HasValue &&
                    (!string.IsNullOrWhiteSpace(song.SongTuple.Value.Title) ||
                     !string.IsNullOrWhiteSpace(song.SongTuple.Value.Artist) ||
                     !string.IsNullOrWhiteSpace(song.SongTuple.Value.Source) ||
                     !string.IsNullOrWhiteSpace(song.SongTuple.Value.Charter)),
                _ => false,
            };
            bool isClearedSong = song.SongEntry == null && !hasSelectedIdentifier;
            detailsTitle = isClearedSong ? "..." : songName;
            return isClearedSong
                ? $"      {songIndex + 1}. ... [{song.Identifier}]"
                : $"      {songIndex + 1}. {artistName} - {songName} [{song.Identifier}]";
        }

        private void ShowEditTierDialog(CareerTier tier, Action onAddSong = null, Action onUp = null,
            Action onDown = null, Action onRemove = null, Action onInputBlur = null)
        {
            bool editable = !ReadOnlyFields;
            bool isFirstTier = _presetRef?.Tiers.Count > 0 && ReferenceEquals(_presetRef.Tiers[0], tier);

            var fields = new List<CareerPresetPreviewBuilder.Field>
            {
                new("Name", new StringSetting(tier.Name ?? string.Empty, value =>
                {
                    tier.Name = value;
                    SettingsMenu.Instance?.ShowSettingDetails(tier.Name);
                    Save();
                }), editable),
                new("Description", new StringSetting(tier.Description ?? string.Empty, value =>
                {
                    tier.Description = value;
                    Save();
                }), editable),
            };

            fields.AddRange(new CareerPresetPreviewBuilder.Field[]
            {
                new("Unlock Criteria", CreateEnumSetting(tier.UnlockType, value => tier.UnlockType = value), editable),
                new("Unlock Threshold", new IntSetting(tier.UnlockCriteria, min: 0, onChange: value =>
                {
                    tier.UnlockCriteria = value;
                    Save();
                }), editable, !isFirstTier),
                new("Unlock Text", new StringSetting(tier.CustomUnlockText ?? string.Empty, value =>
                {
                    tier.CustomUnlockText = value;
                    Save();
                }), editable, !isFirstTier),
                new("Is Bonus Tier", new ToggleSetting(tier.IsBonus, value =>
                {
                    tier.IsBonus = value;
                    Save();
                }), editable),
                new("Session Length", CreateEnumSetting(tier.CompletionMode,
                    value => tier.CompletionMode = value, FormatSessionLength), editable),
                new("Venue Size", CreateEnumSetting(tier.VenueSize, value => tier.VenueSize = value), editable),
                new("Venue Hint", new StringSetting(tier.VenueHint ?? string.Empty, value =>
                {
                    tier.VenueHint = value;
                    Save();
                }), editable),
                new("Bonus Media Type", CreateEnumSetting(tier.CompletionBonus, value =>
                {
                    tier.CompletionBonus = value;
                    bool hasBonusMedia = !isFirstTier && value != CompletionBonusType.None;
                    _careerPreview.SetFieldVisible("Bonus Media File", hasBonusMedia);
                    _careerPreview.SetFieldVisible("Bonus Media Text", hasBonusMedia);
                }), editable, !isFirstTier),
                new("Bonus Media File", new FileInfoSetting(tier.MediaFilename, _presetRef,
                    nameof(CareerTier.MediaFilename), value =>
                    {
                        tier.MediaFilename = value;
                        Save();
                    }), editable, !isFirstTier && tier.CompletionBonus != CompletionBonusType.None),
                new("Bonus Media Text", new StringSetting(tier.CompletionBonusText ?? string.Empty, value =>
                {
                    tier.CompletionBonusText = value;
                    Save();
                }), editable, !isFirstTier && tier.CompletionBonus != CompletionBonusType.None),
            });

            _careerPreview.Show(fields, onInputBlur: onInputBlur);
        }

#if false // Replaced by native setting controls above; retained temporarily for comparison while this UI settles.
        private void LegacyShowEditTierDialog(CareerTier tier, Action onAddSong = null, Action onUp = null,
            Action onDown = null, Action onRemove = null)
        {
            if (ReadOnlyFields)
            {
                _careerPreview.Show($"Tier: {tier.Name}", Array.Empty<CareerPresetPreviewBuilder.ActionItem>());
                return;
            }

            var dialog = _careerPreview.CreateActions($"Edit Tier: {tier.Name}");

            if (onAddSong != null) dialog.AddListButton("Add Song", onAddSong);
            if (onUp != null) dialog.AddListButton("Move Tier Up", onUp);
            if (onDown != null) dialog.AddListButton("Move Tier Down", onDown);

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

            dialog.AddListButton(
                $"Media File: {tier.MediaFilename?.Name ?? "(None)"}", () =>
                {
                    DialogManager.Instance.ClearDialog();
                    string[] extensions =
                    {
                        "png",
                        "jpg",
                        "jpeg",
                        "webm",
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
                SaveAndRefresh();
            }, closeOnClick: false);

            if (onRemove != null) dialog.AddListButton("Delete Tier", onRemove);
            dialog.Show();
        }
#endif

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

        private void AddEmptySongAndEdit(CareerTier tier)
        {
            var song = new CareerSong
            {
                Id = Guid.NewGuid(),
                Identifier = CareerSongIdentifier.SongId,
                Description = string.Empty,
            };

            tier.AddSong(song);
            SaveAndRefresh(song);
            _careerPreview.FocusFirstDropdownAndOpen();
        }

        private void AddEmptyTierAndEdit()
        {
            var tier = new CareerTier
            {
                Id = Guid.NewGuid(),
                Name = string.Empty,
                Description = string.Empty,
                UnlockType = UnlockType.StarCount,
                UnlockCriteria = 0,
                VenueSize = VenueSize.NotSpecified,
                CompletionMode = CareerCompletionMode.SingleSong,
                Songs = Array.Empty<CareerSong>(),
            };

            _presetRef.AddTier(tier);
            SaveAndRefresh(tier);
            _careerPreview.FocusSettings();
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

        private void ShowEditSongDialog(CareerTier tier, CareerSong song, int songIndex,
            Action onUp = null, Action onDown = null, Action onRemove = null,
            Action onSummaryChanged = null)
        {
            if (song.SongId.HasValue || song.SongHash.HasValue || !string.IsNullOrEmpty(song.ShortName) ||
                song.SongTuple.HasValue)
            {
                song.RefreshSongEntry();
            }
            if (song.SongEntry != null && song.Identifier == CareerSongIdentifier.SongId &&
                !HasSongId(song.SongEntry))
            {
                song.UpdateIdentifier(CareerSongIdentifier.SongHash, song.SongEntry);
                Save();
                onSummaryChanged?.Invoke();
            }
            var state = new SongFilterState(song, onSummaryChanged);
            ShowSongFields(state);
        }

        private static bool HasSongId(SongEntry song) =>
            song != null && Guid.TryParse(song.YargGuid, out Guid guid) && guid != Guid.Empty;

        private enum SongFacet
        {
            Song,
            Artist,
            Source,
            Charter,
        }

        /// <summary>
        /// Tracks which facets the user has deliberately chosen. The displayed values always describe the
        /// resolved song, while only deliberate choices constrain the option lists. This lets any of the four
        /// dropdowns be the first filter without the initially displayed song over-constraining the others.
        /// </summary>
        private sealed class SongFilterState
        {
            public readonly CareerSong CareerSong;
            public readonly Action SummaryChanged;
            public readonly HashSet<SongFacet> ActiveFacets = new();
            public bool LockAllFacets;

            public string Song;
            public string Artist;
            public string Source;
            public string Charter;

            public SongFilterState(CareerSong song, Action summaryChanged)
            {
                CareerSong = song;
                SummaryChanged = summaryChanged;
                LockAllFacets = song.SongEntry != null;
                Song = song.SongEntry?.Name.Original ?? song.SongTuple?.Title ?? song.Description ?? string.Empty;
                Artist = song.SongEntry?.Artist.Original ?? song.SongTuple?.Artist ?? string.Empty;
                string rawSource = song.SongEntry?.Source.Original ?? song.SongTuple?.Source ?? string.Empty;
                Source = string.IsNullOrWhiteSpace(rawSource)
                    ? string.Empty
                    : SongSources.SourceToGameName(rawSource);
                Charter = song.SongEntry?.Charter.Original ?? song.SongTuple?.Charter ?? string.Empty;
            }
        }

        private void ShowSongFields(SongFilterState state, int? selectedIndex = null)
        {
            CareerSong song = state.CareerSong;

            bool editable = !ReadOnlyFields;

            string identifier = song.Identifier switch
            {
                CareerSongIdentifier.SongHash => "Song Hash",
                CareerSongIdentifier.SongTuple => "Song Tuple",
                _ => "Song ID",
            };
            var identifierSetting = new DropdownSetting<string>(identifier, value =>
            {
                var newIdentifier = value switch
                {
                    "Song Hash" => CareerSongIdentifier.SongHash,
                    "Song Tuple" => CareerSongIdentifier.SongTuple,
                    _ => CareerSongIdentifier.SongId,
                };
                bool replacedUnavailableSongId = newIdentifier == CareerSongIdentifier.SongId &&
                    song.SongEntry != null && !HasSongId(song.SongEntry);
                if (replacedUnavailableSongId)
                {
                    newIdentifier = CareerSongIdentifier.SongHash;
                }
                song.UpdateIdentifier(newIdentifier);
                Save();
                state.SummaryChanged?.Invoke();
                if (replacedUnavailableSongId)
                {
                    ShowSongFields(state, 5);
                }
            }, localizable: false)
            {
                "Song ID",
                "Song Hash",
                "Song Tuple",
            };

            _careerPreview.Show(new CareerPresetPreviewBuilder.Field[]
            {
                new("Song", CreateSongFacetSetting(state, SongFacet.Song), editable),
                new("Artist", CreateSongFacetSetting(state, SongFacet.Artist), editable),
                new("Source", CreateSongFacetSetting(state, SongFacet.Source), editable),
                new("Charter", CreateSongFacetSetting(state, SongFacet.Charter), editable),
                new("Clear", () => ClearSongFacets(state), MenuData.Colors.CancelButton),
                new("Identifier", identifierSetting, editable),
                new("Fallback Description", new StringSetting(song.Description ?? string.Empty, value =>
                {
                    song.Description = value;
                    Save();
                }), editable),
            }, selectedIndex, state.SummaryChanged);
        }

        private SearchableDropdownSetting<string> CreateSongFacetSetting(SongFilterState state, SongFacet facet)
        {
            string currentValue = GetFacetValue(state, facet);
            var setting = new SearchableDropdownSetting<string>(currentValue,
                value => SelectSongFacet(state, facet, value), localizable: false);

            // A resolved song, or a facet the user deliberately selected while
            // resolving one, is immutable until the explicit Clear action.
            if (state.LockAllFacets || state.ActiveFacets.Contains(facet))
            {
                setting.Add(currentValue);
                return setting;
            }

            if (string.IsNullOrWhiteSpace(currentValue))
            {
                setting.Add(string.Empty);
            }

            IEnumerable<string> values = SongContainer.Songs
                .Where(entry => MatchesActiveFacets(entry, state, facet))
                .Select(entry => GetFacetValue(entry, facet))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

            foreach (string value in values)
            {
                setting.Add(value);
            }

            // Keep unresolved/custom career entries visible even when their metadata is not in the library.
            if (!string.IsNullOrWhiteSpace(currentValue) &&
                !setting.Any(value => string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase)))
            {
                setting.Add(currentValue);
            }

            return setting;
        }

        private void SelectSongFacet(SongFilterState state, SongFacet facet, string value)
        {
            SetFacetValue(state, facet, value);
            bool clearingFacet = string.IsNullOrWhiteSpace(value);

            if (clearingFacet)
            {
                state.ActiveFacets.Remove(facet);
            }
            else
            {
                state.ActiveFacets.Add(facet);
            }

            SongEntry[] matches = SongContainer.Songs
                .Where(entry => MatchesActiveFacets(entry, state))
                .OrderBy(entry => entry.Name.Original, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Artist.Original, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Source.Original, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Charter.Original, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // A deliberate choice filters the other facets, but must not silently
            // choose their first value. Fill an unchosen facet only when every
            // remaining song agrees on its value; otherwise leave it blank so the
            // user can make the next disambiguating choice.
            foreach (SongFacet otherFacet in Enum.GetValues(typeof(SongFacet)))
            {
                if (state.ActiveFacets.Contains(otherFacet)) continue;
                if (clearingFacet && otherFacet == facet) continue;

                string[] values = matches
                    .Select(entry => GetFacetValue(entry, otherFacet))
                    .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(2)
                    .ToArray();

                SetFacetValue(state, otherFacet, values.Length == 1 ? values[0] : string.Empty);
            }

            bool allFacetsResolved = Enum.GetValues(typeof(SongFacet))
                .Cast<SongFacet>()
                .All(candidate => !string.IsNullOrWhiteSpace(GetFacetValue(state, candidate)));

            if (!allFacetsResolved)
            {
                ShowSongFields(state, (int) facet);
                return;
            }

            // Multiple library records can share the same four displayed facets.
            // Prefer the current entry in that case, otherwise use their stable sort.
            SongEntry selected = state.CareerSong.SongEntry != null &&
                matches.Contains(state.CareerSong.SongEntry)
                    ? state.CareerSong.SongEntry
                    : matches.FirstOrDefault();

            if (selected == null)
            {
                ShowSongFields(state, (int) facet);
                return;
            }

            state.Song = selected.Name.Original;
            state.Artist = selected.Artist.Original;
            state.Source = SongSources.SourceToGameName(selected.Source.Original);
            state.Charter = selected.Charter.Original;

            CareerSongIdentifier identifier = state.CareerSong.Identifier;
            if (identifier == CareerSongIdentifier.ShortName &&
                (selected is not RBCONEntry rbconEntry || string.IsNullOrEmpty(rbconEntry.RBSongId)))
            {
                identifier = CareerSongIdentifier.SongId;
            }
            if (identifier == CareerSongIdentifier.SongId && !HasSongId(selected))
            {
                identifier = CareerSongIdentifier.SongHash;
            }

            state.CareerSong.UpdateIdentifier(identifier, selected);
            // Rebuild the career list as well as the editor so the resolved
            // title/artist replaces the fallback identifier immediately.
            SaveAndRefresh(state.CareerSong);
        }

        private void ClearSongFacets(SongFilterState state)
        {
            state.Song = string.Empty;
            state.Artist = string.Empty;
            state.Source = string.Empty;
            state.Charter = string.Empty;
            state.ActiveFacets.Clear();
            state.LockAllFacets = false;

            state.CareerSong.SongId = null;
            state.CareerSong.SongHash = null;
            state.CareerSong.ShortName = null;
            state.CareerSong.SongTuple = null;
            state.CareerSong.SongEntry = null;

            SaveAndRefresh(state.CareerSong);
        }

        private static bool MatchesActiveFacets(SongEntry entry, SongFilterState state,
            SongFacet? excludedFacet = null)
        {
            foreach (SongFacet facet in state.ActiveFacets)
            {
                if (facet == excludedFacet)
                {
                    continue;
                }

                if (!string.Equals(GetFacetValue(entry, facet), GetFacetValue(state, facet),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetFacetValue(SongEntry entry, SongFacet facet)
        {
            return facet switch
            {
                SongFacet.Song => entry.Name.Original,
                SongFacet.Artist => entry.Artist.Original,
                SongFacet.Source => SongSources.SourceToGameName(entry.Source.Original),
                SongFacet.Charter => entry.Charter.Original,
                _ => string.Empty,
            };
        }

        private static string GetFacetValue(SongFilterState state, SongFacet facet)
        {
            return facet switch
            {
                SongFacet.Song => state.Song,
                SongFacet.Artist => state.Artist,
                SongFacet.Source => state.Source,
                SongFacet.Charter => state.Charter,
                _ => string.Empty,
            };
        }

        private static void SetFacetValue(SongFilterState state, SongFacet facet, string value)
        {
            switch (facet)
            {
                case SongFacet.Song: state.Song = value; break;
                case SongFacet.Artist: state.Artist = value; break;
                case SongFacet.Source: state.Source = value; break;
                case SongFacet.Charter: state.Charter = value; break;
            }
        }

#if false // Replaced by the Identifier dropdown above.
        private void LegacyShowEditSongDialog(CareerTier tier, CareerSong song, int songIndex,
            Action onUp = null, Action onDown = null, Action onRemove = null)
        {
            if (ReadOnlyFields)
            {
                _careerPreview.Show("Career Song", Array.Empty<CareerPresetPreviewBuilder.ActionItem>());
                return;
            }

            var dialog = _careerPreview.CreateActions("Edit Career Song");

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

            if (onUp != null) dialog.AddListButton("Move Song Up", onUp);
            if (onDown != null) dialog.AddListButton("Move Song Down", onDown);
            if (onRemove != null) dialog.AddListButton("Delete Song", onRemove);
            dialog.Show();
        }
#endif

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
