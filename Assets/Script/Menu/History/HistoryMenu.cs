using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Replays;
using YARG.Scores;

namespace YARG.Menu.History
{
    public class HistoryMenu : ListMenu<ViewType, HistoryView>
    {
        private const string HISTORY_TAB = "History";
        private const string IMPORTED_REPLAYS_TAB = "Import";

        private static readonly (string UnlocalizedName, DateTime MinTime)[] _categoryTimes =
        {
            ("Time.Today",           DateTime.Today              ),
            ("Time.Yesterday",       DateTime.Today.AddDays(-1)  ),
            ("Time.ThisWeek",        DateTime.Today.AddDays(-7)  ),
            ("Time.ThisMonth",       DateTime.Today.AddMonths(-1)),
            ("Time.LastThreeMonths", DateTime.Today.AddMonths(-3)),
            ("Time.ThisYear",        DateTime.Today.AddYears(-1) ),
            ("Time.MoreThanYear",    DateTime.MinValue           ),
        };

        protected override int ExtraListViewPadding => 5;

        [SerializeField]
        private HeaderTabs _headerTabs;

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Up",
                    () => SelectedIndex++),
                new NavigationScheme.Entry(MenuAction.Down, "Down",
                    () => SelectedIndex--),
                new NavigationScheme.Entry(MenuAction.Green, "Confirm",
                    () => CurrentSelection?.ViewClick()),

                new NavigationScheme.Entry(MenuAction.Red, "Back", Back),
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
                null                 => new List<ViewType>(),
                _                    => throw new Exception("Unreachable.")
            };
        }

        private List<ViewType> CreateHistoryList()
        {
            var list = new List<ViewType>();

            // Add the first category
            int categoryIndex = 0;
            list.Add(new CategoryViewType(
                LocaleHelper.LocalizeString(_categoryTimes[0].UnlocalizedName)));

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
                    string text = LocaleHelper.LocalizeString(_categoryTimes[categoryIndex].UnlocalizedName);
                    list.Add(new CategoryViewType(text));
                }

                list.Add(new GameRecordViewType(record));
            }

            return list;
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
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();

            _headerTabs.TabChanged -= OnTabChanged;
        }
    }
}