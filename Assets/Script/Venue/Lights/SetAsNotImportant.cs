using UnityEngine;

namespace YARG.Venue {
	public class SetAsNotImportant : MonoBehaviour {
		private void Start() {
			var directionalLight = GetComponent<Light>();
			directionalLight.renderMode = LightRenderMode.ForceVertex;
		}
	}
}