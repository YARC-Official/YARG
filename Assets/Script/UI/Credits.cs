using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.UI {
	public class Credits : MonoBehaviour, IDragHandler, IScrollHandler {
		[SerializeField]
		private TextAsset _creditsFile;
		[SerializeField]
		private Transform _creditsContainer;
		[SerializeField]
		private ScrollRect _scrollRect;

		[Space]
		[SerializeField]
		private GameObject _headerTemplate;
		[SerializeField]
		private GameObject _cardTemplate;

		private float _scrollRate = 40f;

		private void OnEnable() {
			// Set navigation scheme
			Navigator.Instance.PushScheme(new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => {
					MainMenu.Instance.ShowMainMenu();
				})
			}, true));

			_scrollRect.verticalNormalizedPosition = 1f;
		}

		private void OnDisable() {
			Navigator.Instance.PopScheme();
		}

		private void Start() {
			var json = JsonConvert.DeserializeObject<
				Dictionary<string, Dictionary<string, JObject>>
			>(_creditsFile.text);

			CreateHeader("gameStartedBy");
			CreateCredits(json["gameStartedBy"]);

			CreateHeader("leadArtist");
			CreateCredits(json["leadArtist"]);

			CreateHeader("leadCharter");
			CreateCredits(json["leadCharter"]);

			CreateHeader("contributors");
			CreateCredits(json["contributors"]);

			CreateHeader("specialThanks");
			CreateCredits(json["specialThanks"]);

			_headerTemplate.SetActive(false);
			_cardTemplate.SetActive(false);
		}

		private void Update() {
			// Return the scroll rate
			if (_scrollRate < 40f) {
				_scrollRate += Time.deltaTime * 10f;
				_scrollRate = Mathf.Min(_scrollRate, 40f);
			}

			if (_scrollRate > 0f) {
				// Use velocity, so the scroll speed stays consistent in different lengths
				_scrollRect.velocity = new Vector2(0f, _scrollRate);
			}
		}

		private void CreateHeader(string id) {
			var header = Instantiate(_headerTemplate, _creditsContainer);
			header.GetComponent<LocalizeStringEvent>().StringReference = new LocalizedString {
				TableReference = "Main",
				TableEntryReference = $"Credits.Header.{id}"
			};
		}

		private void CreateCredits(Dictionary<string, JObject> credits) {
			foreach (var (name, info) in credits) {
				var card = Instantiate(_cardTemplate, _creditsContainer);
				card.GetComponent<CreditCard>().SetFromJObject(name, info);
			}
		}

		public void OnDrag(PointerEventData eventData) {
			_scrollRate = -10f;
		}

		public void OnScroll(PointerEventData eventData) {
			_scrollRate = -10f;
		}
	}
}