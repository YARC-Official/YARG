using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Replays;
using YARG.Scores;

namespace YARG.Menu.History
{
    public class HistoryMenu : ListMenu<ViewType, HistoryView>
    {
        private const string HISTORY_TAB          = "History";
        private const string IMPORTED_REPLAYS_TAB = "Import";

        private static readonly (string UnlocalizedName, DateTime MinTime)[] _categoryTimes =
        {
            ("Today",           DateTime.Today              ),
            ("Yesterday",       DateTime.Today.AddDays(-1)  ),
            ("ThisWeek",        DateTime.Today.AddDays(-7)  ),
            ("ThisMonth",       DateTime.Today.AddMonths(-1)),
            ("LastThreeMonths", DateTime.Today.AddMonths(-3)),
            ("ThisYear",        DateTime.Today.AddYears(-1) ),
            ("MoreThanYear",    DateTime.MinValue           ),
        };

        protected override int ExtraListViewPadding => 10;

        [SerializeField]
        private GameObject _exportReplayButton;
        [SerializeField]
        private GameObject _importReplayButton;

        [Space]
        [SerializeField]
        private HeaderTabs _headerTabs;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => CurrentSelection?.ViewClick()),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    Back),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.Common.Analyze",
                    () => CurrentSelection?.Shortcut1()),
            }, false));

            _headerTabs.TabChanged += OnTabChanged;
        }

        protected override List<ViewType> CreateViewList()
        {
            return _headerTabs.SelectedTabId switch
            {
                HISTORY_TAB          => CreateHistoryList(),
                IMPORTED_REPLAYS_TAB => CreateImportedList(),

                // Return an empty list when the tabs are loading
                null => new List<ViewType>(),
                _    => throw new Exception("Unreachable.")
            };
        }

        private List<ViewType> CreateHistoryList()
        {
            var list = new List<ViewType>();

            // Add the first category
            int categoryIndex = 0;
            list.Add(new CategoryViewType(LocalizeTime(_categoryTimes[0])));

            foreach (var record in ScoreContainer.GetAllGameRecords())
            {
                // See if we should create a category (make sure to skip the ones that have nothing in them)
                bool shouldCreateCategory = false;
                while (record.Date < _categoryTimes[categoryIndex].MinTime)
                {
                    categoryIndex++;
                    shouldCreateCategory = true;
                }

                // Create that category
                if (shouldCreateCategory)
                {
                    string text = LocalizeTime(_categoryTimes[categoryIndex]);
                    list.Add(new CategoryViewType(text));
                }

                list.Add(new GameRecordViewType(record));
            }

            return list;

            static string LocalizeTime((string, DateTime) input) =>
                Localize.Key("Menu.History.Time", input.Item1);
        }

        private List<ViewType> CreateImportedList()
        {
            var list = new List<ViewType>();

            foreach (var replay in ReplayContainer.Replays)
            {
                list.Add(new ReplayViewType(replay));
            }

            return list;
        }

        private void OnTabChanged(string tabId)
        {
            RequestViewListUpdate();
            SelectedIndex = 0;

            // Show the correct button
            _exportReplayButton.SetActive(tabId == HISTORY_TAB);
            _importReplayButton.SetActive(tabId == IMPORTED_REPLAYS_TAB);
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }

        public void ExportReplayButton()
        {
            if (CurrentSelection is not GameRecordViewType gameRecordViewType) return;

            var name = gameRecordViewType.GameRecord.ReplayFileName;
            var startPath = Path.Combine(ScoreContainer.ScoreReplayDirectory, name);

            // Check to see if the replay exists
            if (!File.Exists(startPath))
            {
                DialogManager.Instance.ShowMessage("Cannot Export Replay",
                    "The replay for this song does not exist. It has probably been deleted.");
                return;
            }

            // Ask the user for an ending location
            FileExplorerHelper.OpenSaveFile(null, Path.GetFileNameWithoutExtension(name), "replay", path =>
            {
                // Delete the file if it already exists
                if (File.Exists(path)) File.Delete(path);

                // Move the file
                File.Copy(startPath, path);
            });
        }

        public void ImportReplayButton()
        {
            // Ask the user for the replay location
            FileExplorerHelper.OpenChooseFile(null, "replay", path =>
            {
                // We need to check if the replay is valid before importing it

                var result = ReplayIO.ReadReplay(path, out var replay);

                if (result != ReplayReadResult.Valid)
                {
                    DialogManager.Instance.ShowMessage("Cannot Import Replay",
                        "The selected replay is most likely corrupted, or is not a valid replay file.");
                    return;
                }

                // Get the destination path and see if the replay already exists
                var name = Path.GetFileName(path);
                var dest = Path.Combine(ReplayContainer.ReplayDirectory, name);
                if (File.Exists(dest))
                {
                    DialogManager.Instance.ShowMessage("Cannot Import Replay",
                        "A replay with the same name already exists in the imported replays folder.");
                    return;
                }

                // If it's all good, copy it in!
                File.Copy(path, dest);

                // Add it to the replay container...
                var entry = ReplayEntry.CreateFromReplay(replay);
                ReplayContainer.AddReplayToCache(entry);

                // then refresh list (to show the replay)
                RequestViewListUpdate();
            });
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();

            _headerTabs.TabChanged -= OnTabChanged;
        }
    }
}
