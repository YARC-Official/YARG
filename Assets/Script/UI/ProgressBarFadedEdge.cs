using UnityEngine;

namespace YARG.UI {
    [ExecuteInEditMode]
    public class ProgressBarFadedEdge : MonoBehaviour {
		[SerializeField]
		private RectTransform progressImg;
        [SerializeField]
		private RectTransform progressMask;
		[SerializeField]
		private float maskFullPosition;

		[Space]
		[SerializeField]
        [Range(0, 1)]
        private float _value;

		void OnValidate() {
			SetProgress(_value);
		}

        /// <summary>
		/// Sets position of the progress bar.
		/// </summary>
		/// <param name="progress">A value from 0 to 1 inclusive.</param>
        public void SetProgress(float progress) {
			progress = Mathf.Clamp01(progress);
			float mW = maskFullPosition > 0 ? maskFullPosition : progressMask.rect.width;
			float iW = progressImg.rect.width;
			var mPos = progressMask.anchoredPosition;
			var iPos = progressImg.anchoredPosition;

			mPos.x = progress * mW - iW;
			iPos.x = -mPos.x;

			progressMask.anchoredPosition = mPos;
			progressImg.anchoredPosition = iPos;
        }
    }
}
