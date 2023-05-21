using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.UI;

namespace YARG.Input {
	public enum MenuAction {
		//         // Guitar | Drums      |
		Confirm,   // Green  | Green      |
		Back,      // Red    | Red        |
		Shortcut1, // Yellow | Yellow Cym |
		Shortcut2, // Blue   | Blue Cym   |
		Shortcut3, // Orange | Green Cym  |
		Up,        // Strum  | Yellow     |
		Down,      // Strum  | Blue       |
		Pause,     // Pause  |            |
		More,      // Select | Kick       |
	}

	public readonly struct NavigationContext {
		/// <summary>
		/// The type of navigation.
		/// </summary>
		public readonly MenuAction Action;
		/// <summary>
		/// The input strategy that this event was invoked from. Can be null.
		/// </summary>
		public readonly InputStrategy InputStrategy;

		public NavigationContext(MenuAction action, InputStrategy strategy) {
			Action = action;
			InputStrategy = strategy;
		}

		public bool IsSameAs(NavigationContext other) {
			return other.Action == Action && other.InputStrategy == InputStrategy;
		}
	}

	public class Navigator : MonoBehaviour {
		private const float InputRepeatTime = 0.035f;
		private const float InputRepeatCooldown = 0.5f;

		private class HoldContext {
			public readonly NavigationContext Ctx;
			public float timer = InputRepeatCooldown;

			public HoldContext(NavigationContext ctx) {
				Ctx = ctx;
			}
		}

		public static Navigator Instance { get; private set; }

		public event Action<NavigationContext> NavigationEvent;

		private List<HoldContext> _heldInputs = new();
		private Stack<NavigationScheme> _schemeStack = new();

		private void Awake() {
			Instance = this;
			UpdateHelpBar().Forget();
		}

		private void Update() {
			// Update held inputs
			foreach (var heldInput in _heldInputs) {
				heldInput.timer -= Time.unscaledDeltaTime;

				if (heldInput.timer <= 0f) {
					heldInput.timer = InputRepeatTime;
					InvokeNavigationEvent(heldInput.Ctx);
				}
			}

			// TODO: Keyboard inputs for menus
			// UpdateKeyboardInput();
		}

		public void CallNavigationEvent(MenuAction action, InputStrategy strategy) {
			InvokeNavigationEvent(new NavigationContext(
				action,
				strategy
			));
		}

		public void StartNavigationHold(MenuAction action, InputStrategy strategy) {
			var ctx = new NavigationContext(
				action,
				strategy
			);

			// Skip if the input is already being held
			if (_heldInputs.Any(i => i.Ctx.IsSameAs(ctx))) {
				return;
			}

			InvokeNavigationEvent(ctx);
			_heldInputs.Add(new HoldContext(ctx));
		}

		public void EndNavigationHold(MenuAction action, InputStrategy strategy) {
			var ctx = new NavigationContext(
				action,
				strategy
			);

			_heldInputs.RemoveAll(i => i.Ctx.IsSameAs(ctx));
		}

		private void InvokeNavigationEvent(NavigationContext ctx) {
			NavigationEvent?.Invoke(ctx);

			if (_schemeStack.Count > 0) {
				_schemeStack.Peek().InvokeFuncs(ctx.Action);
			}
		}

		public void PushScheme(NavigationScheme scheme) {
			_schemeStack.Push(scheme);
			UpdateHelpBar().Forget();
		}

		public void PopScheme() {
			_schemeStack.Pop();
			UpdateHelpBar().Forget();
		}

		public void PopAllSchemes() {
			_schemeStack.Clear();
			UpdateHelpBar().Forget();
		}

		private async UniTask UpdateHelpBar() {
			// Wait one frame to update, in case another one gets pushed
			await UniTask.WaitForEndOfFrame(this);

			if (_schemeStack.Count <= 0) {
				HelpBar.Instance.gameObject.SetActive(false);
			} else {
				HelpBar.Instance.gameObject.SetActive(true);
				HelpBar.Instance.SetInfoFromScheme(_schemeStack.Peek());
			}
		}
	}
}