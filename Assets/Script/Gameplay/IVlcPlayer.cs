#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)

using System;
using System.Threading.Tasks;
using LibVLCSharp;
using UnityEngine;

/// <summary>
/// The libVLC player <see cref="YargVideoPlayer"/> drives: vlc-unity's VLCMediaPlayer (through
/// <see cref="VlcUnityPlayer"/>) everywhere VLC works, or <see cref="VlcCpuMediaPlayer"/> on
/// Linux + NVIDIA + Vulkan. Times are in milliseconds.
/// </summary>
public interface IVlcPlayer
{
    MediaPlayer MediaPlayer { get; }
    RenderTexture OutputTexture { get; }
    event Action<RenderTexture> OnTextureResized;
    long Time { get; }
    long Duration { get; }
    Task OpenAsync(string path, params string[] options);
    void Play();
    void Pause();
    void Stop();
    void SetTime(long milliseconds);

    // The player's component.
    bool enabled { get; set; }
    bool didAwake { get; }
    MonoBehaviour component { get; }
}

/// <summary>Forwards <see cref="IVlcPlayer"/> to vlc-unity's own player, unchanged.</summary>
public sealed class VlcUnityPlayer : IVlcPlayer
{
    private readonly VLCMediaPlayer _player;

    public VlcUnityPlayer(VLCMediaPlayer player)
    {
        _player = player;
    }

    public MediaPlayer MediaPlayer => _player.MediaPlayer;
    public RenderTexture OutputTexture => _player.OutputTexture;

    public event Action<RenderTexture> OnTextureResized
    {
        add => _player.OnTextureResized += value;
        remove => _player.OnTextureResized -= value;
    }

    public long Time => _player.Time;
    public long Duration => _player.Duration;
    public Task OpenAsync(string path, params string[] options) => _player.OpenAsync(path, options);
    public void Play() => _player.Play();
    public void Pause() => _player.Pause();
    public void Stop() => _player.Stop();
    public void SetTime(long milliseconds) => _player.SetTime(milliseconds);

    public bool enabled
    {
        get => _player.enabled;
        set => _player.enabled = value;
    }

    public bool didAwake => _player.didAwake;
    public MonoBehaviour component => _player;
}

#endif
