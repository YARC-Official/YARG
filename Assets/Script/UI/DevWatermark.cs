using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG;
using Debug = UnityEngine.Debug;

public class DevWatermark : MonoBehaviour {

	[SerializeField]
	private TextMeshProUGUI watermarkText;

	// Start is called before the first frame update
	void Start() {
		// check if Constants.VERSION_TAG ends with "b"
		if (Constants.VERSION_TAG.beta) {
			watermarkText.text = $"<b>YARG {Constants.VERSION_TAG}</b>  Developer Build";
			watermarkText.gameObject.SetActive(true);
		} else {
			this.gameObject.SetActive(false);
		}

		// disable script
		this.enabled = false;
		Debug.Log("DevWatermark script disabled");
	}
}
