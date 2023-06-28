using System;
using TMPro;
using UnityEngine;
using YARG.UI;

namespace YARG.PlayMode
{
    public sealed class CommonTrack : MonoBehaviour
    {
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        public TrackView TrackView { get; set; }

        [field: SerializeField]
        public TrackMaterialHandler TrackMaterialHandler { get; private set; }

        [SerializeField]
        private Animation cameraAnimation;

        [Space]
        public Transform hitWindow;

        public Transform fadeBegin;
        public Transform fadeEnd;

        [Space]
        public TextMeshPro comboText;

        public MeshRenderer comboMeterRenderer;
        public MeshRenderer starpowerBarTop;

        [Space]
        public MeshRenderer comboRing;

        public MeshRenderer comboBase;
        public Material nonFCRing;
        public SpriteRenderer comboSunburst;
        public GameObject maxComboLight;
        public GameObject starpowerLight;
        public Sprite sunBurstSprite;
        public Sprite sunBurstSpriteStarpower;
        public Material baseNormal;
        public Material baseGroove;
        public Material baseSP;
        public ParticleSystem starPowerParticles;
        public ParticleSystem starPowerParticles2;
        public Light starPowerParticlesLight;
        public Light starPowerParticles2Light;
        public GameObject starPowerLightIndicators;
        public KickFlashAnimation kickFlash;

        [Space]
        public Color comboSunburstColor;

        public Color comboSunburstSPColor;

        [Space]
        // Toggle settings for performance text
        // NOTE: THIS SHOULD REALLY BE REPLACED BY A PROPER SETTINGS CLASS
        public bool hotStartNotifsEnabled;

        public bool bassGrooveNotifsEnabled;
        public bool noteStreakNotifsEnabled;
        public bool strongFinishNotifsEnabled;
        public bool overdriveReadyNotifsEnabled;
        public bool fullComboTrumpsStrongFinish;

        [Space]
        // Numeric performance text settings
        public int hotStartCutoff;

        public int strongFinishCutoff;
        public float bufferPeriod;
        public int noteStreakInterval;

        [Space]
        [SerializeField]
        private Color[] fretColors;

        [SerializeField]
        private Color[] fretInnerColors;

        [SerializeField]
        private Color[] noteColors;

        [SerializeField]
        private Color[] sustainColors;

        [Space]
        [SerializeField]
        private Color[] kickFlashColors;

        [Space]
        public int[] colorMappings;

        [SerializeField]
        private int kickFlashColorIndex;

        public Color KickFlashColor => kickFlashColors[kickFlashColorIndex];

        public Color FretColor(int i)
        {
            return fretColors[colorMappings[i]];
        }

        public Color FretInnerColor(int i)
        {
            return fretInnerColors[colorMappings[i]];
        }

        public Color NoteColor(int i)
        {
            return noteColors[colorMappings[i]];
        }

        public Color SustainColor(int i)
        {
            return sustainColors[colorMappings[i]];
        }

        //UNUSED THIS IS NOW PROCEDURAL, under TrackAnimations.cs - Mia
        public void PlayKickCameraAnimation()
        {
            StopCameraAnimation();

            cameraAnimation["CameraShakeKickDrums"].wrapMode = WrapMode.Once;
            cameraAnimation.Play("CameraShakeKickDrums");
        }

        public void StopCameraAnimation()
        {
            cameraAnimation.Stop();
            cameraAnimation.Rewind();
        }
    }
}