using System;
using UnityEngine;
using UnityEngine.UI;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Keeps a background video in step with the song.
    /// </summary>
    /// <remarks>
    /// Lifecycle: prepare (seek to the start position, pause) → start when the song reaches it
    /// (Play, re-seek) → run → end (fade out, stop). Around that:
    /// <list type="bullet">
    /// <item>Pause parks the video on the frame the resume will rewind to; the resume releases it
    /// (Play) when the song reaches that frame. A seek from a running song lands late by the
    /// decoder's catch-up from the preceding keyframe, so resuming never seeks if it can help it.</item>
    /// <item>A held seek (<see cref="SetTime"/>: practice restarts, section changes) can hold the
    /// song paused until the video lands.</item>
    /// <item>A speed step on a playing video re-parks it a little ahead and releases it at the new
    /// rate.</item>
    /// </list>
    /// Every path obeys three libVLC rules:
    /// <list type="number">
    /// <item>A seek issued while paused is dropped, so every seek is issued while playing.</item>
    /// <item>A seek lands on target only when issued at rate 1 (off by about target x (rate - 1)
    /// otherwise), so every seek drops to rate 1 (<see cref="SeekVideo"/>).</item>
    /// <item>Changing the rate of a playing video jumps it by (time since its clock was last
    /// refit) x delta-rate. So the song's rate is applied to a parked, paused video just before
    /// its release, or right after a seek lands, never otherwise.</item>
    /// </list>
    /// The Unity backend shares these paths; parking is VLC-only.
    /// </remarks>
    public sealed class BackgroundVideoController
    {
        // Pictures the decoder must deliver after the song-start seek before the curtain lifts.
        private const long REVEAL_MIN_FRAMES = 3;

        // A park further than this from the resume target is from a different rewind.
        private const double PARK_MAX_MISMATCH_SECONDS = 0.25;

        // Speed steps come 0.05 per keypress; re-park once they stop.
        private const float SPEED_STEP_SETTLE_SECONDS = 0.3f;

        // How far ahead (in song seconds at 1x) a speed re-park seeks. The video holds one frame for
        // about this long, so it must cover the seek's landing.
        private const double REPARK_MARGIN_SECONDS = 0.4;

        private readonly GameManager _gameManager;
        private readonly YargVideoPlayer _videoPlayer;

        // From the song folder, so it follows the song's position. Otherwise it just loops.
        private readonly bool _followsSong;

        // The fade overlay over a plain-video background; null for a yarground's embedded screen,
        // which the venue reveals itself and which is never parked or resynced.
        private readonly Image _curtain;

        private bool _prepared;
        private bool _videoStarted;

        // Relative to the video, not the song. A negative start time delays when the video starts;
        // a positive one is the video position at song time 0.
        private double _videoStartTime;

        // Video position to stop at; NaN for a looping video.
        private double _videoEndTime;

        // Wall-clock seconds the video is aimed ahead of the song; see
        // YargVideoPlayer.pipelineLeadSeconds.
        private double _videoLeadSeconds;

        private bool _videoRevealed;
        private bool _videoEnding;
        private long _revealFrameBaseline;

        // A SetTime seek is in flight. Update and the pause/resume paths leave the player alone
        // until it lands.
        private bool _heldSeekPending;

        // SongRunner counts pause overrides, and a re-seek before the first lands fires only one
        // seekCompleted, so a second override would never be released and the song would stay
        // paused. Hold at most one, released when the latest seek lands.
        private bool _holdingSongForSeek;

        // A running video's seek takes the song's rate back once it lands (rule 3).
        private bool _applyRateOnSeekLanded;

        // None → Seeking: BeginPark (pause, speed re-park). Seeking → Parked: OnVideoSeeked.
        // None → Parked: a held seek landing at non-100% speed.
        // Parked → Releasing: ArmParkRelease (resume, re-park landing, song start).
        // Releasing → None: ReleaseParkedVideo plays it once the song reaches the parked frame.
        // CancelPark returns any state to None.
        private enum ParkState { None, Seeking, Parked, Releasing }
        private ParkState _parkState;
        private double _parkedAt;
        private bool _releaseParkOnLanding;

        private float _reparkAtUnscaledTime = float.NaN;
        private bool _resyncWhenSongResumes;

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

        // The only videos a pause parks or a resume resyncs: a plain song-folder background that
        // has started, with a live player and no held seek in flight.
        private bool IsRealignable => _curtain != null && _followsSong && _videoStarted &&
            !_heldSeekPending && _videoPlayer.playerEnabled;

        // A non-100% song speed on VLC, where a seek on a running video must park rather than
        // take the rate while playing.
        private bool ParksForRate => _followsSong && _videoPlayer.usingVlc &&
            !Mathf.Approximately(_gameManager.SongSpeed, 1f);

        public void Update()
        {
            // Before the early returns below: whatever the player is doing, this is consumed on
            // the first frame the song runs again.
            if (_resyncWhenSongResumes && !_gameManager.Paused)
            {
                _resyncWhenSongResumes = false;
                ResyncVideoToSong();
            }

            // A stopped player (ended, or sought past its end) waits for SetTime; a started
            // video from outside the song folder just loops on its own.
            if (_heldSeekPending || !_videoPlayer.playerEnabled || (_videoStarted && !_followsSong))
                return;

            double time = _gameManager.GetVideoPlaybackTime();

            if (_parkState == ParkState.Releasing)
                ReleaseParkedVideo(time);

            if (!float.IsNaN(_reparkAtUnscaledTime) && Time.unscaledTime >= _reparkAtUnscaledTime)
            {
                _reparkAtUnscaledTime = float.NaN;
                ReparkForSpeed(time);
            }

            if (!_videoStarted && (!TryStartVideo(time) || !_followsSong))
                return;

            RevealVideoWhenPlaying(force: false);

            // Faded out before it stops: what a stopped player leaves on the texture is undefined.
            // Both checks are false for a looping video's NaN end time.
            double videoPosition = time + _videoStartTime;
            if (!_videoEnding &&
                videoPosition >= _videoEndTime - BackgroundManager.FADE_DURATION * _gameManager.SongSpeed)
            {
                _videoEnding = true;
                SetCurtain(1f);
            }

            if (videoPosition >= _videoEndTime)
            {
                _videoPlayer.Stop();
                _videoPlayer.playerEnabled = false;
            }
        }

        private bool TryStartVideo(double time)
        {
            if (time < 0.0 || !_prepared)
                return false;

            if (_followsSong && VideoTargetFor(time) < 0.0)
                return false;

            _videoStarted = true;

            // At non-100% speed, never re-seek here: a re-seek would take the rate while playing.
            // Release the video from where it is parked instead -- by OnVideoPrepared's seek, or a
            // held load-time seek (practice entry) -- so the rate is applied while paused.
            if (ParksForRate)
            {
                if (_parkState != ParkState.Releasing)
                    ArmParkRelease();
            }
            else
            {
                _parkState = ParkState.None;

                // Re-seek even when the player already sits on the target: a parked start that is
                // only played settles 20-40ms later on videos that start on a keyframe.
                if (_followsSong)
                    SeekWhilePlaying(VideoTargetFor(time));
                else
                    SetVideoPlaying(true);
            }

            _revealFrameBaseline = _videoPlayer.framesDelivered;

            // Update stops at the start for a video outside the song folder, so reveal it now.
            if (!_followsSong)
                RevealVideoWhenPlaying(force: true);

            return true;
        }

        // Some video player properties, such as the length, are only valid once it is prepared.
        private void OnVideoPrepared(YargVideoPlayer player)
        {
            // Start time is considered set if it is greater than 25 ms in either direction
            // End time is only set if it is greater than 0
            // Video will only loop if its length is less than 85% of the song's length
            const double startTimeThreshold = 0.025;
            const double endTimeThreshold = 0;
            const double dontLoopThreshold = 0.85;

            if (_followsSong && !_gameManager.Song.VideoLoop)
            {
                _videoStartTime = _gameManager.Song.VideoStartTimeSeconds;
                _videoEndTime = _gameManager.Song.VideoEndTimeSeconds;
                _videoLeadSeconds = player.pipelineLeadSeconds;

                // Clamped: a negative start time delays the start rather than naming a position
                // before the file begins.
                SeekVideo(Math.Max(VideoTargetFor(0.0), 0.0));
                if (!player.usingVlc)
                    player.playbackSpeed = _gameManager.SongSpeed;

                // Only loop the video if it's not around the same length as the song
                if (Math.Abs(_videoStartTime) < startTimeThreshold &&
                    _videoEndTime <= endTimeThreshold &&
                    player.length < _gameManager.SongLength * dontLoopThreshold)
                {
                    // SetTime, parks and resyncs only understand the first pass of a looping video:
                    // past its length, SetTime stops it. Left as-is; it is a small edge case.
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
                        _videoEndTime = Math.Min(player.length, _gameManager.SongLength + _videoStartTime);
                    }
                }
            }
            else
            {
                _videoStartTime = 0;
                _videoEndTime = double.NaN;
                player.isLooping = true;
            }

            // The player arrives here still playing, which is the only reason the seek above
            // lands (rule 1). Pause now, or it runs ahead of its mark until the start.
            SetVideoPlaying(false);
            _prepared = true;
        }

        public void SetTime(double songTime, bool waitForSeek = true)
        {
            // Videos from outside the song folder don't follow its position.
            if (!_followsSong)
                return;

            CancelPark();

            // A seek back into a video that has faded out (practice) reveals it again.
            if (_videoEnding)
            {
                _videoEnding = false;
                _videoRevealed = false;
                _revealFrameBaseline = _videoPlayer.framesDelivered;
            }

            double target = VideoTargetFor(songTime);
            if (target < 0.0)
            {
                // Before the video starts: stopped, and the start block takes over when the song
                // reaches it.
                EndHeldSeek();
                _videoPlayer.playerEnabled = true;
                _videoStarted = false;
                _videoPlayer.Stop();
                return;
            }

            if (target >= _videoPlayer.length)
            {
                EndHeldSeek();
                _videoPlayer.playerEnabled = false;
                _videoPlayer.Stop();
                return;
            }

            _videoPlayer.playerEnabled = true;

            // Set before OverridePause, whose PauseCore calls SetPaused(true): with a seek pending,
            // that leaves the player alone instead of pausing or parking it.
            _heldSeekPending = true;

            if (waitForSeek && SettingsManager.Settings.WaitForSongVideo.Value && !_holdingSongForSeek)
            {
                _holdingSongForSeek = true;
                _gameManager.OverridePause();
            }

            // Played across even if paused (rule 1); OnVideoSeeked puts it back into the song's
            // state once it lands.
            SeekWhilePlaying(target);
        }

        private void OnVideoSeeked(YargVideoPlayer player)
        {
            if (_parkState == ParkState.Seeking)
            {
                // A speed re-park releases on its own; a pause's park waits for the resume.
                bool release = _releaseParkOnLanding;
                _releaseParkOnLanding = false;
                LandPark(release);
                return;
            }

            // At non-100% speed a held seek parks rather than take the rate while playing: even
            // right after landing, the rate change jumps it 50-100ms (rule 3).
            bool parkHeldSeek = _heldSeekPending && ParksForRate;

            if (_applyRateOnSeekLanded)
            {
                _applyRateOnSeekLanded = false;
                if (!parkHeldSeek)
                    ApplyVideoRate();
            }

            if (!_heldSeekPending)
                return;

            // Before reading Paused below: releasing the hold may resume the song. Its
            // SetPaused(false) leaves the player alone while the seek is still pending.
            ReleaseSongHold();

            // Follow the song, not the video's state before the seek: a resume or pause may have
            // come while the seek was in flight.
            if (parkHeldSeek)
                LandPark(release: !_gameManager.Paused);
            else
                SetVideoPlaying(!_gameManager.Paused);

            _heldSeekPending = false;
        }

        private void ReleaseSongHold()
        {
            if (!_holdingSongForSeek)
                return;

            _holdingSongForSeek = false;
            _gameManager.OverrideResume();
        }

        // A held seek still in flight when SetTime moves outside the video: its landing no longer
        // applies, so release the song now rather than on a seekCompleted that settles nothing.
        private void EndHeldSeek()
        {
            ReleaseSongHold();
            _heldSeekPending = false;
            _applyRateOnSeekLanded = false;
        }

        // The video position to aim at for a song time. The lead is wall-clock, so it scales with
        // song speed when expressed in video time.
        private double VideoTargetFor(double songTime)
        {
            return songTime + _videoStartTime + _videoLeadSeconds * _gameManager.SongSpeed;
        }

        private bool IsWithinVideo(double target) => target >= 0.0 && target < _videoPlayer.length;

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
            SetCurtain(0f);
        }

        private void SetCurtain(float alpha)
        {
            if (_curtain != null)
                _curtain.CrossFadeAlpha(alpha, BackgroundManager.FADE_DURATION, true);
        }

        // The one place the player is played or paused. They apply asynchronously in issue order,
        // so the last one issued wins rather than the last intended.
        private void SetVideoPlaying(bool playing)
        {
            if (playing)
                _videoPlayer.Play();
            else
                _videoPlayer.Pause();
        }

        // Every seek drops to rate 1 (rule 2); ApplyVideoRate restores the song's rate when the
        // seek lands or the park is released.
        private void SeekVideo(double target)
        {
            if (_videoPlayer.usingVlc)
                _videoPlayer.playbackSpeed = 1f;

            _videoPlayer.time = target;
        }

        // Play must come first (rule 1).
        private void SeekWhilePlaying(double target)
        {
            SetVideoPlaying(true);
            SeekVideo(target);
            _applyRateOnSeekLanded = true;
        }

        private void ApplyVideoRate()
        {
            float speed = _gameManager.SongSpeed;
            if (!Mathf.Approximately(_videoPlayer.playbackSpeed, speed))
                _videoPlayer.playbackSpeed = speed;
        }

        public void ResyncWhenSongResumes()
        {
            _resyncWhenSongResumes = true;
        }

        // Called as the song resumes from a pause's rewind. A parked video is released once the song
        // reaches its frame; otherwise this falls back to Play-then-seek, which lands late by the
        // decoder's catch-up from the target's keyframe.
        private void ResyncVideoToSong()
        {
            if (!IsRealignable)
                return;

            double target = VideoTargetFor(_gameManager.GetVideoPlaybackTime());

            // Outside the video's own span, the start block and end check already own it.
            if (!IsWithinVideo(target))
                return;

            if (_parkState == ParkState.Parked &&
                Math.Abs(_videoPlayer.resumePictureTime - target) < PARK_MAX_MISMATCH_SECONDS)
            {
                ArmParkRelease();
                return;
            }

            CancelPark();
            SeekWhilePlaying(target);
        }

        // Where the park stops needn't be exact: the release reads back the picture actually
        // parked on and waits for the song to reach it.
        private void BeginPark(double target, bool releaseOnLanding)
        {
            _parkState = ParkState.Seeking;
            _releaseParkOnLanding = releaseOnLanding;
            SetVideoPlaying(true);
            SeekVideo(target);
        }

        private void LandPark(bool release)
        {
            SetVideoPlaying(false);
            _parkState = ParkState.Parked;
            if (release)
                ArmParkRelease();
        }

        private void ArmParkRelease()
        {
            // The picture on screen, which is what playback resumes from; the paused player's
            // reported time runs ahead of it.
            _parkedAt = _videoPlayer.resumePictureTime;
            _parkState = ParkState.Releasing;
        }

        private void CancelPark()
        {
            _parkState = ParkState.None;
            _reparkAtUnscaledTime = float.NaN;
            _releaseParkOnLanding = false;
        }

        private void ReleaseParkedVideo(double time)
        {
            // The paused video can still advance a frame or two after the pause is issued.
            double picture = _videoPlayer.presentedPictureTime;
            if (!double.IsNaN(picture))
                _parkedAt = picture;

            if (_gameManager.Paused || VideoTargetFor(time) < _parkedAt)
                return;

            _parkState = ParkState.None;

            // Paused, with the next picture refitting the clock: the one moment a rate change
            // costs nothing.
            ApplyVideoRate();
            SetVideoPlaying(true);
        }

        private void ReparkForSpeed(double time)
        {
            if (_gameManager.Paused || !_videoStarted || _heldSeekPending ||
                _parkState != ParkState.None || _videoPlayer.isPaused)
            {
                return;
            }

            double target = VideoTargetFor(time + REPARK_MARGIN_SECONDS * _gameManager.SongSpeed);
            if (IsWithinVideo(target))
                BeginPark(target, releaseOnLanding: true);
        }

        public void SetSpeed(float speed)
        {
            if (!_followsSong || !_videoPlayer.usingVlc)
            {
                _videoPlayer.playbackSpeed = speed;
                return;
            }

            // Never on a playing VLC video (rule 3). A paused, parked or seeking video takes the
            // new rate at its release or landing; a playing one is re-parked once the steps stop.
            if (_videoStarted && !_videoPlayer.isPaused && _parkState == ParkState.None && !_heldSeekPending)
                _reparkAtUnscaledTime = Time.unscaledTime + SPEED_STEP_SETTLE_SECONDS;
        }

        /// <param name="parkAtPlaybackTime">
        /// On pause, the video playback time the resume will rewind to, when it will rewind.
        /// </param>
        /// <remarks>
        /// A rewinding resume reaches the video twice: <see cref="ResyncWhenSongResumes"/> fires
        /// as the song starts moving again at the rewound position, and
        /// <c>SetPaused(false)</c> comes about a second later, once the song has played back past
        /// where it was paused. The second must not undo the first's release.
        /// </remarks>
        public void SetPaused(bool paused, double? parkAtPlaybackTime = null)
        {
            if (paused)
            {
                CancelPark();

                if (parkAtPlaybackTime.HasValue && _videoPlayer.usingVlc && IsRealignable)
                {
                    double target = VideoTargetFor(parkAtPlaybackTime.Value);
                    if (IsWithinVideo(target))
                    {
                        BeginPark(target, releaseOnLanding: false);
                        return;
                    }
                }
            }
            else
            {
                // The park release owns the Play: it waits for the song to reach the parked frame.
                if (_parkState == ParkState.Releasing)
                    return;

                // A held seek that landed while the song was paused (e.g. a section change from the
                // menu): release it rather than play it, so it takes the rate while still paused.
                if (_parkState == ParkState.Parked && _videoStarted && !_heldSeekPending)
                {
                    ArmParkRelease();
                    return;
                }
            }

            if (!_videoPlayer.playerEnabled || !_videoStarted || _heldSeekPending)
                return;

            SetVideoPlaying(!paused);

            // The speed changed while paused: re-align at the new rate rather than change it on the
            // playing video.
            if (!paused && _followsSong && _videoPlayer.usingVlc &&
                !Mathf.Approximately(_videoPlayer.playbackSpeed, _gameManager.SongSpeed))
            {
                _reparkAtUnscaledTime = Time.unscaledTime;
            }
        }
    }
}
