using UnityEngine;

public class ParticleGroup : MonoBehaviour {
	public bool keepAlpha = true;

	[SerializeField]
	private ParticleSystem[] colorParticles;
	[SerializeField]
	private Light[] colorLights;

	private ParticleSystem[] particles;
	private FadeLight[] fadeLights;

	private void Start() {
		particles = GetComponentsInChildren<ParticleSystem>();
		fadeLights = GetComponentsInChildren<FadeLight>();
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

		// Set colors of lights
		foreach (var light in colorLights) {
			light.color = color;
		}
	}

	public void Play() {
		foreach (var particle in particles) {
			particle.Play();
		}

		foreach (var fadeLight in fadeLights) {
			fadeLight.Play();
		}
	}
}