using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay;

namespace YARG.Venue.VenueCamera
{
    /// <summary>
    /// Data storage for venue camera
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VenueCamera : GameplayBehaviour
    {
        [SerializeField]
        private CameraManager _cameraManager;
        [SerializeField]
        public CameraManager.CameraLocation CameraLocation;

        [Space]
        [SerializeField]
        [Header("Camera Cut Subjects For This Camera")]
        public List<CameraCutEvent.CameraCutSubject> CameraCutSubjects;
    }
}