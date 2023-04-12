using System;
using System.IO;
using UnityEngine;

namespace YARG.Util {
	public static class Utils {
		/// <returns>
		/// A unique hash for <paramref name="a"/>.
		/// </returns>
		public static string Hash(string a) {
			return Hash128.Compute(a).ToString();
		}

		/// <summary>
		/// Checks if the path <paramref name="a"/> is equal to the path <paramref name="b"/>.<br/>
		/// Platform specific case sensitivity is taken into account.
		/// </summary>
		public static bool PathsEqual(string a, string b) {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			
			// Linux is case sensitive
			return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.CurrentCulture);
			
#else

			// Windows and OSX are not case sensitive
			return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.CurrentCultureIgnoreCase);

#endif
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to screen space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the screen space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect RectTransformToScreenSpace(RectTransform transform) {
			// https://answers.unity.com/questions/1013011/convert-recttransform-rect-to-screen-space.html
			Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale.Abs());
			return new Rect((Vector2) transform.position - (size * transform.pivot), size);
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the viewport space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect RectTransformToViewportSpace(RectTransform transform) {
			Rect rect = RectTransformToScreenSpace(transform);
			rect.width /= Screen.width;
			rect.height /= Screen.height;
			rect.x /= Screen.width;
			rect.y /= Screen.height;

			return rect;
		}

		/// <returns>
		/// The inputed note split into a note + octave.
		/// </returns>
		public static (float note, int octave) SplitNoteToOctaveAndNote(float note) {
			float outNote = note;
			int octave = 0;

			while (outNote > 12f) {
				octave++;
				outNote -= 12f;
			}

			return (outNote, octave);
		}

		/// <param name="v">The linear volume between 0 and 1.</param>
		/// <returns>
		/// The linear volume converted to decibels.
		/// </returns>
		public static float VolumeFromLinear(float v) {
			return Mathf.Log10(Mathf.Min(v + float.Epsilon, 1f)) * 20f;
		}
	}
}
