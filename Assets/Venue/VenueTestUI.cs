// VenueTestUI.cs - Simple UI for testing
using UnityEngine;
using UnityEngine.UI;

public class VenueTestUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Button playButton;
    public Button stopButton;
    public Button pauseButton;
    public Button populateButton;
    public Slider timeSlider;
    public Text timeText;
    
    [Header("References")]
    public VenueManager venueManager;
    public VenueSetup VenueSetup;
    
    void Start()
    {
        if (playButton != null)
            playButton.onClick.AddListener(() => venueManager.Play());
        
        if (stopButton != null)
            stopButton.onClick.AddListener(() => venueManager.Stop());

        if (populateButton != null)
            populateButton.onClick.AddListener(() => VenueSetup.CreateExampleVenueEvents());
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(() => venueManager.Pause());
        
        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 236f; // 230-second demo
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }
    }
    
    void Update()
    {
        if (venueManager != null)
        {
            if (timeSlider != null && timeSlider.interactable)
                timeSlider.value = venueManager.currentTime;
            
            if (timeText != null)
                timeText.text = $"Time: {venueManager.currentTime:F1}s";
        }
    }
    
    void OnTimeSliderChanged(float value)
    {
        if (venueManager != null)
        {
            venueManager.currentTime = value;
            // Reset event index based on current time
            int newIndex = 0;
            for (int i = 0; i < venueManager.venueEvents.Count; i++)
            {
                if (venueManager.venueEvents[i].timeStamp <= value)
                    newIndex = i + 1;
                else
                    break;
            }
            // Note: You'd need to make currentEventIndex public in VenueManager for this
        }
    }
}