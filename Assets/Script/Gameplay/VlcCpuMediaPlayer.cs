#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
using LibVLCSharp;
using UnityEngine;
using YARG.Core.Logging;

/// <summary>
/// The Linux libVLC player, a drop-in for the parts of vlc-unity's VLCMediaPlayer that
/// <see cref="YargVideoPlayer"/> uses. Takes pictures through libVLC's vmem callbacks as I420
/// planes, uploads them to three R8 textures, and converts them to RGB on the GPU into
/// <see cref="OutputTexture"/>.
/// </summary>
/// <remarks>
/// vlc-unity's Linux Vulkan output imports linear DMA-BUFs as GL_TEXTURE_2D, which NVIDIA's driver
/// refuses; the renderer then leaves the player without an output and libVLC opens a window of its
/// own. vmem works on any GPU and driver and can never open a window: it sets libVLC's window
/// module to "dummy". It also sets dec-dev to "none", so decoding is always in software. The
/// player is still created through the plugin, which is how LibVLCSharp's Unity build creates
/// every MediaPlayer; vmem replaces whatever output it set.
/// </remarks>
public sealed class VlcCpuMediaPlayer : MonoBehaviour
{
    private const string SHADER_NAME = "Hidden/YARG/VlcI420ToRgb";
    private static readonly int U_TEX = Shader.PropertyToID("_UTex");
    private static readonly int V_TEX = Shader.PropertyToID("_VTex");

    public MediaPlayer MediaPlayer { get; private set; }

    /// <summary>The newest picture, upright, as VLCMediaPlayer's is after its flips.</summary>
    public RenderTexture OutputTexture { get; private set; }

    /// <summary>
    /// Raised on the main thread when <see cref="OutputTexture"/> is created or replaced at a new
    /// size, and with null before it is destroyed.
    /// </summary>
    public event Action<RenderTexture> OnTextureResized;

    /// <summary>The playback time in milliseconds.</summary>
    /// <summary>True once <see cref="Awake"/> has run, as VLCMediaPlayer's is.</summary>
    public bool didAwake { get; private set; }

    public VLCState CurrentState => MediaPlayer != null ? MediaPlayer.State : VLCState.NothingSpecial;

    public long Time => MediaPlayer != null ? MediaPlayer.Time / 1000 : 0;

    /// <summary>The media duration in milliseconds.</summary>
    public long Duration
    {
        get
        {
            using var media = MediaPlayer?.Media;
            return media == null ? 0 : media.Duration / 1000;
        }
    }

    /// <summary>
    /// The size of what the picture is drawn into. A larger video is scaled down to fit it by
    /// libVLC, on its own thread, so Unity's main thread never copies more than it shows. Zero
    /// means no limit. Read when libVLC opens a video output.
    /// </summary>
    public void SetMaxOutputSize(int width, int height)
    {
        lock (_frameLock)
            (_maxWidth, _maxHeight) = (width, height);
    }

    // Guards everything below. libVLC calls back on its own threads; the main thread uploads under
    // it, so a session's cleanup can't free a buffer mid-upload.
    private readonly object _frameLock = new();
    private FrameSession _session;
    // A permutation of 0..2: libVLC writes _write, OnDisplay publishes it as _ready, Update uploads
    // _read. libVLC never touches _read, so uploads never tear.
    private int _write, _ready = 1, _read = 2;
    private bool _fresh;
    private int _maxWidth, _maxHeight;

    private Material _material;
    private Texture2D _yTexture, _uTexture, _vTexture;
    private GCHandle _handle;

    private sealed class FrameSession
    {
        public readonly int Width, Height, ChromaWidth, ChromaHeight;
        public readonly int LumaSize, ChromaSize, Size;
        public readonly IntPtr[] Buffers = new IntPtr[3];

        public FrameSession(int width, int height)
        {
            Width = width;
            Height = height;
            ChromaWidth = (width + 1) / 2;
            ChromaHeight = (height + 1) / 2;
            LumaSize = width * height;
            ChromaSize = ChromaWidth * ChromaHeight;
            Size = LumaSize + 2 * ChromaSize;
            for (int i = 0; i < Buffers.Length; i++)
                Buffers[i] = Marshal.AllocHGlobal(Size);
        }

        public void Free()
        {
            for (int i = 0; i < Buffers.Length; i++)
            {
                Marshal.FreeHGlobal(Buffers[i]);
                Buffers[i] = IntPtr.Zero;
            }
        }
    }

    private void Awake()
    {
        didAwake = true;
        try
        {
            var shader = Shader.Find(SHADER_NAME);
            if (shader == null)
                throw new InvalidOperationException($"shader '{SHADER_NAME}' is missing");
            _material = new Material(shader);

            MediaPlayer = new MediaPlayer(VLCMediaPlayer.LibVLC);

            // Before any media: the next video output libVLC opens reads these.
            _handle = GCHandle.Alloc(this);
            SetFormatCallbacks(MediaPlayer.NativeReference, FormatCallback, CleanupCallback);
            SetCallbacks(MediaPlayer.NativeReference, LockCallback, IntPtr.Zero, DisplayCallback,
                GCHandle.ToIntPtr(_handle));
        }
        catch (Exception ex)
        {
            YargLogger.LogFormatWarning("[VlcCpuMediaPlayer] Couldn't create the libVLC player: {0}", ex);
            MediaPlayer?.Dispose();
            MediaPlayer = null;
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }

    /// <summary>Opens and parses the media, then plays it, as VLCMediaPlayer.OpenAsync does.</summary>
    public async Task OpenAsync(string path, params string[] options)
    {
        MediaPlayer.Media?.Dispose();

        var uri = new Uri(path.Trim('"'));
        var media = new Media(uri, options ?? Array.Empty<string>());
        try
        {
            await media.ParseAsync(VLCMediaPlayer.LibVLC,
                uri.IsFile ? MediaParseOptions.ParseLocal : MediaParseOptions.ParseNetwork);

            using var subItems = media.SubItems;
            using var selected = subItems?.FirstOrDefault();
            MediaPlayer.Media = selected ?? media;
        }
        finally
        {
            media.Dispose();
        }

        Play();
    }

    public void Play() => MediaPlayer.Play();

    // SetPause, not VLCMediaPlayer.Pause's libvlc_media_player_pause, which toggles: on a player
    // that hasn't reached Playing yet it starts playback instead.
    public void Pause() => MediaPlayer.SetPause(true);

    public void Stop() => MediaPlayer?.Stop();

    public void SetTime(long milliseconds) => MediaPlayer.SeekTo(TimeSpan.FromMilliseconds(milliseconds));

    private void Update()
    {
        bool resized = false;
        lock (_frameLock)
        {
            if (_session == null || !_fresh)
                return;

            (_ready, _read) = (_read, _ready);
            _fresh = false;

            if (OutputTexture == null || OutputTexture.width != _session.Width ||
                OutputTexture.height != _session.Height)
            {
                DestroyTextures();
                OutputTexture = new RenderTexture(_session.Width, _session.Height, 0, RenderTextureFormat.ARGB32);
                OutputTexture.Create();
                _yTexture = CreatePlane(_session.Width, _session.Height);
                _uTexture = CreatePlane(_session.ChromaWidth, _session.ChromaHeight);
                _vTexture = CreatePlane(_session.ChromaWidth, _session.ChromaHeight);
                _material.SetTexture(U_TEX, _uTexture);
                _material.SetTexture(V_TEX, _vTexture);
                resized = true;
            }

            IntPtr frame = _session.Buffers[_read];
            _yTexture.LoadRawTextureData(frame, _session.LumaSize);
            _uTexture.LoadRawTextureData(frame + _session.LumaSize, _session.ChromaSize);
            _vTexture.LoadRawTextureData(frame + _session.LumaSize + _session.ChromaSize, _session.ChromaSize);
        }

        _yTexture.Apply(updateMipmaps: false);
        _uTexture.Apply(updateMipmaps: false);
        _vTexture.Apply(updateMipmaps: false);
        DrawToOutput();

        // Outside the lock: handlers call back into libVLC.
        if (resized)
            OnTextureResized?.Invoke(OutputTexture);
    }

    // The planes hold the picture top row first. Graphics.DrawTexture maps v=0 to the rect's top
    // edge under a y-down matrix, so drawing them with v flipped leaves OutputTexture upright.
    private void DrawToOutput()
    {
        var previous = RenderTexture.active;
        Graphics.SetRenderTarget(OutputTexture);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, OutputTexture.width, OutputTexture.height, 0);
        Graphics.DrawTexture(new Rect(0, 0, OutputTexture.width, OutputTexture.height), _yTexture,
            new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0, _material);
        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    // Linear: the shader reads the raw 8-bit samples and does its own conversion.
    private static Texture2D CreatePlane(int width, int height) =>
        new(width, height, TextureFormat.R8, mipChain: false, linear: true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

    private void OnDestroy()
    {
        if (MediaPlayer != null)
        {
            // Release closes the video output, which runs CleanupCallback before Dispose returns,
            // so no callback can use the handle after it is freed.
            MediaPlayer.Stop();
            MediaPlayer.Dispose();
            MediaPlayer = null;
        }

        if (_handle.IsAllocated)
            _handle.Free();

        lock (_frameLock)
        {
            _session?.Free();
            _session = null;
        }

        DestroyTextures();
        if (_material != null)
            Destroy(_material);
    }

    private void DestroyTextures()
    {
        if (OutputTexture == null)
            return;

        OnTextureResized?.Invoke(null);
        if (RenderTexture.active == OutputTexture)
            RenderTexture.active = null;
        Destroy(OutputTexture);
        Destroy(_yTexture);
        Destroy(_uTexture);
        Destroy(_vTexture);
        OutputTexture = null;
        _yTexture = _uTexture = _vTexture = null;
    }

    // ─── libVLC's vmem callbacks: libVLC threads, no Unity API calls ───

    // LibVLCSharp's MediaPlayer.SetVideoFormatCallbacks can't be used: it declares the cleanup
    // callback as taking `ref IntPtr`, but libVLC passes the opaque pointer itself, so every video
    // output close throws in its trampoline and the cleanup never runs. These are libVLC's own
    // signatures; the libvlc import binds through VlcLibraryLoader's dllmap entry.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate uint FormatCb(IntPtr* opaque, byte* chroma, uint* widths, uint* heights,
        uint* pitches, uint* lines);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CleanupCb(IntPtr opaque);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate IntPtr LockCb(IntPtr opaque, IntPtr* planes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DisplayCb(IntPtr opaque, IntPtr picture);

    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_video_set_format_callbacks")]
    private static extern void SetFormatCallbacks(IntPtr player, FormatCb setup, CleanupCb cleanup);

    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_video_set_callbacks")]
    private static extern void SetCallbacks(IntPtr player, LockCb lockCb, IntPtr unlockCb, DisplayCb displayCb,
        IntPtr opaque);

    // Static and held in fields: libVLC keeps the pointers for the player's lifetime.
    private static readonly unsafe FormatCb FormatCallback = OnFormat;
    private static readonly CleanupCb CleanupCallback = OnCleanup;
    private static readonly unsafe LockCb LockCallback = OnLock;
    private static readonly DisplayCb DisplayCallback = OnDisplay;

    private static VlcCpuMediaPlayer FromOpaque(IntPtr opaque) =>
        (VlcCpuMediaPlayer) GCHandle.FromIntPtr(opaque).Target;

    // widths[0]/heights[0] are the decoded size, which can include alignment padding;
    // widths[1]/heights[1] are the visible size, the picture actually shown. I420 is what libVLC's
    // software decoders produce, so libVLC copies their planes without converting them.
    [MonoPInvokeCallback(typeof(FormatCb))]
    private static unsafe uint OnFormat(IntPtr* opaque, byte* chroma, uint* widths, uint* heights,
        uint* pitches, uint* lines)
    {
        var self = FromOpaque(*opaque);
        int width = (int) (widths[1] != 0 ? widths[1] : widths[0]);
        int height = (int) (heights[1] != 0 ? heights[1] : heights[0]);
        if (width == 0 || height == 0)
            return 0;

        lock (self._frameLock)
        {
            (width, height) = FitInside(width, height, self._maxWidth, self._maxHeight);

            chroma[0] = (byte) 'I';
            chroma[1] = (byte) '4';
            chroma[2] = (byte) '2';
            chroma[3] = (byte) '0';
            widths[0] = (uint) width;
            heights[0] = (uint) height;

            self._session?.Free();
            var session = self._session = new FrameSession(width, height);
            self._fresh = false;

            pitches[0] = (uint) session.Width;
            lines[0] = (uint) session.Height;
            pitches[1] = pitches[2] = (uint) session.ChromaWidth;
            lines[1] = lines[2] = (uint) session.ChromaHeight;
        }

        // The picture count; 0 would fail the output.
        return 1;
    }

    // Scaled down to fit inside the limit at the same aspect; even, so the chroma planes divide it
    // exactly.
    private static (int, int) FitInside(int width, int height, int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0 || (width <= maxWidth && height <= maxHeight))
            return (width, height);

        float scale = Mathf.Min((float) maxWidth / width, (float) maxHeight / height);
        return (Mathf.Max(2, Mathf.RoundToInt(width * scale) & ~1), Mathf.Max(2, Mathf.RoundToInt(height * scale) & ~1));
    }

    [MonoPInvokeCallback(typeof(CleanupCb))]
    private static void OnCleanup(IntPtr opaque)
    {
        var self = FromOpaque(opaque);
        lock (self._frameLock)
        {
            self._session?.Free();
            self._session = null;
            self._fresh = false;
        }
    }

    [MonoPInvokeCallback(typeof(LockCb))]
    private static unsafe IntPtr OnLock(IntPtr opaque, IntPtr* planes)
    {
        var self = FromOpaque(opaque);
        lock (self._frameLock)
        {
            // libVLC only locks between OnFormat and OnCleanup, so there is always a session.
            var session = self._session;
            IntPtr frame = session.Buffers[self._write];
            planes[0] = frame;
            planes[1] = frame + session.LumaSize;
            planes[2] = frame + session.LumaSize + session.ChromaSize;
        }
        return IntPtr.Zero;
    }

    [MonoPInvokeCallback(typeof(DisplayCb))]
    private static void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        var self = FromOpaque(opaque);
        lock (self._frameLock)
        {
            (self._write, self._ready) = (self._ready, self._write);
            self._fresh = true;
        }
    }
}

#endif
