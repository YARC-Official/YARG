using UnityEngine;

namespace YARG.PlayMode {
	public class TrackAnimations : MonoBehaviour {
		/*
		TODO: Let's organize this a bit more, yeah?
		*/

		protected AbstractTrack abstractTrack;
		protected CommonTrack commonTrack;

		// Overdrive shake animation parameters
		protected Vector3 trackStartPos;
		protected Vector3 trackEndPos = new(0, 0.08f, 0.13f);
		protected float spShakeDuration = 0.2f;
		protected float spShakeElapsedTime = 0;
		protected bool spShakegotStartPos = false;
		protected bool spShakeDepressed = false;
		public bool spShakeAscended = false;
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

		// Kick Camera shake animation parameters
		[Space]
		[SerializeField]
		protected Vector3 trackEndPosKick = new Vector3(0, -0.04f, 0.01f);
		[SerializeField]
		protected float kickShakeMiddleDuration = 0.005f; 
		[SerializeField]
		protected float kickShakeTotalDuration = 0.125f;

		protected Vector3 initialCameraPos;
		protected bool initialCameraPosGot;
		protected float kickShakeDuration;
		protected float kickShakeElapsedTime = 0;
		protected bool kickShakeGone = false;
		protected bool kickShakeReturned = false;
		protected bool kickShakeResetTime = false;
		protected bool executeKickShake = false;

		// Kick Flash animation
		protected Sprite kickFlashSprite;
		protected Animation kickFlashAnimation;
		protected Animator kickFlashAnimator;

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
			trackStartPos = commonTrack.TrackCamera.transform.position;
			spLightsStartPos = commonTrack.starPowerLightIndicators.transform.position;

			kickFlashSprite = commonTrack.kickFlash.GetComponent<Sprite>();
			kickFlashAnimation = commonTrack.kickFlash.GetComponent<Animation>();
			kickFlashAnimator = commonTrack.kickFlash.GetComponent<Animator>();
			executeKickShake = false;
		}

		void Update() {

			KickShakeCameraAnim();
			
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
				commonTrack.TrackCamera.transform.position = Vector3.Lerp(trackStartPos, trackStartPos + trackEndPos, percentageComplete);

				if (commonTrack.TrackCamera.transform.position == trackStartPos + trackEndPos) {
					spShakeResetTime = true;
					spShakeDepressed = true;
				}
			}

			if (spShakeResetTime) {
				spShakeElapsedTime = 0f;
				spShakeResetTime = false;
				percentageComplete = 0;
			}

			// End track animation
			if (spShakeDepressed && !spShakeAscended) {
				spShakeDuration = 0.2f;
				commonTrack.TrackCamera.transform.position = Vector3.Lerp(trackStartPos + trackEndPos, trackStartPos, percentageComplete);

				if (commonTrack.TrackCamera.transform.position == trackStartPos) {
					spShakeResetTime = true;
					spShakeAscended = true;
				}
			}
		}

		public void StarpowerTrackAnimReset() {
			if (!spShakegotStartPos) {
				trackStartPos = commonTrack.TrackCamera.transform.position; // This gets the initial track camera position, Kick shake animation also uses this.
				spShakegotStartPos = true;
			}

			spShakeDepressed = false;
			spShakeAscended = false;
			spShakeElapsedTime = 0f;
		}

		public void KickShakeCameraAnim() {
			

			float percentageComplete = kickShakeElapsedTime / kickShakeDuration;
			
			if(!initialCameraPosGot) {

				initialCameraPos = commonTrack.TrackCamera.transform.position;
				initialCameraPosGot = true;
			}

			if (executeKickShake) {
				
				
				if (!kickShakeGone && !kickShakeReturned) {
					kickShakeElapsedTime += Time.deltaTime;
					kickShakeDuration = kickShakeMiddleDuration;

					//commonTrack.TrackCamera.transform.position = Vector3.Lerp(initialCameraPos, initialCameraPos + trackEndPosKick, percentageComplete); <-- This lerp was
					commonTrack.TrackCamera.transform.position = initialCameraPos + trackEndPosKick;                                                                                                                                  // a Nan issue so I changed to lerp to instant position assign. Should present no issues since "MiddleDuration" lasted less than a frame. - Mia


					if (commonTrack.TrackCamera.transform.position == initialCameraPos + trackEndPosKick) {
						kickShakeResetTime = true;
						kickShakeGone = true;
					}
				}

				if (kickShakeResetTime) {
					kickShakeElapsedTime = 0f;
					kickShakeResetTime = false;
					percentageComplete = 0;
				}
				

				if (kickShakeGone && !kickShakeReturned) {
					kickShakeElapsedTime += Time.deltaTime;
					
					kickShakeDuration = kickShakeTotalDuration;
					commonTrack.TrackCamera.transform.position = Vector3.Lerp(initialCameraPos + trackEndPosKick, initialCameraPos, percentageComplete);

					if (commonTrack.TrackCamera.transform.position == initialCameraPos) {
						executeKickShake = false;
						kickShakeResetTime = true;
						kickShakeReturned = false;
						kickShakeGone = false;
					}
				}
			}
		}

		public void PlayKickShakeCameraAnim() {
			kickShakeResetTime = true;
			executeKickShake = true;
		}

		public void PlayKickFlashAnim() {
			StopKickFlashAnim();

			commonTrack.kickFlash.SetActive(true);
			kickFlashAnimator.Play(0, 0, 0f);
			/*if (kickFlashAnimator.GetCurrentAnimatorStateInfo(0).length > kickFlashAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime) {
				commonTrack.kickFlash.SetActive(true);
			} else {
				commonTrack.kickFlash.SetActive(false);
			}*/
			
		}

		public void StopKickFlashAnim() {

			//kickFlashAnimator.Stop();
			//kickFlashAnimator.Rewind();

		}
	}
}
