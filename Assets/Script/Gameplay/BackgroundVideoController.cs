using System;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Keeps a song's background video in step with the song: start, pause, resume, seeks, speed
    /// changes and the end of the video.
    /// </summary>
    public sealed class BackgroundVideoController
    {
        private readonly GameManager _gameManager;
        private readonly YargVideoPlayer _videoPlayer;
        // From the song folder, so it follows the song's position. Otherwise it just loops.
        private readonly bool _followsSong;
        // The fade overlay over a plain-video background; null for a yarground's embedded screen,
        // which the venue reveals itself.
        private readonly Image _curtain;

        /// <summary>Whether Update has work to do.</summary>
        public bool Enabled { get; private set; } = true;

        private GameManager GameManager => _gameManager;

        public BackgroundVideoController(GameManager gameManager, YargVideoPlayer videoPlayer,
            bool followsSong, Image curtain)
        {
            _gameManager = gameManager;
            _videoPlayer = videoPlayer;
            _followsSong = followsSong;
            _curtain = curtain;

            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.seekCompleted += OnVideoSeeked;
        }

        private bool _videoStarted = false;
        private bool _videoSeeking = false;

        // SongRunner counts pause overrides, and a re-seek before the first lands fires only one
        // seekCompleted, so a second override would never be released and the song would stay
        // paused. Hold at most one, released when the latest seek lands.
        private bool _holdingSongForSeek;


        // Pictures the decoder must deliver after the song-start seek before the curtain lifts.
        private const long REVEAL_MIN_FRAMES = 3;
        private bool _videoRevealed;
        private bool _videoEnding;
        private long _revealFrameBaseline;

        // These values are relative to the video, not to song time!
        // A negative start time will delay when the video starts, a positive one will set the video position
        // to that value when starting playback at the start of a song.
        private double _videoStartTime;

        // Wall-clock seconds the video is aimed ahead of the song; see YargVideoPlayer.pipelineLeadSeconds.
        private double _videoLeadSeconds;

        // Pausing parks the video on the frame the resume will rewind to, so resuming is a Play
        // rather than a seek: a seek from a running song lands late by however long the decoder
        // takes to reach the target from its keyframe. See TryParkForResume.
        //
        // Seeking: the park seek is in flight, video still playing. Parked: landed and paused.
        // Releasing: the song has resumed; Play once it reaches the parked frame.
        private enum ParkState { None, Seeking, Parked, Releasing }
        private ParkState _parkState;
        private double _parkedAt;

        // Seeks are issued at rate 1 (see SeekVideo); a seek on a running video takes the song's
        // rate back once it lands.
        private bool _applyRateOnSeekLanded;

        // A speed step on a playing video re-parks it once the steps settle (see SetSpeed).
        private float _reparkAtUnscaledTime = float.NaN;
        private bool _releaseParkOnLanding;
        private const float SPEED_STEP_SETTLE_SECONDS = 0.3f;
        private const double REPARK_MARGIN_SECONDS = 0.4;

        // End time cannot be negative; a negative value means it is not set.
        private double _videoEndTime;

        public void Update()
        {
            if (_videoSeeking)
                return;

            double time = GameManager.GetVideoPlaybackTime();

            if (_parkState == ParkState.Releasing)
                ReleaseParkedVideo(time);

            if (!float.IsNaN(_reparkAtUnscaledTime) && Time.unscaledTime >= _reparkAtUnscaledTime)
            {
                _reparkAtUnscaledTime = float.NaN;
                ReparkForSpeed(time);
            }

            // Start video
            if (!_videoStarted)
            {
                // Don't start playing the video until the start of the song
                if (time < 0.0)
                    return;

                // Delay until the start time is reached
                if (_followsSong && VideoTargetFor(time) < 0.0)
                    return;

                if (_videoEndTime == 0)
                    return;

                _videoStarted = true;

                // At a non-100% speed a held load-time seek (practice entry) has parked the video
                // on this frame; releasing it takes the rate while paused. A running re-seek would
                // have to change the rate on a playing video instead.
                bool releaseLoadPark = _followsSong && _videoPlayer.usingVlc &&
                    !Mathf.Approximately(GameManager.SongSpeed, 1f) &&
                    _parkState is ParkState.Parked or ParkState.Releasing;

                if (releaseLoadPark)
                {
                    if (_parkState == ParkState.Parked)
                        ArmParkRelease();
                }
                else
                {
                    _parkState = ParkState.None;
                    SetVideoPlaying(true);
                }

                if (_followsSong && !releaseLoadPark)
                {
                    // Re-seek even when the player already sits on the target: at 100% a parked
                    // start (played rather than re-seeked) settles 20-40ms later on videos that
                    // start on a keyframe. Must follow the Play above, because a seek issued while
                    // paused is dropped.
                    double startTarget = VideoTargetFor(time);
                    SeekVideo(startTarget);
                    _applyRateOnSeekLanded = true;
                }

                _revealFrameBaseline = _videoPlayer.framesDelivered;

                // A song video keeps Update running even with no end time (looping), since
                // park releases and speed re-parks happen here; the end checks below are false
                // for a NaN end time.
                if (!_followsSong)
                {
                    // Update stops here, so there is no later chance to reveal.
                    RevealVideoWhenPlaying(force: true);

                    Enabled = false;
                    return;
                }
            }

            RevealVideoWhenPlaying(force: false);

            // End video when reaching the specified end time, behind the curtain: what a stopped
            // player leaves on the texture is undefined.
            double videoPosition = time + _videoStartTime;
            if (!_videoEnding && videoPosition >= _videoEndTime - BackgroundManager.FADE_DURATION * GameManager.SongSpeed)
                BeginVideoFadeOut();

            if (videoPosition >= _videoEndTime)
            {
                _videoPlayer.Stop();
                _videoPlayer.playerEnabled = false;
                Enabled = false;
            }
        }

        private void BeginVideoFadeOut()
        {
            _videoEnding = true;

            // Plain-video backgrounds only (no curtain on a yarground's screen), as for the reveal.
            if (_curtain != null)
                _curtain.CrossFadeAlpha(1f, BackgroundManager.FADE_DURATION, true);
        }

        // Some video player properties don't work correctly until
        // it's finished preparing, such as the length
        private void OnVideoPrepared(YargVideoPlayer player)
        {
            // Start time is considered set if it is greater than 25 ms in either direction
            // End time is only set if it is greater than 0
            // Video will only loop if its length is less than 85% of the song's length
            const double startTimeThreshold = 0.025;
            const double endTimeThreshold = 0;
            const double dontLoopThreshold = 0.85;

            if (_followsSong && !GameManager.Song.VideoLoop)
            {
                _videoStartTime = GameManager.Song.VideoStartTimeSeconds;
                _videoEndTime = GameManager.Song.VideoEndTimeSeconds;
                _videoLeadSeconds = player.pipelineLeadSeconds;

                // Clamped: a negative start time delays when the video starts; it does not name a
                // position before the file begins.
                SeekVideo(Math.Max(VideoTargetFor(0.0), 0.0));
                if (!player.usingVlc)
                    player.playbackSpeed = GameManager.SongSpeed;

                // Only loop the video if it's not around the same length as the song
                if (Math.Abs(_videoStartTime) < startTimeThreshold &&
                    _videoEndTime <= endTimeThreshold &&
                    player.length < GameManager.SongLength * dontLoopThreshold)
                {
                    player.isLooping = true;
                    _videoEndTime = double.NaN;
                }
                else
                {
                    player.isLooping = false;
                    if (_videoEndTime <= 0)
                    {
                        // Unspecified: the video's own end, or the song's if the video is longer,
                        // which it otherwise plays on past.
                        _videoEndTime = Math.Min(player.length, GameManager.SongLength + _videoStartTime);
                    }
                }
            }
            else
            {
                _videoStartTime = 0;
                _videoEndTime = double.NaN;
                player.isLooping = true;
            }

            // The player arrives here still playing, and the start-position seek above only lands
            // because of that -- a seek issued while paused is dropped. Pause now, after the seek,
            // or the video runs ahead of its mark until Update()'s start block.
            SetVideoPlaying(false);

        }

        public void SetTime(double songTime, bool waitForSeek = true)
        {
            // Don't seek videos that aren't from the song
            if (!_followsSong)
                return;

            _parkState = ParkState.None;
            _reparkAtUnscaledTime = float.NaN;
            _releaseParkOnLanding = false;

            // A seek back into a video that has faded out (practice) reveals it again.
            if (_videoEnding)
            {
                _videoEnding = false;
                _videoRevealed = false;
                _revealFrameBaseline = _videoPlayer.framesDelivered;
            }

            double videoTime = VideoTargetFor(songTime);
            if (videoTime < 0f) // Seeking before video start
            {
                EndHeldSeek();
                Enabled = true;
                _videoPlayer.playerEnabled = true;
                _videoStarted = false;
                _videoPlayer.Stop();
            }
            else if (videoTime >= _videoPlayer.length) // Seeking after video end
            {
                EndHeldSeek();
                Enabled = false;
                _videoPlayer.playerEnabled = false;
                _videoPlayer.Stop();
            }
            else
            {
                Enabled = false; // Temp disable
                _videoPlayer.playerEnabled = true;

                // Hack to ensure the video stays synced to the audio
                _videoSeeking = true; // Signaling flag; must come first
                bool videoWasPaused = _videoPlayer.isPaused;

                if (waitForSeek && SettingsManager.Settings.WaitForSongVideo.Value &&
                    !_holdingSongForSeek)
                {
                    _holdingSongForSeek = true;
                    GameManager.OverridePause();
                }

                // A seek issued while paused is dropped, so play across it; OnVideoSeeked
                // settles the video back into the song's state once it lands.
                if (videoWasPaused)
                    SetVideoPlaying(true);

                SeekVideo(videoTime);
                _applyRateOnSeekLanded = true;
            }
        }

        private void OnVideoSeeked(YargVideoPlayer player)
        {
            if (_parkState == ParkState.Seeking)
            {
                _parkState = ParkState.Parked;
                SetVideoPlaying(false);

                // A speed re-park releases on its own; a pause's park waits for the resume.
                if (_releaseParkOnLanding)
                {
                    _releaseParkOnLanding = false;
                    ArmParkRelease();
                }

                return;
            }

            // A held seek (SetTime) at a non-100% speed parks instead of taking the rate while
            // playing: changing the rate of a playing video jumps it by (time since its new clock
            // context began) x delta-rate, which measured 50-100ms even right after landing.
            bool parkHeldSeek = _videoSeeking && _followsSong && _videoPlayer.usingVlc &&
                !Mathf.Approximately(GameManager.SongSpeed, 1f);

            // Otherwise its first picture has only just started libVLC's new clock context, so the
            // rate change costs little.
            if (_applyRateOnSeekLanded)
            {
                _applyRateOnSeekLanded = false;
                if (!parkHeldSeek)
                    ApplyVideoRate();
            }

            if (!_videoSeeking)
                return;

            ReleaseSongHold();

            if (parkHeldSeek)
            {
                // Released by ReleaseParkedVideo, which applies the rate while paused, once the song
                // reaches the picture; if the song is still paused, the resume arms it instead.
                SetVideoPlaying(false);
                _parkState = ParkState.Parked;
                if (!GameManager.Paused)
                    ArmParkRelease();
            }
            else
            {
                // Follow the song, not the video's state before the seek: a resume issued while the
                // seek was in flight skipped the video (SetPaused ignores it mid-seek), and a pause
                // could have landed meanwhile too.
                SetVideoPlaying(!GameManager.Paused);
            }

            Enabled = true;
            _videoSeeking = false;
        }

        private void ReleaseSongHold()
        {
            if (!_holdingSongForSeek)
                return;

            _holdingSongForSeek = false;
            GameManager.OverrideResume();
        }

        // A held seek still in flight when SetTime moves outside the video: its landing no longer
        // applies, so release the song now rather than on a seekCompleted that settles nothing.
        private void EndHeldSeek()
        {
            ReleaseSongHold();
            _videoSeeking = false;
            _applyRateOnSeekLanded = false;
        }

        // The video position to aim at for a song time. The lead is wall-clock, so it scales with
        // song speed when expressed in video time.
        private double VideoTargetFor(double songTime)
        {
            return songTime + _videoStartTime + _videoLeadSeconds * GameManager.SongSpeed;
        }

        // Lifts the curtain once the decoder has delivered pictures, so a slow spin-up is hidden
        // rather than shown. Latched.
        private void RevealVideoWhenPlaying(bool force)
        {
            // Not while ending: the reveal would cancel the fade-out it runs every frame against.
            if (_videoRevealed || _videoEnding)
                return;

            if (!force && _videoPlayer.framesDelivered - _revealFrameBaseline < REVEAL_MIN_FRAMES)
                return;

            _videoRevealed = true;

            // Plain-video backgrounds only: a yarground's embedded screen shares this manager but
            // gets its reveal from ShowVenue().
            if (_curtain != null)
                _curtain.CrossFadeAlpha(0f, BackgroundManager.FADE_DURATION, true);
        }

        // Every play/pause goes through here. Several independent paths drive the player, and
        // Play/Pause are applied asynchronously in issue order, so the last one issued wins rather
        // than the last intended.
        private void SetVideoPlaying(bool playing)
        {
            if (playing)
            {
                _videoPlayer.Play();
            }
            else
            {
                _videoPlayer.Pause();
            }
        }

        // Called as the song resumes from a pause's rewind. A parked video plays once the song
        // reaches its frame; otherwise this falls back to the song-start block's Play-then-seek,
        // which lands late by the decode time from the target's keyframe.
        public void ResyncVideoToSong()
        {
            if (_curtain == null || !_followsSong ||
                !_videoStarted || _videoSeeking || !_videoPlayer.playerEnabled)
            {
                return;
            }

            double time = GameManager.GetVideoPlaybackTime();
            double target = VideoTargetFor(time);

            // Outside the video's own span, the start block and end check already own it.
            if (target < 0 || target >= _videoPlayer.length)
                return;

            // A landed park already holds (about) this frame: play it when the song gets there.
            // Measured from the picture on screen, which is what playback resumes from; the paused
            // player's reported time runs ahead of it.
            double parkedAt = _videoPlayer.resumePictureTime;

            if (_parkState == ParkState.Parked && Math.Abs(parkedAt - target) < PARK_MAX_MISMATCH_SECONDS)
            {
                _parkedAt = parkedAt;
                _parkState = ParkState.Releasing;
                return;
            }

            _parkState = ParkState.None;

            SetVideoPlaying(true);
            SeekVideo(target);
            _applyRateOnSeekLanded = true;
        }

        // A park more than this far from the resume target is from a different rewind.
        private const double PARK_MAX_MISMATCH_SECONDS = 0.25;

        // Seeks while still playing -- a seek issued while paused is dropped -- and pauses once it
        // lands, in OnVideoSeeked. Where it stops needn't be exact: the release reads back the
        // picture actually parked on and waits for the song to reach it.
        private bool TryParkForResume(double playbackTime)
        {
            if (_curtain == null || !_followsSong ||
                !_videoStarted || _videoSeeking || !_videoPlayer.playerEnabled || !_videoPlayer.usingVlc)
            {
                return false;
            }

            double target = VideoTargetFor(playbackTime);
            if (target < 0 || target >= _videoPlayer.length)
                return false;

            _parkState = ParkState.Seeking;
            SetVideoPlaying(true);
            SeekVideo(target);
            return true;
        }

        private void ArmParkRelease()
        {
            _parkedAt = _videoPlayer.resumePictureTime;
            _parkState = ParkState.Releasing;
        }

        private void ReleaseParkedVideo(double time)
        {
            // The picture the paused video will resume from; it can still advance a frame or two
            // after the pause is issued.
            double picture = _videoPlayer.presentedPictureTime;
            if (!double.IsNaN(picture))
                _parkedAt = picture;

            if (GameManager.Paused || VideoTargetFor(time) < _parkedAt)
                return;

            _parkState = ParkState.None;

            // Paused, with the next picture starting a new clock context: the one moment a rate
            // change costs nothing.
            ApplyVideoRate();
            SetVideoPlaying(true);
        }

        // A seek lands on target only when issued at rate 1; at rate r it lands off by about
        // target x (r - 1). So every VLC seek drops to 1, and ApplyVideoRate restores the song's
        // rate once the seek has landed or the parked video is released.
        private void SeekVideo(double target)
        {
            if (_videoPlayer.usingVlc)
                _videoPlayer.playbackSpeed = 1f;

            _videoPlayer.time = target;
        }

        private void ApplyVideoRate()
        {
            float speed = GameManager.SongSpeed;
            if (Mathf.Approximately(_videoPlayer.playbackSpeed, speed))
                return;

            _videoPlayer.playbackSpeed = speed;
        }

        // Seeks a little ahead at rate 1, parks there, and releases at the new rate when the song
        // arrives. The video holds that frame for about the margin.
        private void ReparkForSpeed(double time)
        {
            if (GameManager.Paused || !_videoStarted || _videoSeeking || _parkState != ParkState.None ||
                _videoPlayer.isPaused)
            {
                return;
            }

            double target = VideoTargetFor(time + REPARK_MARGIN_SECONDS * GameManager.SongSpeed);
            if (target < 0 || target >= _videoPlayer.length)
                return;

            _parkState = ParkState.Seeking;
            _releaseParkOnLanding = true;
            SeekVideo(target);
        }

        public void SetSpeed(float speed)
        {
            if (!_followsSong || !_videoPlayer.usingVlc)
            {
                _videoPlayer.playbackSpeed = speed;
                return;
            }

            // Never on a playing VLC video: its rate change jumps the picture by (time since
            // the last seek) x delta-rate, seconds late in a section. A paused, parked or
            // seeking video takes the new rate at its release or landing; a playing one is
            // re-parked once the 0.05 steps stop coming.
            if (_videoStarted && !_videoPlayer.isPaused && _parkState == ParkState.None && !_videoSeeking)
                _reparkAtUnscaledTime = Time.unscaledTime + SPEED_STEP_SETTLE_SECONDS;
        }

        /// <param name="parkAtPlaybackTime">
        /// On pause, the video playback time the resume will rewind to, when it will rewind.
        /// </param>
        public void SetPaused(bool paused, double? parkAtPlaybackTime = null)
        {
            if (paused && parkAtPlaybackTime.HasValue && TryParkForResume(parkAtPlaybackTime.Value))
                return;

            // The park release owns the Play: it waits for the song to reach the parked frame.
            if (!paused && _parkState == ParkState.Releasing)
                return;

            // A held seek that landed while the song was paused (e.g. a section change from the
            // menu): release it rather than play it, so it takes the rate while still paused.
            if (!paused && _parkState == ParkState.Parked && _videoStarted && !_videoSeeking)
            {
                ArmParkRelease();
                return;
            }

            if (paused)
            {
                _parkState = ParkState.None;
                _reparkAtUnscaledTime = float.NaN;
                _releaseParkOnLanding = false;
            }

            // Pause/unpause video
            if (_videoPlayer.playerEnabled && _videoStarted && !_videoSeeking)
            {
                SetVideoPlaying(!paused);

                // The speed changed while paused: re-align at the new rate rather than change it
                // on the playing video.
                if (!paused && _followsSong && _videoPlayer.usingVlc &&
                    !Mathf.Approximately(_videoPlayer.playbackSpeed, GameManager.SongSpeed))
                {
                    _reparkAtUnscaledTime = Time.unscaledTime;
                }
            }

        }
    }
}
