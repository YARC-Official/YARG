using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core;
using YARG.Core.Game;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class KeysArray : MonoBehaviour
    {
        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private float _whiteKeyOffset;
        [SerializeField]
        private float _blackKeyOffset;

        public float KeySpacing => _trackWidth / ProKeysPlayer.WHITE_KEY_VISIBLE_COUNT;

        private readonly List<KeysObjectContainer> _keys = new();

        public void Initialize(ThemePreset themePreset, ColorProfile.ProKeysColors colors)
        {
            var whiteKeyPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, GameMode.ProKeys,
                ThemeManager.WHITE_KEY_PREFAB_NAME);
            var blackKeyPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, GameMode.ProKeys,
                ThemeManager.BLACK_KEY_PREFAB_NAME);

            // Pro-keys always starts at C

            _keys.Clear();
            int whitePositionIndex = 0;
            int blackPositionIndex = 0;

            for (int i = 0; i < ProKeysPlayer.TOTAL_KEY_COUNT; i++)
            {
                // The index within the octave (0-11)
                int noteIndex = i % 12;

                if (PianoHelper.IsBlackKey(noteIndex))
                {
                    // Black keys

                    var fret = Instantiate(blackKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        blackPositionIndex * KeySpacing + _blackKeyOffset, 0f, 0f);

                    // This is terrible lol
                    var keyColor = i switch
                    {
                        1 or 3         => colors.GetOverlayColor(0).ToUnityColor(),
                        6 or 8 or 10   => colors.GetOverlayColor(1).ToUnityColor(),
                        13 or 15       => colors.GetOverlayColor(2).ToUnityColor(),
                        18 or 20 or 22 => colors.GetOverlayColor(3).ToUnityColor(),
                        _              => Color.white
                    };

                    var material = fret.GetComponentInChildren<MeshRenderer>().material;

                    var keyword = new LocalKeyword(material.shader, "_ISPRESSED");

                    var container = new KeysObjectContainer
                    {
                        Transform = fret.transform,
                        ModelParent = fret.transform.Find("Model Parent"),
                        Material = material,
                        PressedKeyword = keyword,
                        FretComponent = fret.GetComponent<Fret>(),
                    };

                    keyColor.r *= 0.5f;
                    keyColor.g *= 0.5f;
                    keyColor.b *= 0.5f;

                    container.Material.color = keyColor;

                    _keys.Add(container);

                    blackPositionIndex++;
                    if (PianoHelper.IsGapOnNextBlackKey(noteIndex))
                    {
                        blackPositionIndex++;
                    }
                }
                else
                {
                    // White keys

                    var fret = Instantiate(whiteKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        whitePositionIndex * KeySpacing + _whiteKeyOffset, 0f, 0f);

                    var material = fret.GetComponentInChildren<MeshRenderer>().material;

                    var keyword = new LocalKeyword(material.shader, "_ISPRESSED");

                    var container = new KeysObjectContainer
                    {
                        Transform = fret.transform,
                        ModelParent = fret.transform.Find("Model Parent"),
                        Material = material,
                        PressedKeyword = keyword,
                        FretComponent = fret.GetComponent<Fret>(),
                    };

                    _keys.Add(container);

                    whitePositionIndex++;
                }
            }
        }

        public float GetKeyX(int index)
        {
            return _keys[index].Transform.localPosition.x;
        }

        public void SetPressed(int index, bool pressed)
        {
            var key = _keys[index];

            var rotation = Vector3.zero;
            if (pressed)
            {
                rotation = Vector3.zero.WithX(-15);
            }

            // I tried to make an animation in Unity but it wouldnt even let me add
            // a property, so I gave up and use tweening instead.
            key.ModelParent.DOLocalRotate(rotation, 0.025f);

            // Only do white key light ups for now
            if (PianoHelper.IsWhiteKey(index % 12))
            {
                key.Material.SetKeyword(key.PressedKeyword, pressed);
            }
        }

        private struct KeysObjectContainer
        {
            public Transform Transform;
            public Transform ModelParent;

            public Material Material;

            public LocalKeyword PressedKeyword;

            public Fret FretComponent;
        }
    }
}