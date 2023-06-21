using System.Collections.Generic;
using UnityEngine;

namespace YARG.Util {
	public class ParticleGroup : MonoBehaviour {
		public bool keepAlpha = true;

		[SerializeField]
		private ParticleSystem[] colorParticles;
		[SerializeField]
		private Light[] colorLights;
		[SerializeField]
		private ParticleSystem[] emissionParticles;

		private ParticleSystem[] particles;
		private FadeLight[] fadeLights;
		private Light[] normalLights;

		private void Awake() {
			particles = GetComponentsInChildren<ParticleSystem>();

			List<FadeLight> fadeLightsList = new();
			List<Light> normalLightsList = new();
			foreach (var light in GetComponentsInChildren<Light>()) {
				var fadeLight = light.GetComponent<FadeLight>();
				if (fadeLight != null) {
					fadeLightsList.Add(fadeLight);
				} else {
					normalLightsList.Add(light);
					light.enabled = false;
				}
			}

			fadeLights = fadeLightsList.ToArray();
			normalLights = normalLightsList.ToArray();
		}

		public void Colorize(Color color) {
			// Set colors of particles
			foreach (var ps in colorParticles) {
				var m = ps.main;

				var c = color;
				if (keepAlpha) {
					c.a = m.startColor.color.a;
				}

				m.startColor = c;
			}

			foreach (var ps in emissionParticles) {
				var material = ps.GetComponent<ParticleSystemRenderer>().material;
				material.color = color;
				material.SetColor("_EmissionColor", color * 35f);
			}

			// Set colors of lights
			foreach (var light in colorLights) {
				light.color = color;
			}
		}

		public void SetStartSpeed(float speed) {
			foreach (var particle in particles) {
				var main = particle.main;
				main.startSpeed = speed;
			}
		}

		public void Play(float speed = 4.0f) {
			foreach (var particle in particles) {
				if (particle.main.loop && particle.isEmitting) {
					continue;
				}

				particle.Play();
			}

			foreach (var fadeLight in fadeLights) {
				fadeLight.Play();
			}

			foreach (var light in normalLights) {
				light.enabled = true;
			}
		}

		public void Stop() {
			foreach (var particle in particles) {
				if (particle.main.loop && !particle.isEmitting) {
					continue;
				}

				particle.Stop();
			}

			foreach (var light in normalLights) {
				light.enabled = false;
			}
		}
	}
}