using UnityEngine;

public class Colorizer : MonoBehaviour {
	public Color color = Color.white;
	public bool keepAlpha = true;

	[SerializeField]
	private ParticleSystem[] particles;
	[SerializeField]
	private Light[] lights;

	private void Start() {
		// Set colors of particles
		foreach (var ps in particles) {
			var m = ps.main;

			var c = color;
			if (keepAlpha) {
				c.a = m.startColor.color.a;
			}

			m.startColor = c;
		}

		// Set colors of lights
		foreach (var light in lights) {
			light.color = color;
		}
	}
}