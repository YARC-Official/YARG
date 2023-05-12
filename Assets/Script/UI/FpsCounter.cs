using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Settings;
using YARG.Data;


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
        if (!value) {
            fpsText.gameObject.SetActive(false);
            fpsCircle.gameObject.SetActive(false);
        }
        else {
            fpsText.gameObject.SetActive(true);
            fpsCircle.gameObject.SetActive(true);
        }
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
                fpsCircle.color = Color.red;
            } else if (fps < 60) {
                fpsCircle.color = Color.yellow;
            } else {
                fpsCircle.color = Color.green;
            }

            // Display the FPS
            fpsText.text += "FPS: " + fps.ToString();

            // reset the update time
            updateTime = Time.unscaledTime + 1f;
        }
	}
}