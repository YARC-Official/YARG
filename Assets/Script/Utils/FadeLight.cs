using UnityEngine;

[RequireComponent(typeof(Light))]
public class FadeLight : MonoBehaviour {
	[SerializeField]
	private float fadeOutRate = 2f;

	private new Light light;

	private void Awake() {
		light = GetComponent<Light>();
	}

	private void Update() {
		light.intensity -= Time.deltaTime * fadeOutRate;
	}
}
