#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)

using System.Threading;
using LibVLCSharp;
using UnityEngine;
using YARG.Core.Logging;

/// <summary>
/// libVLC's watch_time timer for one <see cref="VLCMediaPlayer"/>: counts presented pictures,
/// records the last one's timestamp, and decides when a seek has landed.
/// </summary>
/// <remarks>
/// Owned by YARG rather than the vendored player. Three libVLC rules shape it:
/// <list type="bullet">
/// <item>One watcher per player.</item>
/// <item>Callbacks run on libVLC threads. They only record into Interlocked fields and never call
/// into the player.</item>
/// <item>libVLC asserts at player deletion that no watcher remains, aborting the process. So
/// <see cref="Stop"/> must run before VLCMediaPlayer disposes the player, which
/// <see cref="VlcPlayerTeardownHook"/> guarantees.</item>
/// </list>
/// </remarks>
public sealed class VlcTimeWatch
{
    // A seek has landed once libVLC's on_seek reports it finished AND a picture has been presented
    // after that. on_seek alone fires before any picture from the new position exists. A distance
    // test against the target never passes for a mid-GOP target, because the seek settles on the
    // nearest decodable frame. Any picture after on_seek is post-seek: the seek flushes the
    // decoder.
    private const long SEEK_MIN_FRAMES = 1;

    // Always ends the wait: a caller may be holding the song paused on it. A seek to the position
    // the player already holds presents nothing new, so it ends here.
    public const float SEEK_TIMEOUT_SECONDS = 0.35f;

    public enum SeekStatus { None, Pending, Landed, TimedOut }

    private readonly VLCMediaPlayer _owner;
    private MediaPlayer _watchedPlayer;
    private VlcPlayerTeardownHook _teardownHook;

    // Held so the GC keeps them alive while libVLC holds pointers to them.
    private MediaPlayer.WatchTimeOnUpdate _onUpdate;
    private MediaPlayer.WatchTimeOnSeek _onSeek;

    // Written on libVLC threads.
    private long _picturesPresented;
    private long _presentedPictureTs = long.MinValue;
    private int _seekFinished;
    private long _seekFinishedAtPicture;

    // Main thread only.
    private bool _seekPending;
    private bool _seekAccepted;
    private long _seekFrameBaseline;
    private float _seekDeadline;

    public VlcTimeWatch(VLCMediaPlayer owner)
    {
        _owner = owner;
    }

    public bool Active => _watchedPlayer != null;

    /// <summary>Pictures presented since this watch was created. Never resets.</summary>
    public long PicturesPresented => Interlocked.Read(ref _picturesPresented);

    /// <summary>Stream time in seconds of the last presented picture; NaN before the first.</summary>
    public double PresentedPictureTime
    {
        get
        {
            long ts = Interlocked.Read(ref _presentedPictureTs);
            return ts == long.MinValue ? double.NaN : ts / 1_000_000.0;
        }
    }

    public double SeekTarget { get; private set; }
    public bool SeekAccepted => _seekAccepted;
    public long PicturesSinceSeek => PicturesPresented - _seekFrameBaseline;

    public unsafe void Start()
    {
        var player = _owner != null ? _owner.MediaPlayer : null;
        if (player == null || player == _watchedPlayer)
            return;

        Stop();
        Interlocked.Exchange(ref _presentedPictureTs, long.MinValue);

        _onUpdate = (point, _) =>
        {
            // INT64_MAX system date is a paused-clock update, not a presented picture.
            if (point.SystemDate == long.MaxValue)
                return;

            Interlocked.Exchange(ref _presentedPictureTs, point.Time);
            Interlocked.Increment(ref _picturesPresented);
        };
        _onSeek = (point, _) =>
        {
            // Called with the request, then with null once seeking has finished.
            if (point.HasValue)
                return;

            Interlocked.Exchange(ref _seekFinishedAtPicture, Interlocked.Read(ref _picturesPresented));
            Interlocked.Exchange(ref _seekFinished, 1);
        };

        // 0 = every update; the paused callback is unused, and the API accepts null for it.
        if (!player.WatchTime(0, _onUpdate, null, _onSeek))
        {
            _onUpdate = null;
            _onSeek = null;
            YargLogger.LogWarning("[VlcTimeWatch] WatchTime registration failed; seeks complete on " +
                "their timeout.");
            return;
        }

        _watchedPlayer = player;

        if (_teardownHook == null)
        {
            var go = _owner.gameObject;
            _teardownHook = go.GetComponent<VlcPlayerTeardownHook>();
            if (_teardownHook == null)
                _teardownHook = go.AddComponent<VlcPlayerTeardownHook>();
            _teardownHook.Disabled = Stop;
        }
    }

    /// <summary>Idempotent; every teardown path calls it.</summary>
    public void Stop()
    {
        // Only unwatch the player it was registered on: a replaced player may already be disposed.
        if (_watchedPlayer != null && _owner != null && _owner.MediaPlayer == _watchedPlayer)
            _watchedPlayer.UnwatchTime();

        _watchedPlayer = null;
        _onUpdate = null;
        _onSeek = null;
    }

    /// <summary>Call before issuing the seek: on_seek can arrive before SeekTo returns.</summary>
    public void BeginSeek(double target)
    {
        SeekTarget = target;
        _seekFrameBaseline = PicturesPresented;
        _seekDeadline = Time.unscaledTime + SEEK_TIMEOUT_SECONDS;
        _seekPending = true;
        _seekAccepted = false;
        Interlocked.Exchange(ref _seekFinished, 0);
    }

    /// <summary>Once per frame. Reports Landed or TimedOut exactly once per seek.</summary>
    public SeekStatus PollSeek()
    {
        if (!_seekPending)
            return SeekStatus.None;

        // Pictures presented before on_seek finished are pre-seek; count only those after it.
        if (!_seekAccepted && Interlocked.CompareExchange(ref _seekFinished, 0, 0) == 1)
        {
            _seekAccepted = true;
            _seekFrameBaseline = Interlocked.Read(ref _seekFinishedAtPicture);
        }

        if (_seekAccepted && PicturesSinceSeek >= SEEK_MIN_FRAMES)
        {
            _seekPending = false;
            return SeekStatus.Landed;
        }

        if (Time.unscaledTime >= _seekDeadline)
        {
            _seekPending = false;
            return SeekStatus.TimedOut;
        }

        return SeekStatus.Pending;
    }
}

#endif
