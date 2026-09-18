using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Career;
using YARG.Core.IO;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Scores;

namespace YARG.Menu.Career
{
    /// <summary>
    /// The tier and song list of a single career run.
    ///
    /// Progress is evaluated from the database once per change and cached here (see
    /// <see cref="Initialize"/> and <see cref="LoadProgress"/>): scrolling the list or rebuilding it
    /// never queries the database.
    /// </summary>
    public class CareerMenu : ListMenu<ViewType, SongView>
    {
        protected override int ExtraListViewPadding => 10;

        // The long press is the only guard on a reset - there is no confirmation dialog.
        private const float RESET_HOLD_SECONDS = 1f;

        private CareerBase _career;
        private CareerEvaluation _evaluation;
        private int _careerSaveId;

        // The menu scene is reloaded after a run, and MenuManager brings the career menu back without
        // going through the career list again, so the career being browsed has to outlive the instance.
        private static CareerBase _sessionCareer;

        // Progress can only change by completing a song, so the cached evaluation is reloaded when
        // that has happened, not on every enable.
        private bool _progressDirty;

        private CancellationTokenSource _backgroundCts;

        [SerializeField]
        private TextMeshProUGUI _careerNameText;
        [SerializeField]
        private TextMeshProUGUI _careerDescriptionText;
        [SerializeField]
        private RawImage _bgImage;

        protected override void OnEnable()
        {
            base.OnEnable();

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new ()
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
                    new NavigationScheme.Entry(MenuAction.Yellow, "Menu.Career.Reset",
                        () => { }, // a tap does nothing, only holding resets
                        holdSeconds: RESET_HOLD_SECONDS,
                        onHoldHandler: ResetCareer),
                    new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                        Back, hide: true),
                    }, false));

            if (_career is null)
            {
                if (_sessionCareer != null)
                {
                    // Fresh instance after a run: re-adopt the career of the session and re-read what
                    // that run changed. This is also the point where an unlock becomes "seen".
                    Initialize(_sessionCareer, LoadProgress(_sessionCareer));
                }

                return;
            }

            if (_progressDirty)
            {
                _progressDirty = false;

                // A song was started from this list, so whatever it produced is now in the database.
                ApplyProgress(LoadProgress(_career));
                RequestViewListUpdate();
                CheckUnlocks();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Navigator.Instance.PopScheme();

            // Stop caring about an in-flight decode; it frees the image itself and the texture it
            // produced gets destroyed instead of assigned.
            _backgroundCts?.Cancel();
            _backgroundCts?.Dispose();
            _backgroundCts = null;
        }

        protected override List<ViewType> CreateViewList()
        {
            var viewList = new List<ViewType>();

            if (_career == null)
            {
                // Not initialized yet, so return an empty list
                return viewList;
            }

            _evaluation ??= EvaluateFor(_career, _careerSaveId);

            // The run summary sits above the tiers and is not clickable.
            // viewList.Add(new CareerHeaderViewType(_career, _evaluation));

            for (var tierIndex = 0; tierIndex < _career.Tiers.Count; tierIndex++)
            {
                var tier = _career.Tiers[tierIndex];
                var result = _evaluation.Tiers[tierIndex];

                // Every tier reached so far stays in the list, completed ones included: replaying an
                // earlier song to raise its best stars is a legitimate way to meet an unlock.
                viewList.Add(new TierViewType(tier, result));

                if (!result.Unlocked)
                {
                    // Only the next locked tier is shown - as a summary header - and nothing beyond it.
                    break;
                }

                for (var songIndex = 0; songIndex < tier.Songs.Length; songIndex++)
                {
                    viewList.Add(new SongViewType(
                        tier.Songs[songIndex], _career.CareerId, tier.Id, tierIndex, songIndex,
                        songIndex < result.Songs.Count ? result.Songs[songIndex] : null,
                        OnSongPlayed));
                }
            }

            return viewList;
        }

        /// <summary>
        /// Enter a career with progress that has already been evaluated. The evaluation carries the
        /// save it was built from, so both are injected together and then cached.
        /// </summary>
        public void Initialize(CareerBase career, CareerEvaluation evaluation)
        {
            _career = career;
            _sessionCareer = career;
            ApplyProgress(evaluation);

            SetText(_careerNameText, career.Name);
            SetText(_careerDescriptionText, career.Description);

            _progressDirty = false;

            RequestViewListUpdate();
            StartBackgroundLoad(career);
            CheckUnlocks();
        }

        /// <summary>
        /// Resolve the save for the players who are seated right now and evaluate its progress. This
        /// is the career menu's only database read for progress.
        /// </summary>
        public static CareerEvaluation LoadProgress(CareerBase career)
        {
            return EvaluateFor(career, FindSaveIdForPlayers(career));
        }

        /// <summary>
        /// The active save for (career, seated human players), or 0 when there is none yet. No save is
        /// created here - starting a run stays free until something is completed.
        /// </summary>
        public static int FindSaveIdForPlayers(CareerBase career)
        {
            if (ScoreContainer.Careers is null)
            {
                YargLogger.LogError("Career: score database is not initialized, cannot resolve career save.");
                return 0;
            }

            var profiles = PlayerContainer.Players.Where(player => !player.SittingOut && !player.Profile.IsBot)
                                                 .Select(player => player.Profile.Id)
                                                 .ToList();
            if (profiles.Count == 0)
            {
                YargLogger.LogFormatError("Career: no seated players to attribute the career save to for career '{0}'.",
                    career?.Name);
                return 0;
            }

            var save = ScoreContainer.Careers.FindActiveSaveForProfiles(career.CareerId, profiles);
            return save?.Id ?? 0;
        }

        /// <summary>
        /// Evaluate a career against its save's rows. A save id of 0 means "no progress yet", which is
        /// answered without touching the database.
        /// </summary>
        public static CareerEvaluation EvaluateFor(CareerBase career, int careerSaveId)
        {
            var snapshot = careerSaveId > 0 && ScoreContainer.Careers is not null
                ? ScoreContainer.Careers.LoadProgressSnapshot(careerSaveId)
                : new CareerProgressSnapshot { CareerSaveId = careerSaveId };

            snapshot.CareerSaveId = careerSaveId;
            return CareerEvaluation.Evaluate(career, snapshot);
        }

        private void ApplyProgress(CareerEvaluation evaluation)
        {
            _evaluation = evaluation;
            _careerSaveId = evaluation?.CareerSaveId ?? 0;
        }

        private void OnSongPlayed()
        {
            _progressDirty = true;
        }

        /// <summary>
        /// Discard the current run. History rows are kept (see CareerDatabase.SoftDeleteSave), so the
        /// next run starts from scratch and the old one stays comparable.
        /// </summary>
        private void ResetCareer()
        {
            if (_career is null)
            {
                return;
            }

            if (_careerSaveId <= 0)
            {
                ToastManager.ToastInformation(Localize.Key("Menu.Career.NothingToReset"));
                return;
            }

            ScoreContainer.Careers?.SoftDeleteSave(_careerSaveId);

            ApplyProgress(EvaluateFor(_career, 0));
            RequestViewListUpdate();

            ToastManager.ToastSuccess(Localize.Key("Menu.Career.ResetDone"));
        }

        /// <summary>
        /// Present tiers that unlocked since the player last looked, and mark them seen while doing
        /// so - until this menu shows them the unlock has not been seen.
        /// </summary>
        private void CheckUnlocks()
        {
            if (_career is null || _careerSaveId <= 0)
            {
                return;
            }

            var careers = ScoreContainer.Careers;
            if (careers is null)
            {
                return;
            }

            // Tier 0 is unlocked from the start, so it never has an unlock to present.
            var fresh = careers.GetTierProgress(_careerSaveId)
                .Where(progress => progress.IsUnlocked && !progress.UnlockSeen && progress.TierIndex > 0)
                .OrderBy(progress => progress.TierIndex)
                .ToList();

            if (fresh.Count == 0)
            {
                return;
            }

            var names = fresh.Select(progress => TierName(progress.TierId)).ToList();
            var tier = _career.Tiers.FirstOrDefault(t => t.Id == fresh.First().TierId);

            foreach (var progress in fresh)
            {
                careers.MarkUnlockSeen(_careerSaveId, progress.TierId);
            }

            var text = tier?.CustomUnlockText ?? Localize.KeyFormat("Menu.Career.UnlockMessage", Localize.List(names));

            DialogManager.Instance.ShowMessage(Localize.Key("Menu.Career.UnlockTitle"), text);
        }

        private string TierName(Guid tierId)
        {
            var tier = _career.Tiers.FirstOrDefault(t => t.Id == tierId);
            return tier?.Name ?? Localize.Key("Menu.Career.UnknownTier");
        }

        /// <summary>
        /// Show the image named by the career's <c>bgImage</c> field, which the preset importer
        /// extracts next to the career JSON. Missing or unloadable art leaves the menu without a
        /// background.
        /// </summary>
        private void StartBackgroundLoad(CareerBase career)
        {
            _backgroundCts?.Cancel();
            _backgroundCts?.Dispose();
            _backgroundCts = null;

            SetBackground(null);

            string folder;
            if (career.DefaultPreset)
            {
                folder = Path.Combine(PathHelper.StreamingAssetsPath, "career", career.Id.ToString());
            }
            else
            {
                folder = career?.GetExtraContentFolder();
            }

            if (string.IsNullOrEmpty(folder) || string.IsNullOrWhiteSpace(career.BackgroundImageName))
            {
                // TODO: fall back to default career art once a default exists; until then no art is
                //  better than a blank stretched placeholder.
                return;
            }

            _backgroundCts = new CancellationTokenSource();

            var file = Path.Combine(folder, career.BackgroundImageName);
            if (!File.Exists(file))
            {
                YargLogger.LogFormatWarning<string, string, string>(
                    "Career: background image `{0}` for career '{1}' was not found in `{2}`.",
                    career.BackgroundImageName, career.Name, folder);
                return;
            }

            LoadBackground(career.CareerId, file, _backgroundCts.Token).Forget();
        }

        private async UniTask LoadBackground(Guid careerId, string file, CancellationToken token)
        {
            Texture2D texture;
            try
            {
                // Decoding goes through the native image library, so keep it off the main thread.
                using var image = await UniTask.RunOnThreadPool(() => YARGImage.Load(file));
                if (image is null)
                {
                    YargLogger.LogFormatWarning("Career: failed to load background image `{0}`.", file);
                    return;
                }

                texture = image.LoadTexture(true);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatWarning<string, string>(
                    "Career: could not decode background image `{0}`: {1}", file, e.Message);
                return;
            }

            // The rows, the career, or the whole menu may have changed while this was decoding.
            if (token.IsCancellationRequested || _career == null || _career.CareerId != careerId)
            {
                Destroy(texture);
                return;
            }

            SetBackground(texture);
        }

        private Texture2D _loadedBackground;

        private void SetBackground(Texture2D texture)
        {
            if (_bgImage == null)
            {
                return;
            }

            // Only textures this menu created are destroyed - the prefab's own reference is not ours
            // to free.
            if (_loadedBackground != null && _loadedBackground != texture)
            {
                Destroy(_loadedBackground);
            }

            _loadedBackground = texture;
            _bgImage.texture = texture;
            _bgImage.enabled = texture != null;

            if (texture != null)
            {
                // Decoded textures come out flipped.
                _bgImage.uvRect = new Rect(0, 0, 1, -1);
            }

            _bgImage.gameObject.SetActive(texture != null);
        }

        private static void SetText(TextMeshProUGUI target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }
    }
}
