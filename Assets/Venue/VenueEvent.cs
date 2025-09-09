// VenueEvent.cs
using UnityEngine;

[System.Serializable]
public class VenueEvent
{
    public float timeStamp;
    public VenueEventType eventType;
    public string eventData;
}

public enum VenueEventType
{
    CameraCut,
    Lighting,
    PostProcess,
    DirectedCut
}