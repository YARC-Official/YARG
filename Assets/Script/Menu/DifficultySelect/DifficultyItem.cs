using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;

namespace YARG.Menu.DifficultySelect
{
    public class DifficultyItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _header;
        [SerializeField]
        private TextMeshProUGUI _body;

        [SerializeField]
        private Image _selectionFill;
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [field: SerializeField]
        public NavigatableButton Button { get; private set; }

        public void Initialize(string header, string body, UnityAction action)
        {
            _header.gameObject.SetActive(true);
            _header.text = header;

            _body.text = body;
            Button.SetOnClickEvent(action);
        }

        public void Initialize(string body, UnityAction action)
        {
            _header.gameObject.SetActive(false);

            _body.text = body;
            Button.SetOnClickEvent(action);
        }

        /// <summary>
        /// Tints the header/body text, using the selected accent while selected.
        /// Each text's own alpha is preserved.
        /// </summary>
        public void SetAccentColors(Color textColor, Color selectedTextColor)
        {
            ApplyAccent(Button.Selected);

            Button.SelectionStateChanged += (_, selected, _) => ApplyAccent(selected);

            void ApplyAccent(bool selected)
            {
                var color = selected ? selectedTextColor : textColor;
                _header.color = color.WithAlpha(_header.color.a);
                _body.color = color.WithAlpha(_body.color.a);
            }
        }

        /// <summary>
        /// Shows the item as a non-interactive menu title: header text only, no
        /// body, no action. Used so a sub-menu identifies itself.
        /// </summary>
        public void InitializeAsTitle(string header)
        {
            // A title never joins the navigation group, so nothing ever
            // deselects it once the pointer selects it. Keep the pointer away
            // from it entirely; the row is a label, not a button.
            Button.SelectOnHover = false;

            _header.gameObject.SetActive(true);
            _header.text = header;
            _body.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shrinks the body text to the header's font size. Used by rows whose
        /// body lists several settings and would otherwise dominate the menu.
        /// </summary>
        public void UseSmallBodyText()
        {
            _body.fontSize = _header.fontSize;
        }

        /// <summary>
        /// Dims the item and disables interaction (used to show a fixed, non-editable choice).
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _canvasGroup.alpha = interactable ? 1f : 0.3f;
            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
        }

        /// <summary>
        /// Fills the entire button background with the given color while the item
        /// is selected, behind the selected text color. The colored variant's
        /// selection background carries no sprite — a sprite-less Image draws a
        /// plain full-rect quad — so the color alone paints an edge-to-edge
        /// ready/sit-out style fill, without a per-color sprite.
        /// </summary>
        public void SetSelectionFill(Color fillColor)
        {
            _selectionFill.color = fillColor;
        }
    }
}