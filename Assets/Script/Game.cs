using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour {
	public const float HIT_MARGIN = 0.04f;

	public delegate void FretPressAction(bool on, int fret);
	public event FretPressAction FretPressEvent;

	public static Game Instance {
		get;
		private set;
	} = null;

	private YargInput input;

	public float songTime = -1f;
	public float songSpeed = 8f;
	public List<NoteInfo> chart = new() {
		new(0.0f, 0),
		new(0.4f, 1),
		new(0.8f, 2),
		new(1.2f, 3),
		new(1.6f, 4),
		new(2.0f, 3),
		new(2.4f, 2),
		new(2.8f, 1),
		new(3.2f, 0),
	};

	public bool StrumThisFrame {
		get;
		private set;
	} = false;

	private void Awake() {
		Instance = this;

		input = new YargInput();
		input.Enable();

		input._5Fret.Green.started += _ => FretPress(0);
		input._5Fret.Red.started += _ => FretPress(1);
		input._5Fret.Yellow.started += _ => FretPress(2);
		input._5Fret.Blue.started += _ => FretPress(3);
		input._5Fret.Orange.started += _ => FretPress(4);
		input._5Fret.Strum.started += _ => Strum(true);

		input._5Fret.Green.canceled += _ => FretRelease(0);
		input._5Fret.Red.canceled += _ => FretRelease(1);
		input._5Fret.Yellow.canceled += _ => FretRelease(2);
		input._5Fret.Blue.canceled += _ => FretRelease(3);
		input._5Fret.Orange.canceled += _ => FretRelease(4);
		input._5Fret.Strum.canceled += _ => Strum(false);
	}

	private void Update() {
		songTime += Time.deltaTime;
	}

	private void LateUpdate() {
		StrumThisFrame = false;
	}

	private void FretPress(int fret) {
		FretPressEvent?.Invoke(true, fret);
	}

	private void FretRelease(int fret) {
		FretPressEvent?.Invoke(false, fret);
	}

	private void Strum(bool on) {
		StrumThisFrame = on;
	}
}