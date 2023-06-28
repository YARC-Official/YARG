using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Data;

namespace YARG.UI
{
    public class ScoreSection : MonoBehaviour
    {
        public enum ScoreType
        {
            NORMAL,
            HIGH_SCORE,
            DISQUALIFIED
        }

        [SerializeField]
        private TextMeshProUGUI playerName;

        [SerializeField]
        private Transform textContainer;

        [SerializeField]
        private List<TextMeshProUGUI> text;

        public void SetScore(PlayerManager.Player player, ScoreType type)
        {
            if (player.chosenInstrument == null)
            {
                playerName.text = player.DisplayName;
                text[0].text = "<color=red>Sat Out</color>";
                return;
            }

            playerName.text = $"<sprite name=\"{player.chosenInstrument}\"> {player.DisplayName}";

            if (!player.lastScore.HasValue)
            {
                return;
            }

            const int TEXT_COUNT = 6;

            // Spawn additional text
            for (int i = 0; i < TEXT_COUNT - 1; i++)
            {
                text.Add(Instantiate(text[0].gameObject, textContainer).GetComponent<TextMeshProUGUI>());
            }

            string end = type switch
            {
                ScoreType.HIGH_SCORE   => " <color=green>HI</color>",
                ScoreType.DISQUALIFIED => " <color=red>DQ</color>",
                _                      => "",
            };

            var score = player.lastScore.Value;
            text[0].text = player.chosenDifficulty switch
            {
                Difficulty.EASY        => "Easy",
                Difficulty.MEDIUM      => "Medium",
                Difficulty.HARD        => "Hard",
                Difficulty.EXPERT      => "Expert",
                Difficulty.EXPERT_PLUS => "Expert+",
                _                      => throw new System.Exception("Unreachable")
            };
            text[1].text = $"{score.percentage.percent * 100f:N1}%" + end;
            text[2].text = $"{score.notesHit} hit";
            text[3].text = $"{score.notesMissed} missed";
            text[4].text = $"{score.score.score:N0} pts";
            text[5].text = $"{score.score.stars}/6 stars";
        }
    }
}