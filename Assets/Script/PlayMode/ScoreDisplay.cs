using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreDisplay : MonoBehaviour {
    [SerializeField]
    private TextMeshProUGUI scoreText;

    private void Start() {
        scoreText.text = $"<mspace=0.59em>0";
    }

    private void OnEnable() {
		ScoreKeeper.OnScoreChange += OnScoreChange;
	}

    private void OnDisable() {
        ScoreKeeper.OnScoreChange -= OnScoreChange;
    }

    private void OnScoreChange() {
        scoreText.text = $"<mspace=0.59em>{ScoreKeeper.TotalScore:n0}";
    }
}
