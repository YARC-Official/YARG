using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlassParticleSpread : MonoBehaviour {

	private ParticleSystem particle;
	private float timeElapsed = 0;

	[SerializeField]
	public float radius = 1f;

    // Start is called before the first frame update
    void Start() {
		particle = transform.GetComponent<ParticleSystem>();
    }

    // Update is called once per frame
    void Update() {
		ParticleSystem.MainModule particleMain = particle.main;
		ParticleSystem.ShapeModule particleShape = particle.shape;
		float duration = particleMain.duration;
		float percentageComplete = timeElapsed / duration;

		if (particle.isPlaying) {
			timeElapsed += Time.deltaTime;

			particleShape.radius = Mathf.Lerp(0, radius, percentageComplete);

		}

		if(particleShape.radius == radius || !particle.isPlaying) {
			timeElapsed = 0;
			particleShape.radius = 0;

		}
	


    }
}
