// VenueSetup.cs - Helper script for scene setup
using UnityEngine;

public class VenueSetup : MonoBehaviour
{
    [Header("Scene Setup")]
    public VenueManager venueManager;
    
    void Start()
    {
        // Optional: Create example events on start
        // CreateExampleVenueEvents();
    }
    
    public void CreateExampleVenueEvents()
    {
        if (venueManager == null) return;
        
        venueManager.venueEvents.Clear();
        
        // Example venue timeline for a 30-second demo
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 0f, 
            eventType = VenueEventType.CameraCut, 
            eventData = "coop_all_near" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 0f, 
            eventType = VenueEventType.Lighting, 
            eventData = "verse" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 3f, 
            eventType = VenueEventType.CameraCut, 
            eventData = "coop_g_near" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 6f, 
            eventType = VenueEventType.CameraCut, 
            eventData = "coop_v_near" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 8f, 
            eventType = VenueEventType.Lighting, 
            eventData = "chorus" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 9f, 
            eventType = VenueEventType.CameraCut, 
            eventData = "coop_gv_near" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 12f, 
            eventType = VenueEventType.PostProcess, 
            eventData = "film_sepia_ink.pp" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 15f, 
            eventType = VenueEventType.DirectedCut, 
            eventData = "directed_guitar" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 18f, 
            eventType = VenueEventType.Lighting, 
            eventData = "strobe_fast" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 20f, 
            eventType = VenueEventType.PostProcess, 
            eventData = "ProFilm_a.pp" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 22f, 
            eventType = VenueEventType.CameraCut, 
            eventData = "coop_all_far" 
        });
        
        venueManager.venueEvents.Add(new VenueEvent 
        { 
            timeStamp = 25f, 
            eventType = VenueEventType.Lighting, 
            eventData = "blackout_fast" 
        });
        
        Debug.Log("Created " + venueManager.venueEvents.Count + " example venue events!");
    }
}