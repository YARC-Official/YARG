using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.Career;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Roguelike;
using CareerViewType = YARG.Menu.Career.ViewType;

namespace YARG.Menu.Roguelike
{
    /// <summary>
    /// The roguelike run screen: start a run, pick one of the round's songs, see how the run is going.
    /// Uses the career menu's layout and row prefab; the prefab is built by the
    /// "YARG/Roguelike/Build Menu" editor command from the career menu prefab.
    /// </summary>
    public class RoguelikeMenu : ListMenu<CareerViewType, SongView>
    {
        private const float ABANDON_HOLD_SECONDS = 1f;

        protected override int ExtraListViewPadding => 16;

        [SerializeField]
        private TextMeshProUGUI _titleText;
        [SerializeField]
        private TextMeshProUGUI _subtitleText;

        private bool _hasScheme;
        private bool _runInProgressScheme;

        protected override void OnEnable()
        {
            base.OnEnable();

            SetText(_titleText, Localize.Key("Menu.Roguelike.Name"));
            SetText(_subtitleText, Localize.Key("Menu.Roguelike.Description"));

            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_hasScheme)
            {
                Navigator.Instance.PopScheme();
                _hasScheme = false;
            }
        }

        private void Refresh()
        {
            var run = RoguelikeSession.Current;
            if (run is { IsOver: false })
            {
                RoguelikeLibrary.EnsureChoices(run);
            }

            SetMenuNavigation(run is { IsOver: false });
            RequestViewListUpdate();

            // Land on the first thing that can be acted on (a song choice or a button)
            if (!SetIndexTo(view => view.IsClickable))
            {
                SelectedIndex = 0;
            }
        }

        private void SetMenuNavigation(bool runInProgress)
        {
            // The Abandon button only exists while a run is in progress, so the scheme is swapped
            // when that changes
            if (_hasScheme && _runInProgressScheme == runInProgress)
            {
                return;
            }

            if (_hasScheme)
            {
                Navigator.Instance.PopScheme();
            }

            var entries = new List<NavigationScheme.Entry>
            {
                new(MenuAction.Up, "Menu.Common.Up", ctx =>
                {
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex--;
                }),
                new(MenuAction.Down, "Menu.Common.Down", ctx =>
                {
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex++;
                }),
                new(MenuAction.Green, "Menu.Common.Confirm", () =>
                {
                    if (CurrentSelection is { IsClickable: true } selection)
                    {
                        selection.ViewClick();
                    }
                }),
                new(MenuAction.Red, "Menu.Common.Back", Back, hide: true),
            };

            if (runInProgress)
            {
                entries.Add(new NavigationScheme.Entry(MenuAction.Yellow, "Menu.Roguelike.Abandon",
                    () => { }, // a tap does nothing, only holding abandons
                    holdSeconds: ABANDON_HOLD_SECONDS,
                    onHoldHandler: AbandonRun));
            }

            _ = Navigator.Instance.PushScheme(new NavigationScheme(entries, false));
            _hasScheme = true;
            _runInProgressScheme = runInProgress;
        }

        protected override List<CareerViewType> CreateViewList()
        {
            var views = new List<CareerViewType>();
            var run = RoguelikeSession.Current;

            if (run is { IsOver: false })
            {
                AddRunInProgress(views, run);
            }
            else
            {
                AddStartScreen(views, run);
            }

            if (run is { History: { Count: > 0 } })
            {
                AddHistory(views, run);
            }

            return views;
        }

        private void AddRunInProgress(List<CareerViewType> views, RoguelikeRun run)
        {
            var title = run.IsBossRound
                ? Localize.KeyFormat("Menu.Roguelike.BossRound", run.Round + 1, run.Rules.RoundCount)
                : Localize.KeyFormat("Menu.Roguelike.Round", run.Round + 1, run.Rules.RoundCount);

            var detail = Localize.KeyFormat("Menu.Roguelike.RoundDetail", run.TargetIntensity, run.Lives,
                run.Rules.MinStarsToPass);

            var outcomeNote = RoguelikeSession.LastOutcome switch
            {
                RoguelikeRoundOutcome.Advanced => Localize.Key("Menu.Roguelike.OutcomeAdvanced"),
                RoguelikeRoundOutcome.LifeLost => Localize.Key("Menu.Roguelike.OutcomeLifeLost"),
                _                              => null,
            };
            if (outcomeNote != null)
            {
                detail = outcomeNote + "  " + detail;
            }

            views.Add(new RoguelikeHeaderViewType(title, detail));

            int offered = 0;
            foreach (var hash in run.Choices)
            {
                var song = RoguelikeLibrary.Resolve(hash);
                if (song is null)
                {
                    continue;
                }

                views.Add(new RoguelikeChoiceViewType(song, RoguelikeLibrary.GetRating(run, song), PlaySong));
                offered++;
            }

            if (offered == 0)
            {
                views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.NoSongs"),
                    Localize.Key("Menu.Roguelike.NoSongsDetail"), BaseViewType.BackgroundType.SecondaryHeader));
                views.Add(new RoguelikeActionViewType(Localize.Key("Menu.Roguelike.AbandonNow"), null, AbandonRun));
            }
        }

        private void AddStartScreen(List<CareerViewType> views, RoguelikeRun lastRun)
        {
            if (lastRun is { State: RoguelikeRunState.Won })
            {
                views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.RunWon"),
                    Localize.KeyFormat("Menu.Roguelike.RunWonDetail", lastRun.Rules.RoundCount)));
            }
            else if (lastRun is { State: RoguelikeRunState.Lost })
            {
                views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.RunLost"),
                    Localize.KeyFormat("Menu.Roguelike.RunLostDetail", lastRun.Round + 1, lastRun.Rules.RoundCount)));
            }

            var player = FindRunPlayer();
            if (player is null)
            {
                views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.NoPlayer"),
                    Localize.Key("Menu.Roguelike.NoPlayerDetail")));
                return;
            }

            var gameMode = player.Profile.GameMode;
            var preferred = GetPreferredInstrument(player);

            views.Add(new RoguelikeActionViewType(Localize.Key("Menu.Roguelike.StartRun"),
                Localize.KeyFormat("Menu.Roguelike.StartRunDetail", player.Profile.Name, preferred.ToLocalizedName()),
                () => StartRun(player, gameMode, preferred)));

            var shortTiers = FindShortTiers(gameMode, preferred, new RoguelikeRules());
            if (shortTiers.Count > 0)
            {
                views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.ShortTiers"),
                    Localize.KeyFormat("Menu.Roguelike.ShortTiersDetail", string.Join(", ", shortTiers)),
                    BaseViewType.BackgroundType.SecondaryHeader));
            }
        }

        private static void AddHistory(List<CareerViewType> views, RoguelikeRun run)
        {
            views.Add(new RoguelikeHeaderViewType(Localize.Key("Menu.Roguelike.History"), null,
                BaseViewType.BackgroundType.SecondaryHeader));

            // Most recent first
            for (int i = run.History.Count - 1; i >= 0; i--)
            {
                var result = run.History[i];
                views.Add(new RoguelikeHistoryViewType(result, RoguelikeLibrary.Resolve(result.SongHash)));
            }
        }

        /// <summary>A run belongs to one player: the first human player in the player list.</summary>
        private static YargPlayer FindRunPlayer()
        {
            return PlayerContainer.Players.FirstOrDefault(player => !player.Profile.IsBot);
        }

        private static Instrument GetPreferredInstrument(YargPlayer player)
        {
            var profile = player.Profile;
            var possible = profile.GameMode.PossibleInstruments();
            if (possible.Contains(profile.PreferredInstrument))
            {
                return profile.PreferredInstrument;
            }

            return possible.Length > 0 ? possible[0] : profile.PreferredInstrument;
        }

        /// <summary>Tiers with fewer songs than a round offers, which will borrow from nearby tiers.</summary>
        private static List<int> FindShortTiers(GameMode gameMode, Instrument preferred, RoguelikeRules rules)
        {
            var counts = RoguelikeLibrary.CountSongsPerTier(gameMode, preferred);
            var shortTiers = new List<int>();
            for (int tier = rules.MinIntensity; tier <= rules.MaxIntensity; tier++)
            {
                counts.TryGetValue(tier, out int count);
                if (count < rules.ChoicesPerRound)
                {
                    shortTiers.Add(tier);
                }
            }
            return shortTiers;
        }

        private void StartRun(YargPlayer player, GameMode gameMode, Instrument preferred)
        {
            int seed = Guid.NewGuid().GetHashCode();
            RoguelikeSession.Start(new RoguelikeRun(gameMode, preferred, seed), player.Profile.Id);
            Refresh();
        }

        private void PlaySong(SongEntry song)
        {
            try
            {
                RoguelikeSession.BeginSong(song);
            }
            catch (InvalidOperationException e)
            {
                ToastManager.ToastError(e.Message);
                Refresh();
                return;
            }

            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }

        private void AbandonRun()
        {
            var run = RoguelikeSession.Current;
            if (run is null || run.IsOver)
            {
                return;
            }

            run.Forfeit();
            Refresh();
        }

        private void Back()
        {
            // The run is kept while the game is open, so backing out and returning resumes it
            MenuManager.Instance.PopMenu();
        }

        private static void SetText(TextMeshProUGUI target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
