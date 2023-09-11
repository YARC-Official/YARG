﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Replays;

namespace YARG.Menu.Replays
{
    public class ReplaysMenu : MonoBehaviour
    {
        private const int REPLAY_VIEW_EXTRA = 5;
        private const float SCROLL_TIME = 1f / 60f;

        private static IReadOnlyList<ReplayEntry> Replays => ReplayContainer.Replays;

        [SerializeField]
        private Transform _replayContainer;
        [SerializeField]
        private Scrollbar _scrollbar;

        [Space]
        [SerializeField]
        private GameObject _replayViewPrefab;

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            private set
            {
                // Properly wrap the value
                if (value < 0)
                {
                    _selectedIndex = Replays.Count - 1;
                }
                else if (value >= Replays.Count)
                {
                    _selectedIndex = 0;
                }
                else
                {
                    _selectedIndex = value;
                }

                UpdateScrollbar();
                UpdateReplayViews();
            }
        }

        private readonly List<ReplayView> _replayViews = new();

        /// <summary>
        /// The <see cref="ReplayView"/> that is currently being selected.
        /// </summary>
        // The selected replay view is always at index `REPLAY_VIEW_EXTRA`
        public ReplayView CurrentReplay => _selectedIndex >= 0 && _selectedIndex < _replayViews.Count
            ? _replayViews[REPLAY_VIEW_EXTRA] : null;

        private float _scrollTimer;

        private void Awake()
        {
            // Create all of the replay views
            for (int i = 0; i < REPLAY_VIEW_EXTRA * 2 + 1; i++)
            {
                var gameObject = Instantiate(_replayViewPrefab, _replayContainer);

                // Add
                var replayView = gameObject.GetComponent<ReplayView>();
                _replayViews.Add(replayView);
            }
        }

        private void OnEnable()
        {
            UpdateReplayViews();

            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Select", () => CurrentReplay?.Confirm()),
                new NavigationScheme.Entry(MenuAction.Red, "Back", () => MenuManager.Instance.PopMenu()),
                new NavigationScheme.Entry(MenuAction.Select, "Open Folder", () => ReplayContainer.OpenReplayDirectory()),
                new NavigationScheme.Entry(MenuAction.Up, "Up", () => SelectedIndex--),
                new NavigationScheme.Entry(MenuAction.Down, "Down", () => SelectedIndex++),
            }, true));
        }

        private void UpdateReplayViews()
        {
            for (int i = 0; i < _replayViews.Count; i++)
            {
                // Hide if it's not in range
                int relativeIndex = i - REPLAY_VIEW_EXTRA;
                int realIndex = SelectedIndex + relativeIndex;
                if (realIndex < 0 || realIndex >= Replays.Count)
                {
                    _replayViews[i].Hide();
                    continue;
                }

                // Otherwise, show as a replay
                _replayViews[i].ShowAsReplay(relativeIndex == 0, Replays[realIndex]);
            }
        }

        private void Update()
        {
            if (_scrollTimer > 0f)
            {
                _scrollTimer -= Time.deltaTime;
                return;
            }

            var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

            if (delta > 0f)
            {
                SelectedIndex--;
                _scrollTimer = SCROLL_TIME;
                return;
            }

            if (delta < 0f)
            {
                SelectedIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }

        public void OnScrollBarChange()
        {
            SelectedIndex = Mathf.FloorToInt(_scrollbar.value * (Replays.Count - 1));
        }

        private void UpdateScrollbar()
        {
            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / Replays.Count);
        }
    }
}