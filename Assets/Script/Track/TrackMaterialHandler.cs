using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.PlayMode {
	public class TrackMaterialHandler : MonoBehaviour {
		// MOST OF THIS CLASS IS TEMPORARY UNTIL THE TRACK TEXTURE SETTINGS ARE IN

		private static readonly int ScrollProperty = Shader.PropertyToID("_Scroll");
		private static readonly int StarpowerStateProperty = Shader.PropertyToID("_Starpower_State");
		private static readonly int WavinessProperty = Shader.PropertyToID("_Waviness");

		private static readonly int Layer1ColorProperty = Shader.PropertyToID("_Layer_1_Color");
		private static readonly int Layer2ColorProperty = Shader.PropertyToID("_Layer_2_Color");
		private static readonly int Layer3ColorProperty = Shader.PropertyToID("_Layer_3_Color");
		private static readonly int Layer4ColorProperty = Shader.PropertyToID("_Layer_4_Color");

		private static readonly int SoloStateProperty = Shader.PropertyToID("_Solo_State");

		public struct Preset {
			public struct Layer {
				public Color Color;
			}

			public Layer Layer1;
			public Layer Layer2;
			public Layer Layer3;
			public Layer Layer4;
		}

		private static Preset _normalPreset;
		private static Preset _groovePreset;

		private float _grooveState;
		public float GrooveState {
			get => _grooveState;
			set {
				_grooveState = value;

				_material.SetColor(Layer1ColorProperty, Color.Lerp(_normalPreset.Layer1.Color, _groovePreset.Layer1.Color, value));
				_material.SetColor(Layer2ColorProperty, Color.Lerp(_normalPreset.Layer2.Color, _groovePreset.Layer2.Color, value));
				_material.SetColor(Layer3ColorProperty, Color.Lerp(_normalPreset.Layer3.Color, _groovePreset.Layer3.Color, value));
				_material.SetColor(Layer4ColorProperty, Color.Lerp(_normalPreset.Layer4.Color, _groovePreset.Layer4.Color, value));

				_material.SetFloat(WavinessProperty, value);
			}
		}

		private float _soloState;
		public float SoloState {
			get => _soloState;
			set {
				_soloState = value;

				foreach (var material in _trimMaterials) {
					material.SetFloat(SoloStateProperty, value);
				}
				_material.SetFloat(SoloStateProperty, value);
			}
		}

		public float StarpowerState {
			get => _material.GetFloat(StarpowerStateProperty);
			set => _material.SetFloat(StarpowerStateProperty, value);
		}

		[SerializeField]
		private MeshRenderer _trackMesh;
		[SerializeField]
		private MeshRenderer[] _trackTrims;

		private Material _material;
		private readonly List<Material> _trimMaterials = new();

		private void Awake() {
			// Get materials
			_material = _trackMesh.material;
			foreach (var trim in _trackTrims) {
				_trimMaterials.Add(trim.material);
			}

			_normalPreset = new() {
				Layer1 = new() {
					Color = FromHex("0F0F0F", 1f)
				},
				Layer2 = new() {
					Color = FromHex("4B4B4B", 0.15f)
				},
				Layer3 = new() {
					Color = FromHex("FFFFFF", 0f)
				},
				Layer4 = new() {
					Color = FromHex("575757", 1f)
				}
			};

			_groovePreset = new() {
				Layer1 = new() {
					Color = FromHex("000933", 1f)
				},
				Layer2 = new() {
					Color = FromHex("23349C", 0.15f)
				},
				Layer3 = new() {
					Color = FromHex("FFFFFF", 0f)
				},
				Layer4 = new() {
					Color = FromHex("2C499E", 1f)
				}
			};
		}

		private static Color FromHex(string hex, float alpha) {
			if (ColorUtility.TryParseHtmlString("#" + hex, out var color)) {
				color.a = alpha;
				return color;
			}

			throw new InvalidOperationException();
		}

		public void ScrollTrack(float scrollSpeed) {
			var oldOffset = _material.GetFloat(ScrollProperty);
			float movement = Time.deltaTime * scrollSpeed / 4f;
			_material.SetFloat(ScrollProperty, oldOffset + movement);
		}
	}
}
