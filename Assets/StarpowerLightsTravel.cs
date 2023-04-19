using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Data;
using YARG.Input;
using YARG.Settings;
using YARG.UI;

public class StarpowerLightsTravel : MonoBehaviour{

	protected Vector3 spLightsStartPos;
	protected Vector3 spLightsEndPos = new(0, 0f, 20f);
	protected float spLightsDuration = 2f;
	protected float spLightsElapsedTime = 0;

	// Start is called before the first frame update
	void Awake() {
		spLightsStartPos = transform.position;
	}

	// Update is called once per frame
	void Update() {
		spLightsElapsedTime += Time.deltaTime;
		float percentageComplete = spLightsElapsedTime / spLightsDuration;

		transform.position = Vector3.Lerp(spLightsStartPos, spLightsStartPos + spLightsEndPos, percentageComplete);

		if (gameObject.transform.position == spLightsStartPos + spLightsEndPos) {

			Destroy(gameObject);
		}
	}
}
