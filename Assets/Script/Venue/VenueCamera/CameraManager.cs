using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay;
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
            Random
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

        private float _cameraTimer;
        private int   _cameraIndex;
        private bool  _volumeSet;

        protected override void OnChartLoaded(SongChart chart)
        {
            var cameras = _venue.GetComponentsInChildren<Camera>(true);
            _cameras = cameras.ToList();

            // Make sure the stage camera is the only one active to start..and put them in a dictionary
            foreach (var camera in cameras)
            {
                var vc = camera.GetComponent<VenueCamera>();

                if (vc == null)
                {
                    continue;
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

                if (vc.CameraLocation == CameraLocation.Stage)
                {
                    camera.enabled = true;
                    _currentCamera = camera;
                    _cameraTimer = GetRandomCameraTimer();
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

            _volumeSet = _profile != null;

            // Make up a PostProcessingEvent of type default to start us off
            var firstEffect = new PostProcessingEvent(PostProcessingType.Default, -2f, 0);
            CurrentEffect = firstEffect;

            SetCurves();

            // 1/8th of a beat is a 32nd note
            // GameManager.BeatEventHandler.Subscribe(UpdateCameraEffect, 1f / 8f, mode: TempoMapEventMode.Quarter);
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
                // Check for more events on the same tick (there is supposed to be a priority system, but we'll choose randomly for now)
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

        private CameraLocation GetCameraLocation(CameraCutEvent cut)
        {

            // TODO: Make this check that the location is actually valid for the current venue before returning it

            // TODO: Add fallbacks to map camera cuts we don't have to something appropriate rather than giving up and
            // using a random camera without even checking that there are other reasonable options

            // TODO: Maybe see if that fallback map can be specified by the venue author (at least in part, and we
            //  just pick randomly if they didn't specify a fallback for a given cut subject that doesn't have a camera)

            if (cut.Subject != CameraCutEvent.CameraCutSubject.Random && _cameraLocationLookup.TryGetValue(cut.Subject, out var location))
            {
                return location;
            }

            if (cut.RandomChoices.Count > 0)
            {
                var choices = cut.RandomChoices.ToList();
                // Remove choices that aren't in _cameraLocationLookup
                // for (var i = choices.Count - 1; i >= 0; i--)
                // {
                //     if (!_cameraLocationLookup.ContainsKey(choices[i]))
                //     {
                //         choices.RemoveAt(i);
                //     }
                // }
                var locationIndex = Random.Range(0, choices.Count - 1);
                return _cameraLocationLookup[choices[locationIndex]];
            }

            YargLogger.LogDebug("Random camera cut has no location options");

            return PickOne(_validLocations.ToArray());
        }

        private Camera MapSubjectToValidCamera(CameraCutEvent cut)
        {
            var subject = cut.Subject;

            // TODO: Handle constraints

            // The map doesn't have a key for this, so just pick a random camera
            if (!_subjectToCameraMap.TryGetValue(subject, out var locations))
            {
                return _cameraLocations[PickOne(_validLocations.ToArray())];
            }

            if (subject == CameraCutEvent.CameraCutSubject.Random)
            {
                // Choose from the cut event's options list, assuming it exists (otherwise completely random)
                if (cut.RandomChoices.Count > 0)
                {
                    var choices = cut.RandomChoices.ToList();
                    // Remove choices that aren't in _subjectToCameraMap without using LINQ
                    for (var i = choices.Count - 1; i >= 0; i--)
                    {
                        if (!_subjectToCameraMap.ContainsKey(choices[i]))
                        {
                            choices.RemoveAt(i);
                        }
                    }

                    // Now pick from the remaining choices
                    var selected = choices[Random.Range(0, choices.Count - 1)];
                    var cameras = _subjectToCameraMap[selected];
                    return cameras[Random.Range(0, cameras.Count - 1)];
                }

                // If there are no choices, just pick a random camera
                return _cameraLocations[PickOne(_validLocations.ToArray())];
            }

            if (locations.Count > 1)
            {
                return locations[Random.Range(0, locations.Count - 1)];
            }

            return locations.First();
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