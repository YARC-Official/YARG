using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class FpsCounter : MonoBehaviour {

	// Instance
	public static FpsCounter Instance { get; private set; } = null;

	// FPS Counter
	public Image fpsCircle;
	public TextMeshProUGUI fpsText;
	private float updateTime = 1f;

	// Awake
	private void Awake() {
		Instance = this;
	}

	// check if the settings have changed and update the fps counter accordingly
	public void UpdateSettings(bool value) {
		SetVisible(value);
	}

	public void SetVisible(bool value) {
		fpsText.gameObject.SetActive(value);
		fpsCircle.gameObject.SetActive(value);
	}

	// OnDestroy - set the instance to null
	private void OnDestroy() {
		Instance = null;
	}

	// Update is called once per frame
	void Update() {
		// update fps per second
		if (Time.unscaledTime > updateTime) {

			// (1 / unscaledDeltaTime) = FPS
			int fps = (int)(1f / Time.unscaledDeltaTime);

			// Clear the FPS text
			fpsText.text = "";

			// Color the FPS sprite based on the FPS
			if (fps < 30) {
				// RED
				if (ColorUtility.TryParseHtmlString("#FF0035", out Color color)) {
					fpsCircle.color = color;
				} else {
					fpsCircle.color = Color.red;
				}
			} else if (fps < 60) {
				// YELLOW
				if (ColorUtility.TryParseHtmlString("#FFD43A", out Color color)) {
					fpsCircle.color = color;
				} else {
					fpsCircle.color = Color.yellow;
				}
			} else {
				// GREEN
				if (ColorUtility.TryParseHtmlString("#46E74F", out Color color)) {
					fpsCircle.color = color;
				} else {
					fpsCircle.color = Color.green;
				}
			}

			// Display the FPS
			fpsText.text += "FPS: " + fps.ToString();

#if UNITY_EDITOR
			// Display the memory usage
			fpsText.text += "\nMemory: " + (Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024).ToString() + " MB";
#endif

			// reset the update time
			updateTime = Time.unscaledTime + 1f;
		}
	}
}