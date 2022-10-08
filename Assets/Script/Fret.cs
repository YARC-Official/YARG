using UnityEngine;

public class Fret : MonoBehaviour {
	[SerializeField]
	private Material pressedMaterial;
	[SerializeField]
	private Material releasedMaterial;

	[SerializeField]
	private MeshRenderer meshRenderer;

	private Color color;

	public bool IsPressed {
		get;
		private set;
	} = false;

	public void SetColor(Color c) {
		color = c;
		meshRenderer.material.color = c;
	}

	public void SetPressed(bool pressed) {
		meshRenderer.material = pressed ? pressedMaterial : releasedMaterial;
		meshRenderer.material.color = color;

		IsPressed = pressed;
	}
}