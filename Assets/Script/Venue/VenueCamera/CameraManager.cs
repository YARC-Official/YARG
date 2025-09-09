using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Gameplay;
using YARG.Helpers.Extensions;
using Random = UnityEngine.Random;

namespace YARG.Venue.VenueCamera
{
    public partial class CameraManager : GameplayBehaviour
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
            Behind,
            Random,
            GuitarVocals,
            BassVocals,
            Crowd,
            VocalsBehind,
            BassGuitarBehind,
            BassVocalsBehind,
            GuitarVocalsBehind,
            KeysVocalsBehind,
            BassKeysBehind,
            GuitarKeysBehind,
            KeysBehind,
            DrumsBehind,
            BassBehind,
            GuitarBehind,
        }

        public enum CameraDistance
        {
            Near,
            Far
        }

        public enum CameraOrientation
        {
            Front,
            Behind
        }

        private readonly HashSet<CameraLocation> _validLocations = new();

        private readonly Dictionary<CameraCutEvent.CameraCutSubject, CameraLocation> _cameraLocationLookup = new()
        {
            { CameraCutEvent.CameraCutSubject.Stage, CameraLocation.Stage },
            { CameraCutEvent.CameraCutSubject.Guitar, CameraLocation.Guitar },
            { CameraCutEvent.CameraCutSubject.GuitarCloseup, CameraLocation.GuitarCloseup },
            { CameraCutEvent.CameraCutSubject.Bass, CameraLocation.Bass },
            { CameraCutEvent.CameraCutSubject.BassCloseup, CameraLocation.BassCloseup },
            { CameraCutEvent.CameraCutSubject.Drums, CameraLocation.Drums },
            { CameraCutEvent.CameraCutSubject.DrumsKick, CameraLocation.DrumsKick },
            { CameraCutEvent.CameraCutSubject.Keys, CameraLocation.Keys },
            { CameraCutEvent.CameraCutSubject.Vocals, CameraLocation.Vocals },
            { CameraCutEvent.CameraCutSubject.AllBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.BehindNoDrum, CameraLocation.Behind },
            // For testing
            { CameraCutEvent.CameraCutSubject.BassBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.GuitarBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.DrumsBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.KeysBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.VocalsBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.BassGuitarBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.BassVocalsBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.GuitarVocalsBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.KeysVocalsBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.BassKeysBehind, CameraLocation.Behind },
            { CameraCutEvent.CameraCutSubject.GuitarKeysBehind, CameraLocation.Behind },

            { CameraCutEvent.CameraCutSubject.AllFar, CameraLocation.Stage },
            { CameraCutEvent.CameraCutSubject.AllNear, CameraLocation.Stage },
            // Back to your regularly scheduled list already in progress
            { CameraCutEvent.CameraCutSubject.Random, CameraLocation.Random }
        };

        [SerializeField]
        private GameObject _venue;

        private List<Camera>  _cameras;
        private Camera        _currentCamera;

        private List<CameraCutEvent> _cameraCuts;
        private int                  _currentCutIndex;

        private readonly Dictionary<CameraLocation, Camera>                        _cameraLocations    = new();
        private          Dictionary<CameraCutEvent.CameraCutSubject, List<Camera>> _subjectToCameraMap = new();

        private List<Camera> _nearCameras = new();
        private List<Camera> _farCameras = new();
        private List<Camera> _frontCameras = new();
        private List<Camera> _behindCameras = new();

        private float _cameraTimer;
        private int   _cameraIndex;
        private bool  _volumeSet;

        protected override void OnChartLoaded(SongChart chart)
        {
            var cameras = _venue.GetComponentsInChildren<Camera>(true);
            _cameras = cameras.ToList();

            // Make sure the stage camera is the only one active to start..and put them in a dictionary
            bool foundStage = false;
            foreach (var camera in cameras)
            {
                var vc = camera.GetComponent<VenueCamera>();

                camera.enabled = false;

                if (vc == null)
                {
                    continue;
                }

                if (vc.CameraDistance == CameraDistance.Near)
                {
                    _nearCameras.Add(camera);
                }
                else
                {
                    _farCameras.Add(camera);
                }

                if (vc.CameraOrientation == CameraOrientation.Front)
                {
                    _frontCameras.Add(camera);
                }
                else
                {
                    _behindCameras.Add(camera);
                }

                foreach (var subject in vc.CameraCutSubjects)
                {
                    // Check that the list for this subject has been initialized
                    if (!_subjectToCameraMap.ContainsKey(subject))
                    {
                        _subjectToCameraMap.Add(subject, new List<Camera> {camera} );
                    }

                    _subjectToCameraMap[subject].Add(camera);
                }

                if (vc.CameraLocation == CameraLocation.Stage && !foundStage)
                {
                    camera.enabled = true;
                    _currentCamera = camera;
                    _cameraTimer = GetRandomCameraTimer();
                    _cameraIndex = _cameras.IndexOf(camera);
                    foundStage = true;
                }

                _cameraLocations[vc.CameraLocation] = camera;
                _validLocations.Add(vc.CameraLocation);
            }

            _postProcessingEvents = chart.VenueTrack.PostProcessing;
            _cameraCuts = chart.VenueTrack.CameraCuts;

            _volumeSet = _profile != null;

            // Make up a PostProcessingEvent of type default to start us off
            var firstEffect = new PostProcessingEvent(PostProcessingType.Default, -2f, 0);
            CurrentEffect = firstEffect;

            InitializePostProcessing();
        }

        private void Update()
        {
            UpdatePostProcessing();

            if (_cameras.Count == 1)
            {
                return;
            }

            // Check for cut events
            if (_currentCutIndex < _cameraCuts.Count && _cameraCuts[_currentCutIndex].Time <= GameManager.VisualTime)
            {
                SwitchCamera(MapSubjectToValidCamera(_cameraCuts[_currentCutIndex]));
                _currentCutIndex++;
            }

            // Update the camera timer
            _cameraTimer -= Time.deltaTime;
            if (_cameraTimer <= 0f)
            {
                YargLogger.LogDebug("Changing camera due to timer expiry");
                SwitchCamera(GetRandomCamera(), true);
            }
        }

        private void SwitchCamera(Camera newCamera, bool random = false)
        {
            _currentCamera.enabled = false;

            if (random)
            {
                _cameraTimer = GetRandomCameraTimer();
                _currentCamera = GetRandomCamera();
                _cameraIndex = _cameras.IndexOf(_currentCamera);
                _currentCamera.enabled = true;
            }
            else
            {
                _currentCamera = newCamera;
                _currentCamera.enabled = true;
                _cameraIndex = _cameras.IndexOf(newCamera);
                _cameraTimer = _cameraTimer = Mathf.Max(11f, (float) _cameraCuts[_currentCutIndex].TimeLength);
            }
        }

        private float GetRandomCameraTimer()
        {
            return Random.Range(3f, 8f);
        }

        private Camera GetRandomCamera()
        {
            var index = Random.Range(0, _cameras.Count - 1);
            return _cameras[index];
        }

        private Camera MapSubjectToValidCamera(CameraCutEvent cut)
        {
            var subject = cut.Subject;
            var hasConstraint = cut.Constraint != CameraCutEvent.CameraCutConstraint.None;

            if (cut.RandomChoices.Count > 0)
            {
                var choices = string.Join(", ", cut.RandomChoices);
                YargLogger.LogDebug($"Camera cut has options: {choices}");
            }
            else
            {
                YargLogger.LogDebug($"New camera subject: {cut.Subject}");
            }

            if (subject == CameraCutEvent.CameraCutSubject.Random)
            {
                // Choose from the cut event's options list, assuming it exists (otherwise completely random)
                if (cut.RandomChoices.Count > 0)
                {
                    // Clone the list so we don't obliterate the original
                    var choices = cut.RandomChoices.ToList();
                    // Remove choices that aren't in _subjectToCameraMap
                    for (var i = choices.Count - 1; i >= 0; i--)
                    {
                        if (!_subjectToCameraMap.ContainsKey(choices[i]))
                        {
                            choices.RemoveAt(i);
                        }
                    }

                    List<Camera> cameras;

                    if (choices.Count > 0)
                    {
                        var selected = choices.Pick();
                        cameras = _subjectToCameraMap.GetValueOrDefault(selected, _cameras);
                    }
                    else
                    {
                        cameras = _cameras;
                    }

                    var filteredCameras = FilterCamerasByConstraint(cut, cameras);

                    if (filteredCameras.Count > 0)
                    {
                        return filteredCameras.Pick();
                    }

                    return cameras.Pick();
                }

                // If there are no choices and no constraints, just pick a random camera
                if (!hasConstraint)
                {
                    return _cameraLocations[PickOne(_validLocations.ToArray())];
                }

                // Try to obey the constraints
                var validCams = FilterCamerasByConstraint(cut, _cameras);
                if (validCams.Count > 0)
                {
                    return validCams.Pick();
                }

                // No luck, just pick anything
                return _cameraLocations[PickOne(_validLocations.ToArray())];
            }

            // The map doesn't have a key for this, so just pick a random camera
            if (!_subjectToCameraMap.TryGetValue(subject, out var locations))
            {
                // It's possible we don't have the subject to camera map but we do have subject to location
                // map, so we can use that to pick a camera

                // TODO: Fix this so it doesn't break when subject is "random"
                if (_cameraLocationLookup.TryGetValue(subject, out var location))
                {
                    var camera = _cameraLocations.GetValueOrDefault(location, _cameras.Pick());
                    return camera;
                }

                if (hasConstraint)
                {
                    var validCams = GetCamerasForConstraint(cut.Constraint);

                    if (validCams.Count > 0)
                    {
                        YargLogger.LogDebug($"Filtering (random) cameras by constraint {cut.Constraint}");
                        return validCams[Random.Range(0, validCams.Count - 1)];
                    }

                    // fall through since the venue doesn't have near/far/front/behind set up
                    YargLogger.LogDebug("No cameras found for constraint");
                }
                return _cameraLocations[PickOne(_validLocations.ToArray())];
            }

            // This is a cut to a single camera with a subject we know
            var filteredLocations = FilterCamerasByConstraint(cut, locations);

            if (filteredLocations.Count == 0)
            {
                // No other choice but to pick a random camera
                return _cameraLocations[PickOne(_validLocations.ToArray())];
            }

            if (filteredLocations.Count > 1)
            {
                return filteredLocations[Random.Range(0, filteredLocations.Count - 1)];
            }

            return filteredLocations[0];
        }

        private List<Camera> FilterCamerasByConstraint(CameraCutEvent cut, List<Camera> cameras)
        {
            var hasConstraint = cut.Constraint != CameraCutEvent.CameraCutConstraint.None;
            if (!hasConstraint)
            {
                return cameras;
            }

            YargLogger.LogDebug($"Filtering cameras by constraint {cut.Constraint}");

            var constraint = (int) cut.Constraint;

            var validCams = new List<Camera>(cameras);

            foreach (var value in Enum.GetValues(typeof(CameraCutEvent.CameraCutConstraint)))
            {
                var flag = (int) value;
                if ((constraint & flag) == flag)
                {
                    var filteredCams = GetCamerasForConstraint((CameraCutEvent.CameraCutConstraint) value);
                    // Now remove anything from validCams that isn't in filteredCams
                    for (var i = 0; i < validCams.Count; i++)
                    {
                        if (!filteredCams.Contains(validCams[i]))
                        {
                            validCams.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            return validCams;
        }

        private List<Camera> GetCamerasForConstraint(CameraCutEvent.CameraCutConstraint constraint)
        {
            var validCams = constraint switch
            {
                CameraCutEvent.CameraCutConstraint.OnlyClose => _nearCameras,
                CameraCutEvent.CameraCutConstraint.OnlyFar   => _farCameras,
                CameraCutEvent.CameraCutConstraint.NoBehind  => _frontCameras,
                // TODO: This is not correct, this should be all cameras that aren't closeup cams (like guitar head or whatever)
                CameraCutEvent.CameraCutConstraint.NoClose => _farCameras,
                _                                          => _cameras
            };

            return validCams;
        }

        protected override void GameplayDestroy()
        {
            // Enable the camera in case it happens to be disabled
            _currentCamera.enabled = true;
            base.GameplayDestroy();
        }

        private static CameraLocation PickOne(CameraLocation[] locations)
        {
            return locations[Random.Range(0, locations.Length - 1)];
        }
    }
}