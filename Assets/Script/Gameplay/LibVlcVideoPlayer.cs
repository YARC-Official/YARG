using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using LibVLCSharp.Shared;
using UnityEngine;
using UnityEngine.Video;
using YARG.Core.Logging;
// LibVLCSharp.Shared.Core would otherwise be shadowed by the sibling YARG.Core namespace, since
// this file lives under namespace YARG.Gameplay (enclosing-namespace lookup wins over `using`).
using VlcCore = LibVLCSharp.Shared.Core;
// Both LibVLCSharp.Shared and YARG.Core.Logging declare a LogLevel type, so an unqualified
// LogLevel reference is ambiguous between the two `using` directives; alias the libVLC one.
using VlcLogLevel = LibVLCSharp.Shared.LogLevel;

namespace YARG.Gameplay
{
    // Drop-in replacement for the subset of UnityEngine.Video.VideoPlayer's API that
    // BackgroundManager uses, backed by libVLC for broader codec/container support (VP9, AV1,
    // HEVC, mkv/avi, etc.) than Unity's VideoPlayer.
    //
    // libVLC can't write directly into a Unity texture without the paid vlc-unity GPU-interop
    // plugin, so decoded frames are pulled out via SetVideoCallbacks into a native buffer and
    // blitted into a Texture2D -> targetTexture each frame. CPU-side and slower than GPU
    // interop, but free/LGPL and adequate for a single background video.
    public class LibVlcVideoPlayer : MonoBehaviour
    {
        private static LibVLC _libVlc;

        [SerializeField]
        private Camera _targetCamera;
        public Camera targetCamera => _targetCamera;

        // The fallback VideoPlayer never renders directly into this -- see _fallbackFrameTexture.
        private RenderTexture _targetTexture;
        public RenderTexture targetTexture
        {
            get => _targetTexture;
            set => _targetTexture = value;
        }

        public event Action<LibVlcVideoPlayer> prepareCompleted;
        public event Action<LibVlcVideoPlayer> seekCompleted;

        public string url { private get; set; }

        // Distinct from MonoBehaviour.enabled -- mirrors the old YargVideoPlayer's
        // "playerEnabled" concept, letting BackgroundManager toggle just the underlying
        // player without touching this component's own Update() loop.
        public bool playerEnabled
        {
            get => _playerEnabled;
            set
            {
                _playerEnabled = value;
                if (!_usingVlc && _fallbackPlayer != null)
                {
                    _fallbackPlayer.enabled = value;
                }
            }
        }

        public double time
        {
            get => _usingVlc
                ? (_mediaPlayer != null ? _mediaPlayer.Time / 1000.0 : 0.0)
                : (_fallbackPlayer != null ? _fallbackPlayer.time : 0.0);
            set
            {
                if (!_usingVlc)
                {
                    if (_fallbackPlayer != null)
                    {
                        _fallbackPlayer.time = value;
                    }
                    return;
                }

                // libVLC silently drops Time before playback actually begins (Playing event) --
                // e.g. BackgroundManager.OnVideoPrepared setting video_start_time. Remember and
                // reapply once Playing fires.
                _pendingSeekTime = value;

                if (_mediaPlayer == null)
                {
                    return;
                }

                // Before the first Playing event, libVLC drops this write anyway -- skip the
                // doomed attempt and let the deferred reapply above handle it (see _hasEverPlayed).
                if (!_hasEverPlayed)
                {
                    return;
                }

                BeginSeek((long) (value * 1000.0));
            }
        }

        // Seeks are async, and PositionChanged fires on every position update, not just once a
        // seek lands -- track the target and require Time to actually reach it (see
        // PositionChanged handler below) before signaling seekCompleted.
        private void BeginSeek(long targetMs)
        {
            _seeking = true;
            _seekTargetMs = targetMs;
            _seekRequestedAtUnscaledTime = Time.unscaledTime;
            _framesDeliveredSinceSeek = 0;
            _mediaPlayer.Time = targetMs;
        }

        // DisplayFrame deliveries since the last BeginSeek. seekCompleted/Time report the seek
        // target the instant it's requested, not once decode actually catches up -- the first
        // frame or two delivered after a seek can still be stale. Callers that need the on-screen
        // result (not just the reported one) should wait for a small margin of this instead of
        // trusting seekCompleted (see BackgroundManager.MIN_FRAMES_BEFORE_INITIAL_REVEAL).
        public int FramesDeliveredSinceSeek => _framesDeliveredSinceSeek;

        public double length { get; private set; }

        public float playbackSpeed
        {
            get => _usingVlc ? (_mediaPlayer?.Rate ?? 1f) : (_fallbackPlayer != null ? _fallbackPlayer.playbackSpeed : 1f);
            set
            {
                if (_usingVlc)
                {
                    _mediaPlayer?.SetRate(value);
                }
                else if (_fallbackPlayer != null)
                {
                    _fallbackPlayer.playbackSpeed = value;
                }
            }
        }

        private bool _isLooping;
        public bool isLooping
        {
            get => _usingVlc ? _isLooping : (_fallbackPlayer != null && _fallbackPlayer.isLooping);
            set
            {
                _isLooping = value;
                if (_fallbackPlayer != null)
                {
                    _fallbackPlayer.isLooping = value;
                }
            }
        }

        // Set once, the first time Prepare() is called -- true if libVLC initialized
        // successfully, false if we've fallen back to Unity's built-in VideoPlayer.
        private bool _vlcAttempted;
        private bool _usingVlc;
        private bool _playerEnabled = true;
        private VideoPlayer _fallbackPlayer;

        // Intermediate texture the fallback VideoPlayer renders into, sized to the clip's own
        // native resolution
        private RenderTexture _fallbackFrameTexture;

        private MediaPlayer _mediaPlayer;
        private Media _media;

        private Texture2D _frameTexture;
        private IntPtr _frameBuffer = IntPtr.Zero;
        private int _frameWidth;
        private int _frameHeight;
        private readonly object _frameLock = new();
        private bool _frameDirty;
        private bool _seeking;
        private int _framesDeliveredSinceSeek;
        private long _seekTargetMs;
        private float _seekRequestedAtUnscaledTime;
        private bool _pendingLoopRestart;
        private double? _pendingSeekTime;
        private bool _applyPendingSeekOnNextUpdate;

        // True once Playing has fired at least once for this media. A seek attempted before that
        // is silently dropped by libVLC, and letting it try anyway races SEEK_TIMEOUT_SECONDS
        // against however long the caller takes to call Play() -- if the timeout wins, it fires a
        // spurious seekCompleted that callers can mistake for the real seek landing.
        private bool _hasEverPlayed;

        // Seek-complete tolerance, and how long to wait before giving up and firing
        // seekCompleted anyway (callers like BackgroundManager gate their own Update() on
        // "seeking" and would otherwise wait forever).
        private const long SEEK_COMPLETE_TOLERANCE_MS = 300;
        private const float SEEK_TIMEOUT_SECONDS = 2f;

        private static void EnsureLibVlc()
        {
            if (_libVlc != null)
            {
                return;
            }

            var resolved = LibVlcNativePath.Resolve();
            if (resolved is { } result)
            {
                YargLogger.LogInfo($"[Video] Using bundled libVLC at {result.NativeDir} (plugins: {result.PluginsDir ?? "<none found>"})");

                if (result.PluginsDir != null)
                {
                    // Set explicitly rather than relying on Core.Initialize's relative-path guessing.
                    Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", result.PluginsDir);
                }

                // Plugin dependencies (libavcodec, libdav1d, etc.) are flattened alongside
                // libvlc.so on Linux; glibc re-reads LD_LIBRARY_PATH on every dlopen(), so
                // setting it here (after process start) still works.
                var existingLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
                var newLdPath = string.IsNullOrEmpty(existingLdPath)
                    ? result.NativeDir
                    : result.NativeDir + Path.PathSeparator + existingLdPath;
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newLdPath);

                VlcCore.Initialize(result.NativeDir);
            }
            else
            {
                // No bundled binary (e.g. Linux, no redistributable NuGet package) -- fall back
                // to a system-installed libvlc via LibVLCSharp's default search.
                YargLogger.LogInfo("[Video] No bundled libVLC found; falling back to Core.Initialize() default search");
                VlcCore.Initialize();
            }

            try
            {
                _libVlc = new LibVLC(enableDebugLogs: true);
                YargLogger.LogInfo("[Video] LibVLC instance created successfully");
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "[Video] Failed to construct LibVLC -- native libvlc failed to load");
                throw;
            }

            // Surface libVLC's own engine log through YargLogger so one log captures the whole
            // pipeline.
            _libVlc.Log += (_, e) =>
            {
                var message = $"[Video][libvlc:{e.Level}] {e.Module}: {e.Message}";
                switch (e.Level)
                {
                    case VlcLogLevel.Error:
                        YargLogger.LogError(message);
                        break;
                    case VlcLogLevel.Warning:
                        YargLogger.LogWarning(message);
                        break;
                    default:
                        YargLogger.LogDebug(message);
                        break;
                }
            };
        }

        public void Prepare()
        {
            if (!_vlcAttempted)
            {
                _vlcAttempted = true;
                try
                {
                    // Must happen synchronously, before the async PrepareAsync -- an exception
                    // thrown inside a fire-and-forget UniTaskVoid can't be caught by a try/catch
                    // around the call site, only logged by UniTaskScheduler.
                    EnsureLibVlc();
                    _usingVlc = true;
                }
                catch (Exception ex)
                {
                    YargLogger.LogWarning($"[Video] libVLC unavailable ({ex.Message}); falling back to " +
                        "Unity's built-in VideoPlayer.");
                    _usingVlc = false;
                    SwitchToFallback();
                }
            }

            if (_usingVlc)
            {
                PrepareAsync(url).Forget();
            }
            else
            {
                PrepareFallback();
            }
        }

        // Configures the Unity VideoPlayer used when libVLC isn't available. Added lazily
        // (rather than serialized in the scene) so this component stays a drop-in replacement
        // wherever it's placed.
        private void SwitchToFallback()
        {
            _fallbackPlayer = GetComponent<VideoPlayer>();
            if (_fallbackPlayer == null)
            {
                _fallbackPlayer = gameObject.AddComponent<VideoPlayer>();
            }

            _fallbackPlayer.playOnAwake = false;
            _fallbackPlayer.renderMode = VideoRenderMode.RenderTexture;
            _fallbackPlayer.isLooping = _isLooping;
            _fallbackPlayer.enabled = _playerEnabled;

            _fallbackPlayer.prepareCompleted += vp =>
            {
                // vp.width/height report the clip's native resolution, valid once prepared --
                // render into a texture that size so Update() can cover-fit blit it into
                // targetTexture instead of Unity's VideoPlayer stretching straight into it.
                int srcW = (int) vp.width;
                int srcH = (int) vp.height;
                if (srcW > 0 && srcH > 0)
                {
                    _fallbackFrameTexture = new RenderTexture(srcW, srcH, 0);
                    _fallbackPlayer.targetTexture = _fallbackFrameTexture;
                }

                length = vp.length;
                prepareCompleted?.Invoke(this);
            };
            _fallbackPlayer.seekCompleted += _ => seekCompleted?.Invoke(this);
        }

        private void PrepareFallback()
        {
            _fallbackPlayer.url = url;
            _fallbackPlayer.Prepare();
        }

        private async UniTaskVoid PrepareAsync(string path)
        {
            YargLogger.LogInfo($"[Video] Prepare() -> {path}");

            _mediaPlayer?.Dispose();
            _media?.Dispose();
            _hasBlitted = false;
            _hasEverPlayed = false;

            _media = new Media(_libVlc, path, FromType.FromPath);

            YargLogger.LogDebug("[Video] Parsing media (ParseLocal)...");
            var parseResult = await _media.Parse(MediaParseOptions.ParseLocal);
            YargLogger.LogInfo($"[Video] Parse result: {parseResult}, duration={_media.Duration}ms, " +
                $"state={_media.State}, trackCount={_media.Tracks.Length}");

            length = _media.Duration / 1000.0;

            foreach (var track in _media.Tracks)
            {
                YargLogger.LogDebug($"[Video] Track: type={track.TrackType}, codec={track.Codec}, id={track.Id}");
            }

            var videoTrack = _media.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
            bool hasVideoTrack = videoTrack.TrackType == TrackType.Video;
            if (!hasVideoTrack)
            {
                YargLogger.LogWarning("[Video] No video track found after parsing -- media.Tracks reported no " +
                    "TrackType.Video entry. This can happen if libVLC's codec/demux plugins failed to load " +
                    "(check the [libvlc:...] log lines above for plugin errors) or the codec truly isn't supported.");
            }

            _frameWidth = hasVideoTrack && videoTrack.Data.Video.Width > 0 ? (int) videoTrack.Data.Video.Width : 1280;
            _frameHeight = hasVideoTrack && videoTrack.Data.Video.Height > 0 ? (int) videoTrack.Data.Video.Height : 720;
            YargLogger.LogInfo($"[Video] Frame size resolved to {_frameWidth}x{_frameHeight} (fromTrack={hasVideoTrack})");

            lock (_frameLock)
            {
                if (_frameBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_frameBuffer);
                }

                _frameBuffer = Marshal.AllocHGlobal(_frameWidth * _frameHeight * 4);
            }

            _mediaPlayer = new MediaPlayer(_media);
            // Background videos are silent -- YARG's own audio pipeline owns song audio, and
            // nothing here keeps a second stream in sync with the game clock.
            _mediaPlayer.Mute = true;
            // Software decode alone can fall behind real-time at >1x speed (see
            // BackgroundManager.CorrectVideoDrift) -- request hardware decode when a suitable
            // plugin is bundled (scripts/NativeBuild/libvlc-plugin-allowlist.txt).
            _mediaPlayer.EnableHardwareDecoding = true;
            YargLogger.LogDebug($"[Video] EnableHardwareDecoding=true (check nearby [libvlc:...] " +
                "lines for which decoder module actually engages)");
            _mediaPlayer.SetVideoFormat("RV32", (uint) _frameWidth, (uint) _frameHeight, (uint) (_frameWidth * 4));
            _mediaPlayer.SetVideoCallbacks(LockFrame, null, DisplayFrame);
            YargLogger.LogDebug("[Video] SetVideoFormat(RV32) + SetVideoCallbacks registered");

            _mediaPlayer.EncounteredError += (_, _) =>
            {
                YargLogger.LogError("[Video] MediaPlayer.EncounteredError fired");
            };

            _mediaPlayer.Playing += (_, _) =>
            {
                // Don't call back into libVLC synchronously here -- it runs on libVLC's own
                // thread mid state-transition, and re-entering (e.g. setting Time) deadlocks
                // against a lock libVLC still holds (observed: playback freezes dead at the
                // seek target). Defer to Update() instead.
                _applyPendingSeekOnNextUpdate = true;
                _hasEverPlayed = true;
            };

            _mediaPlayer.PositionChanged += (_, _) =>
            {
                // Fires on every position update, not just seeks -- only complete once Time
                // actually reaches the target. Update()'s timeout below is the backstop.
                if (_seeking && Math.Abs(_mediaPlayer.Time - _seekTargetMs) <= SEEK_COMPLETE_TOLERANCE_MS)
                {
                    _seeking = false;
                    seekCompleted?.Invoke(this);
                }
            };

            _mediaPlayer.EndReached += (_, _) =>
            {
                YargLogger.LogDebug("[Video] MediaPlayer.EndReached");
                if (isLooping)
                {
                    _pendingLoopRestart = true;
                }
            };

            YargLogger.LogInfo("[Video] Firing prepareCompleted");
            prepareCompleted?.Invoke(this);
        }

        public void Play()
        {
            if (_usingVlc)
            {
                _mediaPlayer?.Play();
            }
            else
            {
                _fallbackPlayer?.Play();
            }
        }

        // Seeks via the same deferred pipeline as the `time` setter (_pendingSeekTime ->
        // _applyPendingSeekOnNextUpdate -> BeginSeek) instead of writing Time synchronously --
        // a Time write while Paused doesn't reliably land until Play() transitions the player to
        // Playing, so writing immediately and also relying on Play()'s deferred reapply seeks
        // twice. Requires the player to be Paused/Stopped when called (true for every current
        // caller); if ever called while already Playing, Play() won't re-fire Playing and the
        // seek will silently never apply.
        public void SeekAndPlay(double time)
        {
            if (_usingVlc)
            {
                _pendingSeekTime = time;
                _mediaPlayer?.Play();
            }
            else if (_fallbackPlayer != null)
            {
                _fallbackPlayer.time = time;
                _fallbackPlayer.Play();
            }
        }

        public void Pause()
        {
            if (_usingVlc)
            {
                // MediaPlayer.Pause() wraps libvlc_media_player_pause(), which TOGGLES
                // play/pause rather than setting an explicit target state -- calling it while
                // already paused would resume playback instead of staying paused.
                // SetPause(true) is the idempotent equivalent (wraps
                // libvlc_media_player_set_pause), safe to call redundantly from racing
                // pause/resume paths.
                _mediaPlayer?.SetPause(true);
            }
            else
            {
                _fallbackPlayer?.Pause();
            }
        }

        public void Stop()
        {
            if (_usingVlc)
            {
                _mediaPlayer?.Stop();
            }
            else
            {
                _fallbackPlayer?.Stop();
            }
        }

        private bool _hasBlitted;

        private IntPtr LockFrame(IntPtr opaque, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, _frameBuffer);
            return IntPtr.Zero;
        }

        private void DisplayFrame(IntPtr opaque, IntPtr picture)
        {
            // Runs on libVLC's own thread; not perfectly atomic with BeginSeek's reset on the
            // main thread, but callers only need this roughly right, not exact.
            _framesDeliveredSinceSeek++;

            lock (_frameLock)
            {
                _frameDirty = true;
            }
        }

        private void Update()
        {
            if (!_usingVlc)
            {
                UpdateFallback();
                return;
            }

            if (_pendingLoopRestart)
            {
                YargLogger.LogDebug("[Video] Looping: Stop() + Play()");
                _pendingLoopRestart = false;
                _mediaPlayer.Stop();
                _mediaPlayer.Play();
            }

            if (_applyPendingSeekOnNextUpdate)
            {
                _applyPendingSeekOnNextUpdate = false;
                if (_pendingSeekTime is { } seekTime)
                {
                    _pendingSeekTime = null;
                    BeginSeek((long) (seekTime * 1000.0));
                }
            }

            if (_seeking && Time.unscaledTime - _seekRequestedAtUnscaledTime > SEEK_TIMEOUT_SECONDS)
            {
                YargLogger.LogWarning($"[Video] Seek to {_seekTargetMs}ms didn't land within " +
                    $"{SEEK_COMPLETE_TOLERANCE_MS}ms after {SEEK_TIMEOUT_SECONDS}s (currently at " +
                    $"{_mediaPlayer.Time}ms) -- giving up waiting and firing seekCompleted anyway.");
                _seeking = false;
                seekCompleted?.Invoke(this);
            }

            if (!_frameDirty || _frameBuffer == IntPtr.Zero)
            {
                return;
            }

            if (_frameTexture == null || _frameTexture.width != _frameWidth || _frameTexture.height != _frameHeight)
            {
                YargLogger.LogDebug($"[Video] (Re)creating frame Texture2D at {_frameWidth}x{_frameHeight}");
                _frameTexture = new Texture2D(_frameWidth, _frameHeight, TextureFormat.BGRA32, false);
            }

            lock (_frameLock)
            {
                _frameTexture.LoadRawTextureData(_frameBuffer, _frameWidth * _frameHeight * 4);
                _frameDirty = false;
            }

            _frameTexture.Apply(false);

            if (targetTexture != null)
            {
                BlitLetterboxed(_frameTexture, targetTexture, _frameWidth, _frameHeight);
            }
            else if (!_hasBlitted)
            {
                YargLogger.LogWarning("[Video] Decoded a frame but targetTexture is null -- nothing to blit into, " +
                    "so the video will not be visible anywhere. The caller must assign targetTexture before playback.");
            }

            _hasBlitted = true;
        }

        private void UpdateFallback()
        {
            if (_fallbackFrameTexture == null || targetTexture == null)
            {
                return;
            }

            BlitLetterboxed(_fallbackFrameTexture, targetTexture, _fallbackFrameTexture.width, _fallbackFrameTexture.height);
        }

        // Logged once per distinct (src, dst) pair rather than every frame -- Update() calls
        // this every frame once a video is playing, and the values are static for the life of
        // a given clip/targetTexture pairing.
        private int _lastLoggedSrcW, _lastLoggedSrcH, _lastLoggedDstW, _lastLoggedDstH;

        // "Contain" fit: scales the source down (or up) to fit entirely within the destination,
        // centered, letterboxing/pillarboxing whichever axis has slack -- unlike a plain
        // Graphics.Blit (which always stretches the source to fill every pixel of dest,
        // distorting it whenever src/dst aspect ratios differ) or a Blit(scale, offset) "cover"
        // crop (which fills dest completely but crops overflow, never showing the full frame).
        // Graphics.Blit's scale/offset overload only selects a SOURCE sub-rect -- it can't leave
        // dest partially unfilled -- so achieving letterboxing means placing a smaller
        // DESTINATION sub-rect ourselves via Graphics.DrawTexture in pixel space instead.
        private void BlitLetterboxed(Texture source, RenderTexture dest, int srcW, int srcH)
        {
            float srcAspect = (float) srcW / srcH;
            float dstAspect = (float) dest.width / dest.height;

            float destW, destH;
            if (srcAspect > dstAspect)
            {
                // Source is relatively wider than the destination -- fit width, letterbox top/bottom.
                destW = dest.width;
                destH = destW / srcAspect;
            }
            else
            {
                // Source is relatively taller than the destination -- fit height, pillarbox left/right.
                destH = dest.height;
                destW = destH * srcAspect;
            }

            if (srcW != _lastLoggedSrcW || srcH != _lastLoggedSrcH
                || dest.width != _lastLoggedDstW || dest.height != _lastLoggedDstH)
            {
                _lastLoggedSrcW = srcW;
                _lastLoggedSrcH = srcH;
                _lastLoggedDstW = dest.width;
                _lastLoggedDstH = dest.height;
                YargLogger.LogInfo($"[Video] Letterbox fit: src={srcW}x{srcH} (aspect={srcAspect:F3}) -> " +
                    $"dest={dest.width}x{dest.height} (aspect={dstAspect:F3}) => drawing {destW:F1}x{destH:F1} " +
                    $"at ({(dest.width - destW) * 0.5f:F1}, {(dest.height - destH) * 0.5f:F1})");
            }

            var prevActive = RenderTexture.active;
            RenderTexture.active = dest;
            GL.Clear(false, true, Color.black);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, dest.width, dest.height, 0);
            Graphics.DrawTexture(new Rect((dest.width - destW) * 0.5f, (dest.height - destH) * 0.5f, destW, destH), source);
            GL.PopMatrix();

            RenderTexture.active = prevActive;
        }

        private void OnDestroy()
        {
            _mediaPlayer?.Dispose();
            _media?.Dispose();

            lock (_frameLock)
            {
                if (_frameBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_frameBuffer);
                    _frameBuffer = IntPtr.Zero;
                }
            }

            if (_frameTexture != null)
            {
                Destroy(_frameTexture);
            }

            if (_fallbackFrameTexture != null)
            {
                _fallbackFrameTexture.Release();
                Destroy(_fallbackFrameTexture);
            }
        }
    }
}
