using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Song;

namespace YARG.Audio
{
    public sealed class PreviewPlayer : IDisposable
    {
        private CancellationTokenSource _previewCanceller;
        private PreviewContext _previewContext;
        private bool _disposed;

        /// <summary>
        /// Starts a preview using the song's configured preview markers, or the default preview
        /// window when the song has no usable markers.
        /// </summary>
        /// <remarks>The returned task completes when preview creation finishes, not when playback stops.</remarks>
        public UniTask StartPreview(
            SongEntry entry,
            float volume,
            float speed,
            double delaySeconds,
            double fadeDuration,
            bool enableCensoring)
        {
            return Play(
                entry,
                volume,
                speed,
                delaySeconds,
                fadeDuration,
                enableCensoring,
                startTime: null,
                length: null);
        }

        /// <summary>Starts a preview for an explicit song range, such as a practice section.</summary>
        /// <remarks>The returned task completes when preview creation finishes, not when playback stops.</remarks>
        public UniTask StartPreview(
            SongEntry entry,
            float volume,
            float speed,
            double delaySeconds,
            double fadeDuration,
            bool enableCensoring,
            double startTime,
            double length)
        {
            return Play(
                entry,
                volume,
                speed,
                delaySeconds,
                fadeDuration,
                enableCensoring,
                startTime,
                length);
        }

        /// <summary>
        /// Cancels the current preview. If it has started playing, waits for its fade-out to finish.
        /// A preview still being created observes cancellation and cleans itself up when creation returns.
        /// </summary>
        public async UniTask StopPreview()
        {
            var canceller = _previewCanceller;
            var context = _previewContext;
            _previewCanceller = null;
            _previewContext = null;

            canceller?.Cancel();
            if (context != null)
            {
                await CancelPreview(context, canceller);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var canceller = _previewCanceller;
            var context = _previewContext;
            _previewCanceller = null;
            _previewContext = null;

            try
            {
                canceller?.Cancel();
            }
            finally
            {
                context?.Dispose();

                // If creation is still in flight, its continuation will dispose the cancellation
                // source and any context it produces. Otherwise, release the source here.
                if (context != null || canceller == null)
                {
                    canceller?.Dispose();
                }
            }
        }

        private async UniTask Play(
            SongEntry entry,
            float volume,
            float speed,
            double delaySeconds,
            double fadeDuration,
            bool enableCensoring,
            double? startTime,
            double? length)
        {
            if (startTime.HasValue != length.HasValue)
            {
                throw new ArgumentException("Preview start time and length must be provided together.");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PreviewPlayer));
            }

            var previousCanceller = _previewCanceller;
            var previousContext = _previewContext;
            var canceller = new CancellationTokenSource();

            _previewCanceller = canceller;
            _previewContext = null;

            previousCanceller?.Cancel();
            if (previousContext != null)
            {
                // Don't wait here: let the previous preview fade out alongside the new fade-in.
                _ = CancelPreview(previousContext, previousCanceller);
            }

            PreviewContext context;
            try
            {
                if (startTime.HasValue)
                {
                    context = await PreviewContext.Create(
                        entry,
                        volume,
                        speed,
                        delaySeconds,
                        fadeDuration,
                        enableCensoring,
                        startTime.Value,
                        length.Value,
                        canceller.Token);
                }
                else
                {
                    context = await PreviewContext.Create(
                        entry,
                        volume,
                        speed,
                        delaySeconds,
                        fadeDuration,
                        enableCensoring,
                        canceller.Token);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while creating song preview!");
                context = null;
            }

            if (context == null)
            {
                if (ReferenceEquals(_previewCanceller, canceller))
                {
                    _previewCanceller = null;
                }

                canceller.Dispose();
                return;
            }

            if (_disposed)
            {
                context.Dispose();
                canceller.Dispose();
                return;
            }

            if (ReferenceEquals(_previewCanceller, canceller) && !canceller.IsCancellationRequested)
            {
                _previewContext = context;
                return;
            }

            // If we made it here, a new request was made while the old preview was loading.
            await CancelPreview(context, canceller);
        }

        private static async UniTask CancelPreview(
            PreviewContext context,
            CancellationTokenSource canceller)
        {
            try
            {
                canceller?.Cancel();
                await context.WaitForCompletionAsync();
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while stopping song preview!");
            }
            finally
            {
                context.Dispose();
                canceller?.Dispose();
            }
        }
    }
}
