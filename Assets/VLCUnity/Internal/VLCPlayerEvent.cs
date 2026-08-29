using System;
using UnityEngine.Events;
using LibVLCSharp;

[Serializable]
public class VLCPlayerEvent : UnityEvent<VLCState> { }
