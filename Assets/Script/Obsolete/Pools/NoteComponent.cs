using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Pools
{
    public class NoteComponent : Poolable
    {
        public enum ModelType
        {
            NOTE,
            HOPO,
            TAP,
            FULL,
            FULL_HOPO
        }

        private enum State
        {
            WAITING,
            HITTING,
            MISSED
        }

        [SerializeField]
        private MeshRenderer[] meshRenderers;

        [SerializeField]
        private int[] meshRendererMiddleIndices;

        [SerializeField]
        private float[] noteGroupsEmission;

        [SerializeField]
        private bool[] boostColor;

        [Space]
        [SerializeField]
        private GameObject noteGroup;

        [SerializeField]
        private GameObject hopoGroup;

        [SerializeField]
        private GameObject tapGroup;

        [SerializeField]
        private GameObject fullGroup;

        [SerializeField]
        private GameObject fullHopoGroup;

        [Space]
        [SerializeField]
        private TextMeshPro fretNumber;

        [SerializeField]
        private LineRenderer lineRenderer;

        [SerializeField]
        private LineRenderer fullLineRenderer;

        private NoteInfo _noteInfo;

        private float _brutalVanishDistance;

        /// <summary>
        /// Ranges between -1 and 1. Notes will disappear when they reach this percentage down the track.
        /// </summary>
        /// <remarks>
        /// A value of 0 is the strikeline, while a value of 1 is the top of the track.<br/>
        /// Larger numbers will cause the notes to disappear sooner.<br/>
        /// Notes will not disappear at all if this number is below 0.
        /// </remarks>
        private float BrutalVanishDistance
        {
            get => _brutalVanishDistance;
            set { _brutalVanishDistance = System.Math.Clamp(value, -1, 1); }
        }

        private bool BrutalIsNoteVanished
        {
            get => PercentDistanceFromStrikeline <= BrutalVanishDistance && state == State.WAITING;
        }

        private float PercentDistanceFromStrikeline
        {
            get
            {
                const float TRACK_START = 3.00f;
                const float TRACK_END = -1.76f;
                const float range = TRACK_START - TRACK_END;

                var result = (transform.position.z - TRACK_END) / range;
                return result;
            }
        }

        private Color _colorCacheNotes = Color.white;

        private Color ColorCacheNotes
        {
            get
            {
                if (isActivatorNote)
                {
                    return Color.magenta;
                }

                if (_isStarpower)
                {
                    return Color.white;
                }

                return _colorCacheNotes;
            }
            set => _colorCacheNotes = value;
        }

        private Color _colorCacheSustains = Color.white;

        private Color ColorCacheSustains
        {
            get
            {
                if (BrutalIsNoteVanished)
                {
                    return Color.clear;
                }

                if (_isStarpower)
                {
                    return Color.white;
                }

                return _colorCacheSustains;
            }
            set => _colorCacheSustains = value;
        }

        private bool _useFullLineRenderer;
        private LineRenderer CurrentLineRenderer => _useFullLineRenderer ? fullLineRenderer : lineRenderer;

        private float lengthCache = 0f;

        private State state = State.WAITING;

        private bool _isStarpower;
        private bool isActivatorNote;

        private float _secondaryAmplitudeTime;
        private float _tertiaryAmplitudeTime;

        private void OnEnable()
        {
            if (pool != null)
            {
                pool.player.track.StarpowerMissEvent += OnStarpowerMissed;
            }

            foreach (MeshRenderer r in meshRenderers)
            {
                r.enabled = true;
            }

            lineRenderer.enabled = false;
            fullLineRenderer.enabled = false;

            // Set it to Time.time to sync with other notes
            _secondaryAmplitudeTime = Time.time;
            _tertiaryAmplitudeTime = Time.time;
        }

        private void OnDisable()
        {
            if (pool != null)
            {
                pool.player.track.StarpowerMissEvent -= OnStarpowerMissed;
            }
        }

        public void SetInfo(NoteInfo info, Color notes, Color sustains, float length, ModelType type,
            bool isStarpowerNote, bool isDrumActivator = false)
        {
            static void SetModelActive(GameObject obj, ModelType inType, ModelType needType)
            {
                if (obj != null)
                {
                    obj.SetActive(inType == needType);
                }
            }

            _useFullLineRenderer = type == ModelType.FULL || type == ModelType.FULL_HOPO;

            // Show/hide models
            SetModelActive(noteGroup, type, ModelType.NOTE);
            SetModelActive(hopoGroup, type, ModelType.HOPO);
            SetModelActive(tapGroup, type, ModelType.TAP);
            SetModelActive(fullGroup, type, ModelType.FULL);
            SetModelActive(fullHopoGroup, type, ModelType.FULL_HOPO);

            SetLength(length);

            state = State.WAITING;
            ColorCacheNotes = notes;
            ColorCacheSustains = sustains;

            _noteInfo = info;
            _isStarpower = isStarpowerNote;
            isActivatorNote = isDrumActivator;

            UpdateColor();
            UpdateRandomness();
            ResetLineAmplitude();
        }

        public void SetFretNumber(string str)
        {
            fretNumber.gameObject.SetActive(true);
            fretNumber.text = str;
        }

        private void UpdateColor()
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                int index = meshRendererMiddleIndices[i];

                if (boostColor[i])
                {
                    meshRenderers[i].materials[index].color = ColorCacheNotes + new Color(3, 3, 3, 0);
                }
                else
                {
                    meshRenderers[i].materials[index].color = ColorCacheNotes;
                }

                meshRenderers[i].materials[index].SetColor("_EmissionColor", ColorCacheNotes * noteGroupsEmission[i]);
            }

            UpdateLineColor();
        }

        private void UpdateRandomness()
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                int index = meshRendererMiddleIndices[i];
                var material = meshRenderers[i].materials[index];

                if (material.HasFloat("_RandomFloat"))
                {
                    material.SetFloat("_RandomFloat", Random.Range(-1f, 1f));
                }

                if (material.HasVector("_RandomVector"))
                {
                    material.SetVector("_RandomVector", new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
                }
            }
        }

        private void UpdateLineColor()
        {
            if (lengthCache == 0f)
            {
                return;
            }

            var mat = CurrentLineRenderer.materials[0];
            if (state == State.WAITING)
            {
                mat.color = ColorCacheSustains;
                mat.SetColor("_EmissionColor", ColorCacheSustains);
            }
            else if (state == State.HITTING)
            {
                mat.color = ColorCacheSustains;
                mat.SetColor("_EmissionColor", ColorCacheSustains * 3f);
            }
            else if (state == State.MISSED)
            {
                mat.color = new(0.9f, 0.9f, 0.9f, 0.5f);
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        private void ResetLineAmplitude()
        {
            if (_useFullLineRenderer)
            {
                return;
            }

            CurrentLineRenderer.materials[0].SetFloat("_PrimaryAmplitude", 0f);
            CurrentLineRenderer.materials[0].SetFloat("_SecondaryAmplitude", 0f);
            CurrentLineRenderer.materials[0].SetFloat("_TertiaryAmplitude", 0f);
        }

        private void SetLength(float length)
        {
            if (length <= 0.2f)
            {
                CurrentLineRenderer.enabled = false;
                lengthCache = 0f;
                return;
            }

            length *= pool.player.trackSpeed / Play.speed;
            lengthCache = length;

            CurrentLineRenderer.enabled = true;
            CurrentLineRenderer.SetPosition(0, new(0f, 0.01f, length));
            CurrentLineRenderer.SetPosition(1, Vector3.zero);
        }

        public void HitNote()
        {
            static void Hide(GameObject obj)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }

            Hide(noteGroup);
            Hide(hopoGroup);
            Hide(tapGroup);
            Hide(fullGroup);
            Hide(fullHopoGroup);

            if (fretNumber != null)
            {
                fretNumber.gameObject.SetActive(false);
            }

            state = State.HITTING;
            UpdateLineColor();
        }

        public void MissNote()
        {
            if (fretNumber != null)
            {
                fretNumber.gameObject.SetActive(false);
            }

            state = State.MISSED;
            UpdateLineColor();
            ResetLineAmplitude();
        }

        private void OnStarpowerMissed(EventInfo missedPhrase)
        {
            if (_noteInfo.time < missedPhrase.time || _noteInfo.time >= missedPhrase.EndTime)
            {
                return;
            }

            _isStarpower = false;
            UpdateColor();
        }

        private void Update()
        {
            transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * pool.player.trackSpeed);
            if (state == State.HITTING)
            {
                // Get the new line start position. Said position should be at
                // the fret board and relative to the note itelf.
                float newStart = -transform.localPosition.z - AbstractTrack.TRACK_END_OFFSET;

                // Apply to line renderer
                CurrentLineRenderer.SetPosition(1, new(0f, 0f, newStart));
            }

            // Remove if off screen
            if (transform.localPosition.z < -3f - lengthCache)
            {
                MoveToPool();
            }

            // Line hit animation
            if (state == State.HITTING && !_useFullLineRenderer)
            {
                float whammy = ((NotePool) pool).WhammyFactor * 1.5f;

                // Update the amplitude times
                _secondaryAmplitudeTime += Time.deltaTime * (4f + whammy);
                _tertiaryAmplitudeTime += Time.deltaTime * (1.7f + whammy);

                // Change line amplitude
                var lineMat = CurrentLineRenderer.materials[0];
                lineMat.SetFloat("_PrimaryAmplitude", 0.18f + whammy * 0.2f);
                lineMat.SetFloat("_SecondaryAmplitude", Mathf.Sin(_secondaryAmplitudeTime) * (whammy + 0.5f));
                lineMat.SetFloat("_TertiaryAmplitude", Mathf.Sin(_tertiaryAmplitudeTime) * (whammy * 0.1f + 0.1f));

                // Move line forward
                float forwardSub = Time.deltaTime * pool.player.trackSpeed / 2.5f * (1f + whammy * 0.1f);
                lineMat.SetFloat("_ForwardOffset", lineMat.GetFloat("_ForwardOffset") + forwardSub);
            }

            // TODO: If/when health system gets added, this should use that instead. Multiplier isn't a good way to scale difficulty here.
            if (pool.player.brutalMode)
            {
                float multiplier = pool.player.track.Multiplier;
                float maxMultiplier = pool.player.track.MaxMultiplier;
                BrutalVanishDistance = Mathf.Clamp(multiplier / maxMultiplier, 0.25f, 0.80f);
            }
            else
            {
                BrutalVanishDistance = -1.0f;
            }

            BrutalUpdateNoteVanish();
        }

        private void BrutalUpdateNoteVanish()
        {
            if (BrutalIsNoteVanished)
            {
                foreach (MeshRenderer r in meshRenderers)
                {
                    r.enabled = false;
                }

                CurrentLineRenderer.enabled = false;

                UpdateLineColor();
            }
            else
            {
                foreach (MeshRenderer r in meshRenderers)
                {
                    r.enabled = true;
                }

                if (lengthCache > 0f)
                {
                    CurrentLineRenderer.enabled = true;
                }

                UpdateLineColor();
            }
        }
    }
}