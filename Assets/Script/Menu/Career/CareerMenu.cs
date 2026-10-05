using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.Audio;
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
    public class CareerMenu : ListMenu<ViewType, SongView>
    {
        protected override int ExtraListViewPadding => 16;

        private const float RESET_HOLD_SECONDS = 1f;

        private CareerBase _career;
        private CareerEvaluation _evaluation;
        private int _careerSaveId;

        private static CareerBase _sessionCareer;
        private bool _progressDirty;

        private CancellationTokenSource _backgroundCts;

        [SerializeField]
        private TextMeshProUGUI _careerNameText;
        [SerializeField]
        private TextMeshProUGUI _careerDescriptionText;
        [SerializeField]
        private RawImage _bgImage;
        [SerializeField]
        private RawImage _videoTexture;

        private VideoPlayer   _videoPlayer;
        private VideoPlayerSampleConsumer _videoPlayerConsumer;
        private RenderTexture _renderTex;

        private bool _videoPlaying;
        private bool _videoPreparePending;

        private UniTaskCompletionSource<bool> _videoStopped;

        protected override void OnEnable()
        {
            base.OnEnable();

            SetMenuNavigation();

            if (_career is null)
            {
                if (_sessionCareer != null)
                {
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

            StopVideo();

            Navigator.Instance.PopScheme();

            _backgroundCts?.Cancel();
            _backgroundCts?.Dispose();
            _backgroundCts = null;
        }

        private void SetMenuNavigation(bool pop = false)
        {
            if (pop)
            {
                Navigator.Instance.PopScheme();
            }

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
        }

        private void SetVideoNavigation()
        {
            Navigator.Instance.PopScheme();
            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Skip", StopVideo)
            }, false));
        }

        protected override List<ViewType> CreateViewList()
        {
            var viewList = new List<ViewType>();

            if (_career == null)
            {
                // Not initialized yet, so return an empty list
                return viewList;
            }

            _evaluation ??= Evaluate(_career, _careerSaveId);

            for (var tierIndex = 0; tierIndex < _career.Tiers.Count; tierIndex++)
            {
                var tier = _career.Tiers[tierIndex];
                var result = _evaluation.Tiers[tierIndex];

                if (result.Unlocked)
                {
                    viewList.Add(new TierViewType(tier, result));
                }
                else
                {
                    // Don't show bonus tier unless there are no more non-bonus tiers to unlock
                    // TODO: This doesn't actually do what it says in the case where there are multiple bonus tiers
                    //  after the last non-bonus tier
                    if (tier.IsBonus && tierIndex < _career.Tiers.Count - 1)
                    {
                        continue;
                    }

                    viewList.Add(new TierViewType(tier, result));
                    break;
                }

                for (var songIndex = 0; songIndex < tier.Songs.Length; songIndex++)
                {
                    viewList.Add(new SongViewType(
                        tier.Songs[songIndex], _career.Id, tier.Id, tierIndex, songIndex,
                        songIndex < result.Songs.Count ? result.Songs[songIndex] : null,
                        OnSongPlayed));
                }
            }

            return viewList;
        }

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

        public static CareerEvaluation LoadProgress(CareerBase career)
        {
            return Evaluate(career, FindSaveId(career));
        }

        public static int FindSaveId(CareerBase career)
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
                YargLogger.LogFormatError("Career: No active players for career '{0}'.",
                    career?.Name);
                return 0;
            }

            var save = ScoreContainer.Careers.FindActiveSaveForProfiles(career.Id, profiles);
            return save?.Id ?? 0;
        }

        public static CareerEvaluation Evaluate(CareerBase career, int careerSaveId)
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

            ApplyProgress(Evaluate(_career, 0));
            RequestViewListUpdate();

            ToastManager.ToastSuccess(Localize.Key("Menu.Career.ResetDone"));
        }

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

            _ = HandleUnlockDisplay(names, tier);
        }

        private string TierName(Guid tierId)
        {
            var tier = _career.Tiers.FirstOrDefault(t => t.Id == tierId);
            return tier?.Name ?? Localize.Key("Menu.Career.UnknownTier");
        }

        private async UniTaskVoid HandleUnlockDisplay(List<string> names, CareerTier tier)
        {
            var folder = GetCareerContentFolder(_career);
            var mediaFilename = tier.MediaFilename?.Name;
            var text = tier?.CustomUnlockText ?? Localize.KeyFormat("Menu.Career.UnlockMessage", Localize.List(names));

            if (tier.CompletionBonus == CompletionBonusType.Image)
            {
                if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(mediaFilename))
                {
                    var path = Path.Combine(folder, mediaFilename);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        await DialogManager.Instance.ShowImageDialog(Localize.Key("Menu.Career.UnlockTitle"), text, path).WaitUntilClosed();
                        return;
                    }
                }
            }

            if (tier.CompletionBonus == CompletionBonusType.Video)
            {
                if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(mediaFilename))
                {
                    var file = Path.Combine(folder, mediaFilename);
                    if (File.Exists(file))
                    {
                        await PlayVideo(file);
                    }
                }
            }

            DialogManager.Instance.ShowMessage(Localize.Key("Menu.Career.UnlockTitle"), text);
        }

        private void StartBackgroundLoad(CareerBase career)
        {
            _backgroundCts?.Cancel();
            _backgroundCts?.Dispose();
            _backgroundCts = null;

            SetBackground(null);

            var folder = GetCareerContentFolder(career);
            var backgroundImageName = career.BackgroundImageName?.Name;

            if (string.IsNullOrEmpty(folder) || string.IsNullOrWhiteSpace(backgroundImageName))
            {
                // TODO: fall back to default career art once a default exists; until then no art is
                //  better than a blank stretched placeholder.
                return;
            }

            _backgroundCts = new CancellationTokenSource();

            var file = Path.Combine(folder, backgroundImageName);
            if (!File.Exists(file))
            {
                YargLogger.LogFormatWarning<string, string, string>(
                    "Career: background image `{0}` for career '{1}' was not found in `{2}`.",
                    backgroundImageName, career.Name, folder);
                return;
            }

            LoadBackground(career.Id, file, _backgroundCts.Token).Forget();
        }

        private static string GetCareerContentFolder(CareerBase career)
        {
            if (career == null)
            {
                return null;
            }

            return career.DefaultPreset
                ? Path.Combine(PathHelper.StreamingAssetsPath, "career", career.Id.ToString())
                : career.GetExtraContentFolder();
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
            if (token.IsCancellationRequested || _career == null || _career.Id != careerId)
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

        private UniTask<bool> PlayVideo(string path)
        {
            // TODO: This also needs to reset the navigation scheme such that the only button is green, labeled skip

            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.targetTexture = _videoTexture.texture as RenderTexture;
                _videoPlayer.audioOutputMode = VideoAudioOutputMode.APIOnly;
                _videoPlayer.prepareCompleted += OnVideoPrepared;
                _videoPlayer.loopPointReached += OnVideoEnd;
            }

            if (!_videoPlaying)
            {
                _videoPlayer.url = path;
                _videoPreparePending = true;
                _videoPlayer.Prepare();
                _videoPlaying = true;
            }

            _videoStopped = new UniTaskCompletionSource<bool>();

            _videoPlayer.loopPointReached += (VideoPlayer vp) => _videoStopped.TrySetResult(true);

            SetVideoNavigation();

            return _videoStopped.Task;
        }

        private void OnVideoPrepared(VideoPlayer player)
        {
            if (!_videoPreparePending)
            {
                return;
            }

            _videoPreparePending = false;

            if (_renderTex == null)
            {
                _renderTex = new RenderTexture((int) player.width, (int) player.height, 0, RenderTextureFormat.ARGB32);
                _videoPlayer.targetTexture = _renderTex;
                _videoTexture.texture = _renderTex;
            }

            _videoPlayerConsumer?.Dispose();
            _videoPlayerConsumer = new VideoPlayerSampleConsumer(player);
            _videoPlayerConsumer.Initialize();

            _videoPlayer.Play();
            _videoTexture.gameObject.SetActive(true);
        }

        private void OnVideoEnd(VideoPlayer player)
        {
            StopVideo();
        }

        private void StopVideo()
        {
            _videoPreparePending = false;
            _videoPlayerConsumer?.Dispose();
            _videoPlayerConsumer = null;

            if (_videoPlayer != null)
            {
                _videoPlayer.Stop();
                _videoTexture.gameObject.SetActive(false);
                _videoPlaying = false;
            }

            if (_renderTex != null)
            {
                _renderTex.Release();
                _renderTex = null;
                _videoTexture.texture = null;
            }

            SetMenuNavigation(true);
            _videoStopped?.TrySetResult(false);
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
            if (_videoPlaying)
            {
                StopVideo();
                return;
            }

            MenuManager.Instance.PopMenu();
        }
    }
}