using UnityEngine;

public class Fret : MonoBehaviour {
	[SerializeField]
	private MeshRenderer meshRenderer;

	private Color color;

	public void SetColor(Color c) {
		color = c;
		meshRenderer.material.color = color;
	}
}