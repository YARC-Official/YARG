using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.Dialogs
{
    /// <summary>
    /// Play A Show dialog, note that it works with only exactly five entries and eventually times out on its own
    /// </summary>
    public class SongPickerListDialog : Dialog
    {
        [FormerlySerializedAs("_showCategories")]
        [SerializeField]
        private ShowCategoryView[] _showCategoryViews;
        [SerializeField]
        private ShowPickerButton _selectButton;
        [SerializeField]
        private Slider _countdownSlider;

        private ShowCategories.ShowCategory[] _categories;
        private ShowCategories                _showCategoriesProvider;
        private List<YargPlayer>              _players;
        private List<YargPlayer>              _votedPlayers;
        private int                           _voteCount;
        private int[]                         _votes;
        public  MusicLibraryMenu              MusicLibrary;

        private Sequence _timerSequence;

        public void Initialize(MusicLibraryMenu musicLibrary)
        {
            MusicLibrary = musicLibrary;
            _showCategoriesProvider = new ShowCategories(musicLibrary);
            _categories = _showCategoriesProvider.GetCategories();
            _players = PlayerContainer.Players.Where(e => !e.Profile.IsBot).ToList();
            _votedPlayers = new List<YargPlayer>();
            _votes = new int[_categories.Length];

            _selectButton.ButtonText.text = Localize.Key("Menu.Dialog.ShowDialog.Select");
            Title.text = Localize.Key("Menu.Dialog.ShowDialog.Title");

            for (int i = 0; i < _showCategoryViews.Length; i++)
            {
                _showCategoryViews[i].CategoryText.text = _categories[i].CategoryText;
            }

            // _timeoutTween = DOVirtual.DelayedCall(10.0f, TallyVotes, true);
            _timerSequence = DOTween.Sequence(_countdownSlider).SetAutoKill(false);
            _timerSequence.Append(_countdownSlider.DOValue(1.0f, 0.01f)).
                Join(_countdownSlider.DOValue(0.0f, 10.0f)).
                AppendCallback(TallyVotes);
        }

        protected override NavigationScheme GetNavigationScheme()
        {
            // Need an empty scheme here, because we don't want the navigation logic
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Select, "That's Enough!", OnSelectButton),
                new NavigationScheme.Entry(MenuAction.Green, "", ChooseSongAction),
                new NavigationScheme.Entry(MenuAction.Red, "", ChooseSongAction),
                new NavigationScheme.Entry(MenuAction.Yellow, "", ChooseSongAction),
                new NavigationScheme.Entry(MenuAction.Blue, "", ChooseSongAction),
                new NavigationScheme.Entry(MenuAction.Orange, "", ChooseSongAction),
            }, true, true);
        }

        private void ChooseSongAction(NavigationContext ctx)
        {
            // Figure out which player and which button
            var player = ctx.Player;
            var button = ctx.Action;

            switch (button)
            {
                case MenuAction.Green:
                    AddVote(player, 0);
                    break;
                case MenuAction.Red:
                    AddVote(player, 1);
                    break;
                case MenuAction.Yellow:
                    AddVote(player, 2);
                    break;
                case MenuAction.Blue:
                    AddVote(player, 3);
                    break;
                case MenuAction.Orange:
                    AddVote(player, 4);
                    break;
            }
        }

        private void AddVote(YargPlayer player, int vote)
        {
            if (_votedPlayers.Contains(player))
            {
                return;
            }

            _votedPlayers.Add(player);
            _votes[vote]++;

            if (_votedPlayers.Count == _players.Count)
            {
                // All players have voted
                _timerSequence.Pause();
                TallyVotes();
            }
        }

        private void TallyVotes()
        {
            // If no players voted, just give a new set of choices
            if (_votedPlayers.Count == 0)
            {
                _votedPlayers.Clear();
                Array.Clear(_votes, 0, _votes.Length);
                Refresh();
                return;
            }

            // Find the highest vote total and deal with the case where there is a tie
            int highestVote = -1;
            List<int> highestVotes = new();
            for (int i = 0; i < _votes.Length; i++)
            {
                if (_votes[i] > highestVote)
                {
                    highestVote = _votes[i];
                    highestVotes.Clear();
                    highestVotes.Add(i);
                }
                else if (_votes[i] == highestVote)
                {
                    highestVotes.Add(i);
                }
            }

            int selectedIndex;
            if (highestVotes.Count > 1)
            {
                // Randomly select one of the highest votes
                int randomIndex = UnityEngine.Random.Range(0, highestVotes.Count);
                selectedIndex = highestVotes[randomIndex];
            }
            else
            {
                selectedIndex = highestVotes[0];
            }

            _votedPlayers.Clear();
            Array.Clear(_votes, 0, _votes.Length);
            MusicLibrary.ShowPlaylist.AddSong(_categories[selectedIndex].Song);
            Refresh();
        }

        private void CloseDialog(NavigationContext ctx)
        {
            // Close the dialog
            DialogManager.Instance.SubmitAndClearDialog();
        }

        private void Refresh()
        {
            // Minimize the chance of a race condition
            if (_timerSequence.IsPlaying())
            {
                _timerSequence.Pause();
            }

            MusicLibrary.RefreshAndReselect();
            _showCategoriesProvider.Refresh();
            _categories = _showCategoriesProvider.GetCategories();
            for (int i = 0; i < _showCategoryViews.Length; i++)
            {
                _showCategoryViews[i].CategoryText.text = _categories[i].CategoryText;
            }

            _timerSequence.Restart();
        }


        // The On[Color]Button methods are used only for mouse clicks. Since clicks aren't associated
        // with a player, we short circuit the vote logic and add the song directly.
        public void OnGreenButton()
        {
            MusicLibrary.ShowPlaylist.AddSong(_categories[0].Song);
            Refresh();
        }

        public void OnRedButton()
        {
            MusicLibrary.ShowPlaylist.AddSong(_categories[1].Song);
            Refresh();
        }

        public void OnYellowButton()
        {
            MusicLibrary.ShowPlaylist.AddSong(_categories[2].Song);
            Refresh();
        }

        public void OnBlueButton()
        {
            MusicLibrary.ShowPlaylist.AddSong(_categories[3].Song);
            Refresh();
        }

        public void OnOrangeButton()
        {
            MusicLibrary.ShowPlaylist.AddSong(_categories[4].Song);
            Refresh();
        }

        public void OnSelectButton()
        {
            DialogManager.Instance.SubmitAndClearDialog();
        }

        public override void Submit()
        {
            _timerSequence.Kill();
            MusicLibrary.RefreshAndReselect();
            DialogManager.Instance.ClearDialog();
        }

        public override ColoredButton AddDialogButton(string localizeKey, Color backgroundColor, UnityAction action)
        {
            // We're living dangerously here, because we don't want to add any buttons to this dialog
            return null;
        }
    }
}