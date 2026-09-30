using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YARG.Career;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu;
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
    public class CareerPresetSubTab : PresetSubTab<CareerBase>
    {
        public CareerPresetSubTab(CustomContent<CareerBase> customContent, IPreviewBuilder previewBuilder = null,
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
            var careerInfoGo = SpawnRawHeader(container, Localize.Key("Settings.Header.CareerInformation"));
            _fieldIndex++;

            // Editable CareerBase metadata fields
            CreateField(container, navGroup, nameof(CareerBase), nameof(CareerBase.Description),
                new StringSetting(_presetRef.Description ?? string.Empty, val =>
                {
                    _presetRef.Description = val;
                    Save();
                }));

            CreateField(container, navGroup, nameof(CareerBase), nameof(CareerBase.BackgroundImageName),
                new StringSetting(_presetRef.BackgroundImageName ?? string.Empty, val =>
                {
                    _presetRef.BackgroundImageName = val;
                    Save();
                }));

            CreateField(container, navGroup, nameof(CareerBase), nameof(CareerBase.Source),
                new StringSetting(_presetRef.Source ?? string.Empty, val =>
                {
                    _presetRef.Source = val;
                    Save();
                }));

            // Header: Career Tiers
            var tiersHeaderGo = SpawnRawHeader(container, Localize.Key("Settings.Header.CareerTiers"));
            _fieldIndex++;

            if (!ReadOnlyFields)
            {
                var addTierBtn = AttachHeaderButton(tiersHeaderGo, "+ Add Tier", MenuData.Colors.NavigationGreen, -10f, 110f, () =>
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
                });
                AddButtonToNav(addTierBtn, navGroup);
            }

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
                string tierDetails = $" [{(tier.IsBonus ? "Bonus | " : "")}{tier.UnlockCriteria} {tier.UnlockType} | {tier.VenueSize} | {tier.CompletionMode}]";

                var tierHeaderGo = SpawnSubHeader(container, tierTitle + tierDetails);
                _fieldIndex++;

                if (!ReadOnlyFields)
                {
                    float xPos = -10f;

                    // Delete button [✖]
                    var delBtn = AttachHeaderButton(tierHeaderGo, "<sprite='AssortedIcons' index=6>", MenuData.Colors.CancelButton, xPos, 34f, () =>
                    {
                        ShowCompactConfirmation("Delete Tier", $"Are you sure you want to delete '{tier.Name}'?", "Menu.Common.Delete",
                            MenuData.Colors.CancelButton, () =>
                            {
                                _presetRef.RemoveTierAt(tierIndex);
                                SaveAndRefresh();
                            });
                    });
                    AddButtonToNav(delBtn, navGroup);
                    xPos -= 38f;

                    // Move down button [▼]
                    if (tierIndex < tiers.Count - 1)
                    {
                        var downBtn = AttachHeaderButton(tierHeaderGo, "▼", MenuData.Colors.NavigationBlue, xPos, 34f, () =>
                        {
                            _presetRef.MoveTier(tierIndex, tierIndex + 1);
                            SaveAndRefresh();
                        });
                        AddButtonToNav(downBtn, navGroup);
                        xPos -= 38f;
                    }

                    // Move up button [▲]
                    if (tierIndex > 0)
                    {
                        var upBtn = AttachHeaderButton(tierHeaderGo, "▲", MenuData.Colors.NavigationBlue, xPos, 34f, () =>
                        {
                            _presetRef.MoveTier(tierIndex, tierIndex - 1);
                            SaveAndRefresh();
                        });
                        AddButtonToNav(upBtn, navGroup);
                        xPos -= 38f;
                    }

                    // Edit Tier button
                    var editBtn = AttachHeaderButton(tierHeaderGo, "Edit Tier", MenuData.Colors.NavigationYellow, xPos, 80f, () =>
                    {
                        ShowEditTierDialog(tier);
                    });
                    AddButtonToNav(editBtn, navGroup);
                    xPos -= 84f;

                    // Add Song button
                    var addSongBtn = AttachHeaderButton(tierHeaderGo, "+ Song", MenuData.Colors.NavigationGreen, xPos, 70f, () =>
                    {
                        ShowSongPickerDialog(tier);
                    });
                    AddButtonToNav(addSongBtn, navGroup);
                }

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

                        string songName = song.SongEntry != null ? song.SongEntry.Name.Original : (!string.IsNullOrEmpty(song.Description) ? song.Description : (song.ShortName ?? song.SongTuple?.Title ?? song.SongId?.ToString() ?? "Unknown Song"));
                        string artistName = song.SongEntry != null ? song.SongEntry.Artist.Original : (song.SongTuple?.Artist ?? "Unknown Artist");
                        string songLabel = $"      {songIndex + 1}. {artistName} - {songName} [{song.Identifier}]";

                        var songGo = SpawnSubHeader(container, songLabel);
                        DimHeaderBackground(songGo, 0.25f);
                        _fieldIndex++;

                        if (!ReadOnlyFields)
                        {
                            float songXPos = -10f;

                            // Remove song button [✖]
                            var delSongBtn = AttachHeaderButton(songGo, "✖", MenuData.Colors.CancelButton, songXPos, 30f, () =>
                            {
                                tier.RemoveSongAt(songIndex);
                                SaveAndRefresh();
                            });
                            AddButtonToNav(delSongBtn, navGroup);
                            songXPos -= 34f;

                            // Move down [▼]
                            if (songIndex < songs.Length - 1)
                            {
                                var downSongBtn = AttachHeaderButton(songGo, "▼", MenuData.Colors.NavigationBlue, songXPos, 30f, () =>
                                {
                                    tier.MoveSong(songIndex, songIndex + 1);
                                    SaveAndRefresh();
                                });
                                AddButtonToNav(downSongBtn, navGroup);
                                songXPos -= 34f;
                            }

                            // Move up [▲]
                            if (songIndex > 0)
                            {
                                var upSongBtn = AttachHeaderButton(songGo, "▲", MenuData.Colors.NavigationBlue, songXPos, 30f, () =>
                                {
                                    tier.MoveSong(songIndex, songIndex - 1);
                                    SaveAndRefresh();
                                });
                                AddButtonToNav(upSongBtn, navGroup);
                                songXPos -= 34f;
                            }

                            // Edit song button
                            var editSongBtn = AttachHeaderButton(songGo, "Edit", MenuData.Colors.NavigationYellow, songXPos, 50f, () =>
                            {
                                ShowEditSongDialog(tier, song, songIndex);
                            });
                            AddButtonToNav(editSongBtn, navGroup);
                        }
                    }
                }
            }
        }

        private void Save()
        {
            if (_presetRef != null && !_presetRef.DefaultPreset)
            {
                CustomContentManager.Careers.SavePresetFile(_presetRef);
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
                DialogManager.Instance.ShowRenameDialog(tier.Name, newName =>
                {
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    tier.Name = newName.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton($"Description: {(string.IsNullOrEmpty(tier.Description) ? "(None)" : tier.Description)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.Description ?? string.Empty, newDesc =>
                {
                    tier.Description = newDesc?.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton($"Unlock Type: {tier.UnlockType}", () =>
            {
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
            });

            dialog.AddListButton($"Unlock Criteria: {tier.UnlockCriteria}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.UnlockCriteria.ToString(), newCriteriaStr =>
                {
                    if (int.TryParse(newCriteriaStr, out int criteriaVal))
                    {
                        tier.UnlockCriteria = Mathf.Max(0, criteriaVal);
                        SaveAndRefresh();
                    }
                });
            });

            dialog.AddListButton($"Is Bonus Tier: {(tier.IsBonus ? "Yes" : "No")}", () =>
            {
                tier.IsBonus = !tier.IsBonus;
                SaveAndRefresh();
            });

            dialog.AddListButton($"Venue Size: {tier.VenueSize}", () =>
            {
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
            });

            dialog.AddListButton($"Venue Hint: {(string.IsNullOrEmpty(tier.VenueHint) ? "(None)" : tier.VenueHint)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.VenueHint ?? string.Empty, newHint =>
                {
                    tier.VenueHint = newHint?.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton($"Completion Mode: {tier.CompletionMode}", () =>
            {
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
            });

            dialog.AddListButton($"Custom Unlock Text: {(string.IsNullOrEmpty(tier.CustomUnlockText) ? "(None)" : tier.CustomUnlockText)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.CustomUnlockText ?? string.Empty, newText =>
                {
                    tier.CustomUnlockText = newText?.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton($"Completion Bonus: {tier.CompletionBonus}", () =>
            {
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
            });

            dialog.AddListButton($"Completion Bonus Text: {(string.IsNullOrEmpty(tier.CompletionBonusText) ? "(None)" : tier.CompletionBonusText)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.CompletionBonusText ?? string.Empty, newText =>
                {
                    tier.CompletionBonusText = newText?.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton($"Media Filename: {(string.IsNullOrEmpty(tier.MediaFilename) ? "(None)" : tier.MediaFilename)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(tier.MediaFilename ?? string.Empty, newMedia =>
                {
                    tier.MediaFilename = newMedia?.Trim();
                    SaveAndRefresh();
                });
            });
        }

        private async void ShowSongPickerDialog(CareerTier tier, string filterQuery = null)
        {
            if (SongContainer.Count == 0)
            {
                ToastManager.ToastError("No Songs in Library!");
                return;
            }

            SongEntry selected = null;
            var pickerDialog = DialogManager.Instance.ShowLibrarySearchDialog("Select a Song to Add", songEntry =>
            {
                selected = songEntry;
            });

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

            string currentSongName = song.SongEntry != null ? song.SongEntry.Name.Original : (!string.IsNullOrEmpty(song.Description) ? song.Description : (song.ShortName ?? song.SongTuple?.Title ?? "Unknown"));
            string currentArtist = song.SongEntry != null ? song.SongEntry.Artist.Original : (song.SongTuple?.Artist ?? "Unknown");

            dialog.AddListButton($"Song: {currentArtist} - {currentSongName}", null);
            dialog.AddListButton($"Current Identifier: {song.Identifier}", null);

            dialog.AddListButton("Change Identifier to: Song ID (GUID)", () =>
            {
                song.UpdateIdentifier(CareerSongIdentifier.SongId);
                SaveAndRefresh();
            });

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

            dialog.AddListButton("Change Identifier to: Short Name", () =>
            {
                song.UpdateIdentifier(CareerSongIdentifier.ShortName);
                SaveAndRefresh();
            });

            dialog.AddListButton($"Edit Fallback Description: {(string.IsNullOrEmpty(song.Description) ? "(None)" : song.Description)}", () =>
            {
                DialogManager.Instance.ShowRenameDialog(song.Description ?? string.Empty, newDesc =>
                {
                    song.Description = newDesc?.Trim();
                    SaveAndRefresh();
                });
            });

            dialog.AddListButton("Delete Song", () =>
            {
                tier.RemoveSongAt(songIndex);
                SaveAndRefresh();
            });
        }

        private static ColoredButton AttachHeaderButton(GameObject parent, string text, Color color, float xOffset, float width, UnityAction onClick)
        {
            var buttonGo = Object.Instantiate(GetSmallRoundButtonPrefab(), parent.transform);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 28f);
            rect.anchoredPosition = new Vector2(xOffset, 0f);

            var button = buttonGo.GetComponent<ColoredButton>();
            button.Text.text = text;
            button.SetBackgroundAndTextColor(color);
            button.OnClick.AddListener(onClick);
            return button;
        }

        private static void AddButtonToNav(ColoredButton button, NavigationGroup navGroup)
        {
            if (button == null || navGroup == null) return;
            var nav = button.GetComponent<NavigatableBehaviour>();
            if (nav != null)
            {
                navGroup.AddNavigatable(button.gameObject);
            }
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
