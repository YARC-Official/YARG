// VenueManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VenueManager : MonoBehaviour
{
    [Header("Venue Components")]
    public CameraController cameraController;
    public LightingController lightingController;
    public PostProcessController postProcessController;
    
    [Header("Venue Events")]
    public List<VenueEvent> venueEvents = new List<VenueEvent>();
    
    [Header("Playback")]
    public bool isPlaying = false;
    public float currentTime = 0f;
    
    private int currentEventIndex = 0;

    void Start()
    {
        // Sort events by timestamp
        venueEvents = venueEvents.OrderBy(e => e.timeStamp).ToList();
        
        // Initialize with default states
        if (cameraController != null)
            cameraController.SetCameraShot("coop_all_near");
            Debug.LogWarning("SetCameraShot = coop_all_near");
        if (lightingController != null)
            lightingController.SetLighting("verse");
            Debug.LogWarning("SetLighting = Verse");
        if (postProcessController != null)
            postProcessController.SetPostProcess("ProFilm_a.pp");
            Debug.LogWarning("SetPostProcess = ProFilm_a.pp");
    }
    
    void Update()
    {
        if (isPlaying)
        {
            currentTime += Time.deltaTime;
            ProcessVenueEvents();
        }
    }
    
    void ProcessVenueEvents()
    {
        while (currentEventIndex < venueEvents.Count && 
               venueEvents[currentEventIndex].timeStamp <= currentTime)
        {
            ProcessEvent(venueEvents[currentEventIndex]);
            currentEventIndex++;
        }
    }
    
    void ProcessEvent(VenueEvent venueEvent)
    {
        switch (venueEvent.eventType)
        {
            case VenueEventType.CameraCut:
                cameraController?.SetCameraShot(venueEvent.eventData);
                break;
            case VenueEventType.DirectedCut:
                cameraController?.SetDirectedCut(venueEvent.eventData);
                break;
            case VenueEventType.Lighting:
                lightingController?.SetLighting(venueEvent.eventData);
                break;
            case VenueEventType.PostProcess:
                postProcessController?.SetPostProcess(venueEvent.eventData);
                break;
        }
    }
    
    public void Play()
    {
        isPlaying = true;
    }
    
    public void Stop()
    {
        isPlaying = false;
        currentTime = 0f;
        currentEventIndex = 0;
    }
    
    public void Pause()
    {
        isPlaying = false;
    }
}