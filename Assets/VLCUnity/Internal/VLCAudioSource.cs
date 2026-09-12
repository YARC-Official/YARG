using System;
using System.Runtime.InteropServices;
using System.Threading;
using LibVLCSharp;
using UnityEngine;

/// <summary>
/// Basic implementation for outputting VLC audio through a Unity Audio Source.
/// With this implementation, you will gain ability to have 3D audio, AudioSource effects, and anything else that
/// AudioSources support.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VLCAudioSource : MonoBehaviour
{
    private const int MaximumSpaceWaitMilliseconds = 100;
    private const int LateBlockThresholdMilliseconds = 100;

    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "libvlc_clock")]
    private static extern long LibVlcClock();

    [Header("Audio Format")]
    [Min(8000)] public int SampleRate = 48000;
    [Min(1)] public int Channels = 2;

    [Header("Buffering (milliseconds)")]
    [Min(20)] public int InitialBufferMilliseconds = 150;
    [Min(20)] public int RebufferMilliseconds = 250;
    [Min(50)] public int ProducerLeadMilliseconds = 250;
    [Min(500)] public int BufferCapacityMilliseconds = 2000;

    public long UnderrunCount => Interlocked.Read(ref underrunCount);
    public long OverflowCount => Interlocked.Read(ref overflowCount);
    public long DroppedFrameCount => Interlocked.Read(ref droppedFrameCount);
    public long LateBlockCount => Interlocked.Read(ref lateBlockCount);
    public long ProducerWaitMilliseconds =>
        Interlocked.Read(ref producerWaitMicroseconds) / 1000L;
    public int BufferedMilliseconds => FramesToMilliseconds(GetBufferedFrames());

    private readonly AutoResetEvent producerWake = new(false);

    private MediaPlayer attachedMediaPlayer;
    private AudioSource audioSource;
    private AudioClip audioClip;

    // Ring positions and capacity are measured in complete sample frames.
    private int frameCapacity;
    private int frameMask;
    private float[] buffer;
    private GCHandle bufferHandle;
    private IntPtr bufferPtr;
    private long writeFrame;
    private long readFrame;

    private int initialBufferFrames;
    private int rebufferFrames;
    private int primeThresholdFrames;
    private int primed;
    private int flushPending;
    private long flushReadFrame;
    private int stopped = 1;
    private int activeProducerCallbacks;

    private long underrunCount;
    private long overflowCount;
    private long droppedFrameCount;
    private long lateBlockCount;
    private long producerWaitMicroseconds;

    public void Attach(MediaPlayer mediaPlayer)
    {
        if (mediaPlayer == null)
            throw new ArgumentNullException(nameof(mediaPlayer));

        ReleaseAudioBuffer();
        InitializeBuffer();

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        audioClip = AudioClip.Create(
            "VLCAudio", frameCapacity, Channels, SampleRate, true, OnAudioRead);
        audioSource.clip = audioClip;

        attachedMediaPlayer = mediaPlayer;
        attachedMediaPlayer.Playing += OnMediaPlayerPlaying;
        attachedMediaPlayer.Stopping += OnMediaPlayerStopping;
        attachedMediaPlayer.SetAudioFormat(
            "FL32", (uint)SampleRate, (uint)Channels);
        attachedMediaPlayer.SetAudioCallbacks(
            OnAudioCallback, null, null, OnFlush, null);

        StartProducer();
        audioSource.Play();
    }

    private void OnDestroy()
    {
        ReleaseAudioBuffer();
    }

    private void OnMediaPlayerPlaying(object sender, EventArgs eventArgs)
    {
        if (ReferenceEquals(sender, attachedMediaPlayer))
            StartProducer();
    }

    private void OnMediaPlayerStopping(object sender, EventArgs eventArgs)
    {
        if (ReferenceEquals(sender, attachedMediaPlayer))
            StopProducer();
    }

    private void StartProducer()
    {
        long currentWrite = Volatile.Read(ref writeFrame);
        Volatile.Write(ref flushReadFrame, currentWrite);
        Volatile.Write(ref flushPending, 1);
        Volatile.Write(ref primed, 0);
        Volatile.Write(ref stopped, 0);
    }

    private void StopProducer()
    {
        Volatile.Write(ref stopped, 1);
        Volatile.Write(ref primed, 0);
        producerWake.Set();
    }

    private void InitializeBuffer()
    {
        SampleRate = Math.Max(8000, SampleRate);
        Channels = Math.Max(1, Channels);

        initialBufferFrames = MillisecondsToFrames(
            Math.Max(20, InitialBufferMilliseconds));
        rebufferFrames = MillisecondsToFrames(
            Math.Max(20, RebufferMilliseconds));

        int requestedCapacity = MillisecondsToFrames(
            Math.Max(500, BufferCapacityMilliseconds));
        int minimumCapacity = Math.Max(SampleRate, rebufferFrames * 4);
        frameCapacity = Mathf.NextPowerOfTwo(
            Math.Max(requestedCapacity, minimumCapacity));
        frameMask = frameCapacity - 1;

        buffer = new float[frameCapacity * Channels];
        bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        bufferPtr = bufferHandle.AddrOfPinnedObject();

        Volatile.Write(ref writeFrame, 0);
        ResetConsumer(0);
        Volatile.Write(ref flushPending, 0);
        Volatile.Write(ref flushReadFrame, 0);
        Volatile.Write(ref stopped, 1);

        Interlocked.Exchange(ref underrunCount, 0);
        Interlocked.Exchange(ref overflowCount, 0);
        Interlocked.Exchange(ref droppedFrameCount, 0);
        Interlocked.Exchange(ref lateBlockCount, 0);
        Interlocked.Exchange(ref producerWaitMicroseconds, 0);
    }

    private void ReleaseAudioBuffer()
    {
        StopProducer();
        audioSource?.Stop();
        DetachMediaPlayer();

        SpinWait.SpinUntil(
            () => Volatile.Read(ref activeProducerCallbacks) == 0);

        bufferPtr = IntPtr.Zero;

        if (bufferHandle.IsAllocated)
            bufferHandle.Free();

        if (audioClip != null)
        {
            Destroy(audioClip);
            audioClip = null;
        }

        if (audioSource != null)
            audioSource.clip = null;

        buffer = null;
    }

    private void DetachMediaPlayer()
    {
        MediaPlayer mediaPlayer = attachedMediaPlayer;
        attachedMediaPlayer = null;

        if (mediaPlayer == null)
            return;

        mediaPlayer.Playing -= OnMediaPlayerPlaying;
        mediaPlayer.Stopping -= OnMediaPlayerStopping;
    }

    private unsafe void OnAudioCallback(
        IntPtr _, IntPtr samples, uint count, long pts)
    {
        Interlocked.Increment(ref activeProducerCallbacks);
        try
        {
            if (Volatile.Read(ref stopped) == 1 ||
                bufferPtr == IntPtr.Zero ||
                samples == IntPtr.Zero ||
                count == 0)
                return;

            int frameCount = (int)Math.Min(count, (uint)int.MaxValue);
            PaceProducerToPts(pts);

            if (Volatile.Read(ref stopped) == 1)
                return;

            if (!WaitForRingSpace(frameCount, out long destinationFrame))
            {
                Interlocked.Increment(ref overflowCount);
                Interlocked.Add(ref droppedFrameCount, frameCount);
                return;
            }

            CopyFrames(
                (float*)samples, destinationFrame, frameCount, intoRing: true);
            Volatile.Write(ref writeFrame, destinationFrame + frameCount);
        }
        finally
        {
            Interlocked.Decrement(ref activeProducerCallbacks);
        }
    }

    private void PaceProducerToPts(long pts)
    {
        if (pts <= 0)
            return;

        long leadMicroseconds =
            Math.Max(50, ProducerLeadMilliseconds) * 1000L;
        long waitStarted = 0;

        while (Volatile.Read(ref stopped) == 0)
        {
            long now = LibVlcClock();
            long delay = pts - now;

            if (delay <= leadMicroseconds)
            {
                if (delay < -LateBlockThresholdMilliseconds * 1000L)
                    Interlocked.Increment(ref lateBlockCount);
                break;
            }

            if (waitStarted == 0)
                waitStarted = now;

            long remaining = delay - leadMicroseconds;
            int waitMilliseconds = (int)Math.Max(
                1, Math.Min(10, remaining / 1000L));
            producerWake.WaitOne(waitMilliseconds);
        }

        if (waitStarted != 0)
        {
            long waited = Math.Max(0, LibVlcClock() - waitStarted);
            Interlocked.Add(ref producerWaitMicroseconds, waited);
        }
    }

    private bool WaitForRingSpace(int frameCount, out long destinationFrame)
    {
        long deadline =
            LibVlcClock() + MaximumSpaceWaitMilliseconds * 1000L;

        while (Volatile.Read(ref stopped) == 0)
        {
            destinationFrame = Volatile.Read(ref writeFrame);
            long usedFrames = Math.Max(
                0, destinationFrame - Volatile.Read(ref readFrame));
            long writableFrames =
                frameCapacity - Math.Min(usedFrames, frameCapacity);

            if (frameCount <= writableFrames)
                return true;

            long remaining = deadline - LibVlcClock();
            if (remaining <= 0)
                break;

            int waitMilliseconds = (int)Math.Max(
                1, Math.Min(10, (remaining + 999L) / 1000L));
            producerWake.WaitOne(waitMilliseconds);
        }

        destinationFrame = 0;
        return false;
    }

    private unsafe void OnAudioRead(float[] output)
    {
        Array.Clear(output, 0, output.Length);

        if (Volatile.Read(ref stopped) == 1 ||
            bufferPtr == IntPtr.Zero ||
            output.Length == 0 ||
            Channels <= 0)
            return;

        int outputFrames = output.Length / Channels;
        if (outputFrames == 0)
            return;

        ApplyPendingFlush();

        long sourceFrame = Volatile.Read(ref readFrame);
        long availableFrames =
            Volatile.Read(ref writeFrame) - sourceFrame;

        if (Volatile.Read(ref primed) == 0)
        {
            int threshold = Math.Max(
                outputFrames, Volatile.Read(ref primeThresholdFrames));
            if (availableFrames < threshold)
                return;

            Volatile.Write(ref primed, 1);
        }

        if (availableFrames < outputFrames)
        {
            Interlocked.Increment(ref underrunCount);
            Volatile.Write(ref primeThresholdFrames, rebufferFrames);
            Volatile.Write(ref primed, 0);
            return;
        }

        fixed (float* destination = output)
        {
            CopyFrames(
                destination, sourceFrame, outputFrames, intoRing: false);
        }

        Volatile.Write(ref readFrame, sourceFrame + outputFrames);
        producerWake.Set();
    }

    private void ApplyPendingFlush()
    {
        if (Interlocked.Exchange(ref flushPending, 0) == 1)
            ResetConsumer(Volatile.Read(ref flushReadFrame));
    }

    private void OnFlush(IntPtr _, long __)
    {
        Volatile.Write(ref flushReadFrame, Volatile.Read(ref writeFrame));
        Volatile.Write(ref primed, 0);
        Volatile.Write(ref flushPending, 1);
    }

    private void ResetConsumer(long newReadFrame)
    {
        Volatile.Write(ref readFrame, newReadFrame);
        Volatile.Write(ref primeThresholdFrames, initialBufferFrames);
        Volatile.Write(ref primed, 0);
    }

    private unsafe void CopyFrames(
        float* externalBuffer, long ringFrame, int frameCount, bool intoRing)
    {
        int frameIndex = (int)(ringFrame & frameMask);
        int firstFrames = Math.Min(frameCount, frameCapacity - frameIndex);
        int firstSamples = firstFrames * Channels;
        float* ringBuffer = (float*)bufferPtr + frameIndex * Channels;

        CopySamples(externalBuffer, ringBuffer, firstSamples, intoRing);

        int remainingFrames = frameCount - firstFrames;
        if (remainingFrames == 0)
            return;

        CopySamples(
            externalBuffer + firstSamples,
            (float*)bufferPtr,
            remainingFrames * Channels,
            intoRing);
    }

    private static unsafe void CopySamples(
        float* externalBuffer, float* ringBuffer, int sampleCount, bool intoRing)
    {
        long byteCount = sampleCount * (long)sizeof(float);

        if (intoRing)
            Buffer.MemoryCopy(
                externalBuffer, ringBuffer, byteCount, byteCount);
        else
            Buffer.MemoryCopy(
                ringBuffer, externalBuffer, byteCount, byteCount);
    }

    private long GetBufferedFrames()
    {
        return Math.Max(
            0, Volatile.Read(ref writeFrame) - Volatile.Read(ref readFrame));
    }

    private int MillisecondsToFrames(int milliseconds)
    {
        return (int)Math.Max(
            1, SampleRate * (long)milliseconds / 1000L);
    }

    private int FramesToMilliseconds(long frames)
    {
        return SampleRate > 0
            ? (int)(frames * 1000L / SampleRate)
            : 0;
    }
}
