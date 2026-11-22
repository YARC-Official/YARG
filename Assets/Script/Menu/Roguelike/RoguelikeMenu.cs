using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Roguelike;
using YARG.Settings;
using LibraryViewType = YARG.Menu.MusicLibrary.ViewType;
using LibrarySongView = YARG.Menu.MusicLibrary.SongView;

namespace YARG.Menu.Roguelike
{
    /// <summary>
    /// The roguelike run screen: start a run, pick one of the round's songs, see how the run is going.
    /// Uses the Quick Play (music library) layout, row prefab and sidebar; the prefab is built by the
    /// "YARG/Roguelike/Build Menu" editor command from the music library prefab.
    /// </summary>
    public class RoguelikeMenu : ListMenu<LibraryViewType, LibrarySongView>
    {
        private const float ABANDON_HOLD_SECONDS = 1f;

        protected override int ExtraListViewPadding => 15;

        [SerializeField]
        private TextMeshProUGUI _titleText;
        [SerializeField]
        private TextMeshProUGUI _subtitleText;
        [SerializeField]
        private Sidebar _sidebar;

        [Header("Sticky Header")]
        [SerializeField]
        private TextMeshProUGUI _headerPrimaryText;
        [SerializeField]
        private TextMeshProUGUI _headerSecondaryText;
        [SerializeField]
        private TextMeshProUGUI _headerSideText;

        // Same timing as Quick Play: the first preview starts straight away, later ones wait a moment
        // so scrolling past a song doesn't start it
        private const double PREVIEW_SCROLL_DELAY = 0.6;
        private const double PREVIEW_FADE_DURATION = 1.25;

        private readonly PreviewPlayer _previewPlayer = new();
        private SongEntry _previewSong;
        private double _previewDelay;

        private bool _hasScheme;
        private bool _runInProgressScheme;

        protected override void Awake()
        {
            base.Awake();

            if (_sidebar != null)
            {
                _sidebar.InitializeStandalone();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Mirrors Quick Play's "Music Library / Quickplay" header
            SetText(_titleText, Localize.Key("Menu.GameModes.Options.Name"));
            SetText(_subtitleText, Localize.Key("Menu.Roguelike.Name"));

            StemSettings.ApplySettings = SettingsManager.Settings.ApplyVolumesInMusicLibrary.Value;
            _previewDelay = 0;

            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            StopPreview();
            StemSettings.ApplySettings = true;

            if (_hasScheme)
            {
                Navigator.Instance.PopScheme();
                _hasScheme = false;
            }
        }

        private void OnDestroy()
        {
            _previewPlayer.Dispose();
        }

        protected override void OnSelectedIndexChanged()
        {
            base.OnSelectedIndexChanged();
            UpdateSidebar();
            UpdatePreview();
        }

        private void UpdateSidebar()
        {
            if (_sidebar != null)
            {
                // Song rows (choices and history) show their details; headers and buttons clear it
                _sidebar.ShowSong((CurrentSelection as SongViewType)?.SongEntry);
            }
        }

        private void UpdatePreview()
        {
            var song = (CurrentSelection as RoguelikeChoiceViewType)?.SongEntry;
            if (song != null && song == _previewSong)
            {
                // Still on the same song (e.g. the list was rebuilt), keep it playing
                return;
            }

            StopPreview();
            _previewSong = song;
            if (song == null)
            {
                return;
            }

            float volume = SettingsManager.Settings.PreviewVolume.Value;
            if (volume > 0)
            {
                _ = _previewPlayer.StartPreview(song, volume, GlobalVariables.State.SongSpeed, _previewDelay,
                    PREVIEW_FADE_DURATION, SettingsManager.Settings.CensorMatureContent.Value);
            }

            _previewDelay = PREVIEW_SCROLL_DELAY;
        }

        private void StopPreview()
        {
            _previewSong = null;
            _ = _previewPlayer.StopPreview();
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

            UpdateStickyHeader(run);

            // Land on the first thing that can be acted on (a song choice or a button)
            if (!SetIndexTo(view => view is RoguelikeChoiceViewType or RoguelikeActionViewType))
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
                new(MenuAction.Green, "Menu.Common.Confirm", () => CurrentSelection?.PrimaryButtonClick()),
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

        protected override List<LibraryViewType> CreateViewList()
        {
            var views = new List<LibraryViewType>();
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

        private void AddRunInProgress(List<LibraryViewType> views, RoguelikeRun run)
        {
            // The sticky header already shows the round, intensity and lives, so this row only adds
            // what it doesn't: how the last round went, and what's needed to pass this one
            var title = RoguelikeSession.LastOutcome switch
            {
                RoguelikeRoundOutcome.Advanced => Localize.Key("Menu.Roguelike.OutcomeAdvanced"),
                RoguelikeRoundOutcome.LifeLost => Localize.Key("Menu.Roguelike.OutcomeLifeLost"),
                _                              => Localize.Key("Menu.Roguelike.ChooseSong"),
            };

            views.Add(new RoguelikeHeaderViewType(title,
                Localize.KeyFormat("Menu.Roguelike.PassRequirement", run.Rules.MinStarsToPass)));

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

        private void AddStartScreen(List<LibraryViewType> views, RoguelikeRun lastRun)
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

        private static void AddHistory(List<LibraryViewType> views, RoguelikeRun run)
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

        // Public so the header's back button (a UnityEvent in the prefab) can call it
        public void Back()
        {
            // The run is kept while the game is open, so backing out and returning resumes it
            MenuManager.Instance.PopMenu();
        }

        /// <summary>Quick Play's sticky header shows the current sort; here it shows the run's status.</summary>
        private void UpdateStickyHeader(RoguelikeRun run)
        {
            if (run is { IsOver: false })
            {
                SetText(_headerPrimaryText, run.IsBossRound
                    ? Localize.KeyFormat("Menu.Roguelike.BossRound", run.Round + 1, run.Rules.RoundCount)
                    : Localize.KeyFormat("Menu.Roguelike.Round", run.Round + 1, run.Rules.RoundCount));
                SetText(_headerSecondaryText, Localize.KeyFormat("Menu.Roguelike.HeaderIntensity", run.TargetIntensity));
                SetText(_headerSideText, Localize.KeyFormat("Menu.Roguelike.HeaderLives", run.Lives));
            }
            else
            {
                SetText(_headerPrimaryText, Localize.Key("Menu.Roguelike.Name"));
                SetText(_headerSecondaryText, string.Empty);
                SetText(_headerSideText, string.Empty);
            }
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
