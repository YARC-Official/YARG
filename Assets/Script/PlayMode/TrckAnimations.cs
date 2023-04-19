using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Data;
using YARG.Input;
using YARG.Settings;
using YARG.UI;

namespace YARG.PlayMode {
	public class TrckAnimations : MonoBehaviour {

		protected AbstractTrack abstractTrack;
		protected CommonTrack commonTrack;

		// Overdrive shake animation parameters
		protected Vector3 trackStartPos;
		protected Vector3 trackEndPos = new(0, 0.08f, 0.13f);
		protected float spShakeDuration = 0.2f;
		protected float spShakeElapsedTime = 0;
		protected bool spShakegotStartPos = false;
		protected bool spShakeDepressed = false;
		protected bool spShakeAscended = false;
		protected bool spShakeResetTime = false;

		// Overdrive particles animation parameters
		protected Vector3 spParticleStartPos;
		protected Vector3 spParticle2StartPos;
		protected Vector3 spParticleEndPos = new(-5f, 0f, 40f);
		protected Vector3 spParticle2EndPos = new(5f, 0f, 40f);
		protected float spParticleDuration = 2f;
		protected float spParticleElapsedTime = 0;
		protected bool spParticlePlayed = false;
		protected bool spParticleReset = false;


		// Overdrive light indicators animation parameters
		public Vector3 spLightsStartPos;
		public Vector3 spLightsEndPos = new(0, 0f, 20f);
		protected float spLightsDuration = 2f;
		protected float spLightsElapsedTime = 0;
		public bool spLightsPlayed = false;
		protected bool spLightsReset = false;

		[Space]
		[SerializeField]
		protected AnimationCurve spParticleAnimCurve;

		private void Awake() {
			commonTrack = GetComponent<CommonTrack>();
			abstractTrack = GetComponent<AbstractTrack>();
		}

		// Start is called before the first frame update
		void Start() {
			abstractTrack = transform.GetComponent<AbstractTrack>();
			spParticleStartPos = commonTrack.starPowerParticles.transform.position;
			spParticle2StartPos = commonTrack.starPowerParticles2.transform.position;

			spLightsStartPos = commonTrack.starPowerLightIndicators.transform.position;

		}

		// Update is called once per frame
		void Update() {

		}

		public void StarpowerLightsAnimSingleFrame() {
			var spLights = Instantiate(commonTrack.starPowerLightIndicators, spLightsStartPos, commonTrack.starPowerLightIndicators.transform.rotation);
			spLights.SetActive(true);
		}

		public void StarpowerLightsAnim() {
			

			if (!spLightsPlayed) {
				GameObject spLights = Instantiate(commonTrack.starPowerLightIndicators, spLightsStartPos, commonTrack.starPowerLightIndicators.transform.rotation);
				
				spLights.SetActive(true);
				spLightsPlayed = true;
			}

		}

		public void StarpowerLightsAnimReset() {

			spLightsPlayed = false;
		}

		public void StarpowerParticleAnim() {
			spParticleElapsedTime += Time.deltaTime;
			float percentageComplete = spParticleElapsedTime / spParticleDuration;

			if (!spParticlePlayed) {
				spParticleReset = false;
				commonTrack.starPowerParticles.Play();
				commonTrack.starPowerParticles2.Play();
				commonTrack.starPowerParticlesLight.gameObject.SetActive(true);
				commonTrack.starPowerParticles2Light.gameObject.SetActive(true);
				spParticlePlayed = true;
			}
			if (commonTrack.starPowerParticles.transform.position != spParticleStartPos + spParticleEndPos) {
				commonTrack.starPowerParticles.transform.position = Vector3.Lerp(spParticleStartPos, spParticleStartPos + spParticleEndPos, spParticleAnimCurve.Evaluate(percentageComplete));
				commonTrack.starPowerParticles2.transform.position = Vector3.Lerp(spParticle2StartPos, spParticle2StartPos + spParticle2EndPos, spParticleAnimCurve.Evaluate(percentageComplete));
			}
		}

		public void StarpowerParticleAnimReset() {
			if (!spParticleReset) {
				spParticleElapsedTime = 0f;
				commonTrack.starPowerParticles.transform.position = spParticleStartPos;
				commonTrack.starPowerParticles2.transform.position = spParticle2StartPos;
				commonTrack.starPowerParticlesLight.gameObject.SetActive(false);
				commonTrack.starPowerParticles2Light.gameObject.SetActive(false);
				commonTrack.starPowerParticles.Stop();
				commonTrack.starPowerParticles2.Stop();
				spParticleReset = true;
				spParticlePlayed = false;
				
			}
		}


		public void StarpowerTrackAnim() {
			// Start track animation
			spShakeElapsedTime += Time.deltaTime;
			float percentageComplete = spShakeElapsedTime / spShakeDuration;
			if (!spShakeDepressed && !spShakeAscended) {
				spShakeDuration = 0.065f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos, trackStartPos + trackEndPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					spShakeResetTime = true;
					spShakeDepressed = true;
				}
			}

			if (spShakeResetTime) {
				spShakeElapsedTime = 0f;
				spShakeResetTime = false;
			}

			// End track animation
			if (spShakeDepressed && !spShakeAscended) {
				spShakeDuration = 0.2f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos + trackEndPos, trackStartPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					spShakeResetTime = true;
					spShakeAscended = true;
				}
			}
		}

		public void StarpowerTrackAnimReset() {
			if (!spShakegotStartPos) {
				trackStartPos = commonTrack.trackCamera.transform.position;
				spShakegotStartPos = true;
			}

			spShakeDepressed = false;
			spShakeAscended = false;
			spShakeElapsedTime = 0f;
		}
	}
}
