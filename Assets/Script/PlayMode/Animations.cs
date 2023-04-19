using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Data;
using YARG.Input;
using YARG.Settings;
using YARG.UI;

namespace YARG.PlayMode {
	public class Animations : MonoBehaviour {

		protected AbstractTrack abstractTrack;
		protected CommonTrack commonTrack;

		// Overdrive animation parameters
		protected Vector3 trackStartPos;
		protected Vector3 trackEndPos = new(0, 0.08f, 0.13f);
		protected float spAnimationDuration = 0.2f;
		protected float elapsedTimeAnim = 0;
		protected bool gotStartPos = false;
		protected bool depressed = false;
		protected bool ascended = false;
		protected bool resetTime = false;

		// Overdrive particles animation parameters
		protected Vector3 particleStartPos;
		protected Vector3 particle2StartPos;
		protected Vector3 particleEndPos;
		protected float particleAnimationDuration = 0.7f;
		protected float elapsedTimeParticle = 0;

		// Start is called before the first frame update
		void Start() {
			abstractTrack = transform.GetComponent<AbstractTrack>();
		}

		// Update is called once per frame
		void Update() {

		}

		protected void StarpowerTrackAnim() {
			// Start track animation
			elapsedTimeAnim += Time.deltaTime;
			float percentageComplete = elapsedTimeAnim / spAnimationDuration;
			if (!depressed && !ascended) {
				spAnimationDuration = 0.065f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos, trackStartPos + trackEndPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					resetTime = true;
					depressed = true;
				}
			}

			if (resetTime) {
				elapsedTimeAnim = 0f;
				resetTime = false;
			}

			// End track animation
			if (depressed && !ascended) {
				spAnimationDuration = 0.2f;
				commonTrack.trackCamera.transform.position = Vector3.Lerp(trackStartPos + trackEndPos, trackStartPos, percentageComplete);

				if (commonTrack.trackCamera.transform.position == trackStartPos + trackEndPos) {
					resetTime = true;
					ascended = true;
				}
			}
		}

		protected void StarpowerTrackAnimReset() {
			if (!gotStartPos) {
				trackStartPos = commonTrack.trackCamera.transform.position;
				gotStartPos = true;
			}

			depressed = false;
			ascended = false;
			elapsedTimeAnim = 0f;
		}
	}
}
