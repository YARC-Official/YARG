using UnityEngine;

public class Track : MonoBehaviour {
	[SerializeField]
	private MeshFilter meshFilter;

	[SerializeField]
	private Color[] fretColors;
	[SerializeField]
	private GameObject fret;

	private void Start() {
		// Spawn in frets
		for (int i = 0; i < 5; i++) {
			var fretObj = Instantiate(fret, transform);
			fretObj.transform.localPosition = new Vector3(i * 0.4f - 0.8f, 0.01f, -1.75f);

			fretObj.GetComponent<Fret>().SetColor(fretColors[i]);
		}
	}

	private void Update() {
		var uvs = meshFilter.mesh.uv;
		for (int i = 0; i < uvs.Length; i++) {
			uvs[i] += new Vector2(0f, Time.deltaTime * 4f);
		}
		meshFilter.mesh.uv = uvs;
	}
}