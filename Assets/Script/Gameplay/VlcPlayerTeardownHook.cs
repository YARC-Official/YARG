using System;
using UnityEngine;

/// <summary>
/// Runs a callback when the VLC player's own GameObject is disabled.
/// </summary>
/// <remarks>
/// Added at runtime by <see cref="YargVideoPlayer"/> to the GameObject that carries
/// VLCMediaPlayer. Destroying a GameObject disables all of its components before any of their
/// OnDestroy runs, so this fires before VLCMediaPlayer.OnDestroy disposes the player -- whatever
/// order a scene unload destroys objects in. YargVideoPlayer's own OnDisable cannot promise that:
/// it lives on a different GameObject, and scene unload tears objects down one at a time.
/// </remarks>
public sealed class VlcPlayerTeardownHook : MonoBehaviour
{
    public Action Disabled;

    private void OnDisable()
    {
        Disabled?.Invoke();
    }
}
