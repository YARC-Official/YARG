using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI.PlayResultScreen
{
    public class PlayResultScreen : MonoBehaviour
    {
        private readonly Color PASS = new(.18f, .85f, 1f);
        private readonly Color FAIL = new(.95f, .17f, .22f);

        [SerializeField]
        private GameObject playerCardPrefab;

        [Space]
        [SerializeField]
        private bool hasFailed;

        [SerializeField]
        private Image backgroundBorderPass;

        [SerializeField]
        private Image backgroundBorderFail;

        [SerializeField]
        private RawImage headerBackgroundPassed;

        [Space]
        [SerializeField]
        private CanvasGroup songInfoCG;

        [SerializeField]
        private TextMeshProUGUI songTitle;

        [SerializeField]
        private TextMeshProUGUI songArtist;

        [SerializeField]
        private CanvasGroup starScoreCG;

        [SerializeField]
        private StarDisplay starDisplay;

        [SerializeField]
        private TextMeshProUGUI score;

        [SerializeField]
        private RectTransform marginContainerRT;

        [Space]
        [SerializeField]
        private GameObject playerCardsContainer;

        [SerializeField]
        private TextMeshProUGUI scoreWIPNotice;

        private List<PlayerCard> playerCards = new();

        public HashSet<PlayerManager.Player> highScores;
        public HashSet<PlayerManager.Player> disqualified;
        public HashSet<PlayerManager.Player> bot;

        public static event Action<bool> OnEnabled;
        
        void OnEnable()
        {
            // Populate header information
            string name;
            if (Play.speed == 1f)
            {
                name = GameManager.Instance?.SelectedSong.Name;
            }
            else
            {
                name = $"{GameManager.Instance.SelectedSong.Name} <size=50%>({Play.speed * 100}% speed)";
            }

            songTitle.SetText(name);
            songArtist.SetText(GameManager.Instance?.SelectedSong?.Artist);
            score.SetText(ScoreKeeper.TotalScore.ToString("n0"));

            int stars = (int) StarScoreKeeper.BandStars;
            starDisplay.SetStars(stars, stars <= 5 ? StarType.Standard : StarType.Gold);

            // change graphics depending on clear/fail
            backgroundBorderFail.gameObject.SetActive(hasFailed);
            backgroundBorderPass.gameObject.SetActive(!hasFailed);
            headerBackgroundPassed.gameObject.SetActive(!hasFailed);
            songArtist.color = hasFailed ? FAIL : PASS;

            ProcessScores();
            CreatePlayerCards();

            StartCoroutine(EnableAnimation());

            OnEnabled?.Invoke(true);
        }

        /// <summary>
        /// Populate relevant score data; save scores.
        /// </summary>
        private void ProcessScores()
        {
            // Create a score to push
            var songScore = new SongScore
            {
                lastPlayed = DateTime.Now, timesPlayed = 1, highestPercent = new(), highestScore = new()
            };
            var oldScore = ScoreManager.GetScore(GameManager.Instance?.SelectedSong);

            highScores = new();
            disqualified = new();
            bot = new();
            foreach (var player in PlayerManager.players)
            {
                // Skip "Sit Out"s
                if (player.chosenInstrument == null)
                {
                    continue;
                }

                // Bots
                if (player.inputStrategy.BotMode)
                {
                    bot.Add(player);
                    continue;
                }

                // DQ speeds below 100%
                if (Play.speed < 1f)
                {
                    disqualified.Add(player);
                    continue;
                }

                // DQ no scores
                if (!player.lastScore.HasValue)
                {
                    disqualified.Add(player);
                    continue;
                }

                var lastScore = player.lastScore.GetValueOrDefault();

                // Skip if the chart has no notes
                if (lastScore.notesHit + lastScore.notesMissed == 0)
                {
                    disqualified.Add(player);
                    continue;
                }

                // Override or add score/percentage
                // TODO: override scores/percentages independently
                if (oldScore == null || oldScore.highestScore == null ||
                    !oldScore.highestScore.TryGetValue(player.chosenInstrument, out var oldHighestSc) ||
                    lastScore.score > oldHighestSc)
                {
                    songScore.highestPercent[player.chosenInstrument] = lastScore.percentage;
                    songScore.highestScore[player.chosenInstrument] = lastScore.score;
                    highScores.Add(player);
                }
            }

            ScoreManager.PushScore(GameManager.Instance?.SelectedSong, songScore);
        }

        /// <summary>
        /// Instantiate player cards to display.
        /// </summary>
        private void CreatePlayerCards()
        {
            // clear existing cards (may be left in for dev preview)
            foreach (Transform pc in playerCardsContainer.transform)
            {
                Destroy(pc.gameObject);
            }

            playerCards.Clear();

            foreach (var player in PlayerManager.players)
            {
                // skip players sitting out
                if (player.chosenInstrument == null) continue;

                var pc = Instantiate(playerCardPrefab, playerCardsContainer.transform).GetComponent<PlayerCard>();

                ClearStatus clr;
                if (bot.Contains(player))
                {
                    clr = ClearStatus.Bot;
                }
                else if (disqualified.Contains(player))
                {
                    clr = ClearStatus.Disqualified;
                }
                else
                {
                    clr = ClearStatus.Cleared;
                }

                pc.Setup(player, clr, highScores.Contains(player));
                playerCards.Add(pc);
            }
        }

        IEnumerator EnableAnimation()
        {
            /* Initial States */

            // background border
            songInfoCG.alpha = 0f;

            // background border
            var bgBorderTgt = backgroundBorderPass.color.a;
            backgroundBorderPass.color = new Color(1f, 1f, 1f, 0f);

            // star score
            starScoreCG.alpha = 0f;

            // margin container (player cards)
            var ccYMinTgt = marginContainerRT.anchorMin.y;
            var ccYMaxTgt = marginContainerRT.anchorMax.y;
            marginContainerRT.anchorMin += new Vector2(0, 1);
            marginContainerRT.anchorMax += new Vector2(0, 1);

            // score WIP notice (attached to player cards)
            var wipC = scoreWIPNotice.color;
            wipC.a = 0;
            scoreWIPNotice.color = wipC;

            // help bar
            // var hbYMinTgt = helpBarRT.anchorMin.y;
            // var hbYMaxTgt = helpBarRT.anchorMax.y;
            // helpBarRT.anchorMin -= new Vector2(0, helpBarRT.anchorMax.y);
            // helpBarRT.anchorMax -= new Vector2(0, helpBarRT.anchorMax.y);

            /* Run Animations */
            // fade in SongInfo
            songInfoCG.DOFade(1, .5f);

            // fade in background
            yield return backgroundBorderPass
                .DOFade(bgBorderTgt, 1.5f)
                .WaitForCompletion();

            // fade in score stars
            yield return starScoreCG
                .DOFade(1f, 0.5f);

            // slide in player cards
            marginContainerRT
                .DOAnchorMin(new Vector2(marginContainerRT.anchorMin.x, ccYMinTgt), .75f)
                .SetEase(Ease.OutBack, overshoot: 1.2f);
            yield return marginContainerRT
                .DOAnchorMax(new Vector2(marginContainerRT.anchorMax.x, ccYMaxTgt), .75f)
                .SetEase(Ease.OutBack, overshoot: 1.2f)
                .WaitForCompletion();

            // show note that scoring system is WIP
            scoreWIPNotice.DOFade(1f, 1f);

            OnEnableAnimationFinish();

            // slide in helpbar
            // helpBarRT
            // 	.DOAnchorMin(new Vector2(helpBarRT.anchorMin.x, hbYMinTgt), .75f)
            // 	.SetEase(Ease.OutQuad);
            // helpBarRT
            // 	.DOAnchorMax(new Vector2(helpBarRT.anchorMax.x, hbYMaxTgt), .75f)
            // 	.SetEase(Ease.OutQuad);
        }

        private void OnEnableAnimationFinish()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Confirm, "Exit", () => { PlayExit(); }),
                new NavigationScheme.Entry(MenuAction.Shortcut1, "Restart", () => { PlayRestart(); })
            }, false));

            foreach (var pc in playerCards)
            {
                pc.Engage();
            }
        }

        private void OnDisable()
        {
            // Unsubscribe player inputs
            Navigator.Instance.PopScheme();
        }

        // TODO: replace with common restart call (ie. what the pause menu calls)
        public void PlayRestart()
        {
            GameManager.AudioManager.UnloadSong();
            GameManager.Instance.LoadScene(SceneIndex.PLAY);
            Play.Instance.Paused = false;
        }

        /// <summary>
        /// Go to song select.
        /// </summary>
        public void PlayExit()
        {
            Play.Instance.Exit();
            OnEnabled?.Invoke(false);
        }
    }
}
