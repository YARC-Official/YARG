using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Playback;

namespace YARG.Venue.VenueCamera
{
    public class CameraManager : GameplayBehaviour
    {
        public enum CameraLocation
        {
            Stage,
            Guitar,
            Bass,
            Drums,
            Keys
        }

        [SerializeField]
        private GameObject _venue;

        private List<Camera>  _cameras;
        private Camera        _currentCamera;
        private VolumeProfile _currentProfile;
        private Volume        _volume;

        private List<PostProcessingEvent> _postProcessingEvents;
        private int _currentEventIndex;

        private float _cameraTimer;
        private int   _cameraIndex;

        public PostProcessingType CurrentEffect { get; private set; }

        protected override void OnChartLoaded(SongChart chart)
        {
            var cameras = _venue.GetComponentsInChildren<Camera>(true);
            _cameras = cameras.ToList();

            Volume volume;
            VolumeProfile profile = null;
            // Make sure the stage camera is the only one active to start
            foreach (var camera in cameras)
            {
                var vc = camera.GetComponent<VenueCamera>();
                if (vc != null && vc.CameraLocation == CameraLocation.Stage)
                {
                    camera.enabled = true;
                    _currentCamera = camera;
                    _cameraTimer = 11.0f;
                    _cameraIndex = _cameras.IndexOf(camera);
                }
                else
                {
                    camera.enabled = false;
                }
            }

            var foo = chart.VenueTrack.Stage;
            var bar = chart.VenueTrack.Performer;
            _postProcessingEvents = chart.VenueTrack.PostProcessing;

            CurrentEffect = PostProcessingType.Default;

            // 1/8th of a beat is a 32nd note
            // GameManager.BeatEventHandler.Subscribe(UpdateCameraEffect, 1f / 8f, mode: TempoMapEventMode.Quarter);
        }

        private void Update()
        {
            // Check for a change in post processing type
            if (_currentEventIndex < _postProcessingEvents.Count &&
                _postProcessingEvents[_currentEventIndex].Time <= GameManager.VisualTime)
            {
                CurrentEffect = _postProcessingEvents[_currentEventIndex].Type;
                _currentEventIndex++;
            }

            if (_cameras.Count == 1)
            {
                return;
            }

            // Update the camera timer
            _cameraTimer -= Time.deltaTime;
            if (_cameraTimer <= 0f)
            {
                // _currentProfile = _currentCamera.GetComponent<VenueCamera>().GetProfile();
                _currentCamera.enabled = false;
                _cameraTimer = 11.0f;
                _cameraIndex++;
                if (_cameraIndex >= _cameras.Count)
                {
                    _cameraIndex = 0;
                }
                _currentCamera = _cameras[_cameraIndex];
                _currentCamera.enabled = true;
                // _currentCamera.GetComponent<VenueCamera>().SetCameraPostProcessing(CurrentEffect);
            }
        }

        // protected override void GameplayDestroy()
        // {
        //     // GameManager.BeatEventHandler.Unsubscribe(UpdateCameraEffect);
        // }
        //
        // private void UpdateCameraEffect()
        // {
        //     throw new System.NotImplementedException();
        // }
    }
}