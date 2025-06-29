using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay;
using YARG.Playback;
using Random = UnityEngine.Random;

namespace YARG.Venue.VenueCamera
{
    public class CameraManager : GameplayBehaviour
    {
        public enum CameraLocation
        {
            Stage,
            Guitar,
            GuitarCloseup,
            Bass,
            BassCloseup,
            Drums,
            DrumsKick,
            Keys,
            Vocals,
            Random
        }

        private readonly HashSet<CameraLocation> _validLocations = new();

        private readonly Dictionary<CameraCutEvent.CameraCutSubject, CameraLocation> _cameraLocationLookup = new()
        {
            {
                CameraCutEvent.CameraCutSubject.Stage, CameraLocation.Stage
            },
            {
                CameraCutEvent.CameraCutSubject.Guitar, CameraLocation.Guitar
            },
            {
                CameraCutEvent.CameraCutSubject.GuitarCloseup, CameraLocation.GuitarCloseup
            },
            {
                CameraCutEvent.CameraCutSubject.Bass, CameraLocation.Bass
            },
            {
                CameraCutEvent.CameraCutSubject.BassCloseup, CameraLocation.BassCloseup
            },
            {
                CameraCutEvent.CameraCutSubject.Drums, CameraLocation.Drums
            },
            {
                CameraCutEvent.CameraCutSubject.DrumsKick, CameraLocation.DrumsKick
            },
            {
                CameraCutEvent.CameraCutSubject.Keys, CameraLocation.Keys
            },
            {
                CameraCutEvent.CameraCutSubject.Vocals, CameraLocation.Vocals
            },
            {
                CameraCutEvent.CameraCutSubject.Random, CameraLocation.Random
            }
        };

        [SerializeField]
        private GameObject _venue;

        private List<Camera>  _cameras;
        private Camera        _currentCamera;
        private VolumeProfile _currentProfile;
        private Volume        _volume;

        private List<PostProcessingEvent> _postProcessingEvents;
        private int                       _currentEventIndex;

        private List<CameraCutEvent> _cameraCuts;
        private int                  _currentCutIndex;

        private readonly Dictionary<CameraLocation, Camera> _cameraLocations = new();

        private float _cameraTimer;
        private int   _cameraIndex;

        public PostProcessingType CurrentEffect { get; private set; }

        protected override void OnChartLoaded(SongChart chart)
        {
            var cameras = _venue.GetComponentsInChildren<Camera>(true);
            _cameras = cameras.ToList();

            Volume volume;
            VolumeProfile profile = null;
            // Make sure the stage camera is the only one active to start..and put them in a dictionary
            foreach (var camera in cameras)
            {
                var vc = camera.GetComponent<VenueCamera>();

                if (vc == null)
                {
                    continue;
                }

                if (vc.CameraLocation == CameraLocation.Stage)
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

                _cameraLocations[vc.CameraLocation] = camera;
                _validLocations.Add(vc.CameraLocation);
            }

            var foo = chart.VenueTrack.Stage;
            var bar = chart.VenueTrack.Performer;
            _postProcessingEvents = chart.VenueTrack.PostProcessing;
            _cameraCuts = chart.VenueTrack.CameraCuts;

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

            // Check for cut events
            if (_currentCutIndex < _cameraCuts.Count && _cameraCuts[_currentCutIndex].Time <= GameManager.VisualTime)
            {
                _currentCamera.enabled = false;
                // _currentCamera = _cameraLocations[_cameraLocationLookup[_cameraCuts[_currentCutIndex].Subject]];
                _currentCamera = _cameraLocations[GetCameraLocation(_cameraCuts[_currentCutIndex])];
                _cameraIndex = _cameras.IndexOf(_currentCamera);
                // _cameraTimer = 11.0f;
                _cameraTimer = Mathf.Max(11f, (float) _cameraCuts[_currentCutIndex].TimeLength);
                _currentCamera.enabled = true;
                _currentCutIndex++;
            }

            // Update the camera timer
            _cameraTimer -= Time.deltaTime;
            if (_cameraTimer <= 0f)
            {
                YargLogger.LogDebug("Changing camera due to timer expiry");
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

        private CameraLocation GetCameraLocation(CameraCutEvent cut)
        {
            if (cut.Subject != CameraCutEvent.CameraCutSubject.Random)
            {
                return _cameraLocationLookup[cut.Subject];
            }

            if (cut.RandomChoices.Count > 0)
            {
                var locationIndex = Random.Range(0, cut.RandomChoices.Count - 1);
                return _cameraLocationLookup[cut.RandomChoices[locationIndex]];
            }

            YargLogger.LogDebug("Random camera cut has no location options");

            return PickOne(_validLocations.ToArray());
        }

        private static CameraLocation PickOne(CameraLocation[] locations)
        {
            return locations[Random.Range(0, locations.Length - 1)];
        }

        private static CameraLocation PickOne(CameraLocation a, CameraLocation b)
        {
            return Random.Range(0, 1) == 0 ? a : b;
        }

        private static CameraLocation PickOne(CameraLocation a, CameraLocation b, CameraLocation c)
        {
            return Random.Range(0, 2) switch {
                0 => a,
                1 => b,
                _ => c
            };
        }

        private static CameraLocation PickOne(CameraLocation a, CameraLocation b, CameraLocation c, CameraLocation d)
        {
            return Random.Range(0, 3) switch
            {
                0 => a,
                1 => b,
                2 => c,
                _ => d
            };
        }

        private static CameraLocation PickOne(CameraLocation a, CameraLocation b, CameraLocation c, CameraLocation d,
            CameraLocation e)
        {
            return Random.Range(0, 4) switch
            {
                0 => a,
                1 => b,
                2 => c,
                3 => d,
                _ => e
            };
        }
    }
}