namespace UnityEngine {
	public static class VectorExtensions {
		/*
		 *	Vector2 Utils
		 */

		public static Vector2 WithX(this Vector2 vec, float x) {
			return new Vector2(x, vec.y);
		}

		public static Vector2 WithY(this Vector2 vec, float y) {
			return new Vector2(vec.x, y);
		}

		public static Vector2 AddX(this Vector2 vec, float x) {
			return new Vector2(vec.x + x, vec.y);
		}

		public static Vector2 AddY(this Vector2 vec, float y) {
			return new Vector2(vec.x, vec.y + y);
		}

		public static Vector2 InvertX(this Vector2 vec) {
			return new Vector2(-vec.x, vec.y);
		}

		public static Vector2 InvertY(this Vector2 vec) {
			return new Vector2(vec.x, -vec.y);
		}

		public static Vector2 Invert(this Vector2 vec) {
			return new Vector2(-vec.x, -vec.y);
		}

		public static Vector2 Abs(this Vector2 vec) {
			return new Vector2(Mathf.Abs(vec.x), Mathf.Abs(vec.y));
		}

		/*
		 *	Vector3 Utils
		 */

		public static Vector3 WithX(this Vector3 vec, float x) {
			return new Vector3(x, vec.y, vec.z);
		}

		public static Vector3 WithY(this Vector3 vec, float y) {
			return new Vector3(vec.x, y, vec.z);
		}

		public static Vector3 WithZ(this Vector3 vec, float z) {
			return new Vector3(vec.x, vec.y, z);
		}

		public static Vector3 AddX(this Vector3 vec, float x) {
			return new Vector3(vec.x + x, vec.y, vec.z);
		}

		public static Vector3 AddY(this Vector3 vec, float y) {
			return new Vector3(vec.x, vec.y + y, vec.z);
		}

		public static Vector3 AddZ(this Vector3 vec, float z) {
			return new Vector3(vec.x, vec.y, vec.z + z);
		}

		public static Vector3 InvertX(this Vector3 vec) {
			return new Vector3(-vec.x, vec.y, vec.z);
		}

		public static Vector3 InvertY(this Vector3 vec) {
			return new Vector3(vec.x, -vec.y, vec.z);
		}

		public static Vector3 InvertZ(this Vector3 vec) {
			return new Vector3(vec.x, vec.y, -vec.z);
		}

		public static Vector3 Invert(this Vector3 vec) {
			return new Vector3(-vec.x, -vec.y, -vec.z);
		}

		public static Vector3 Abs(this Vector3 vec) {
			return new Vector3(Mathf.Abs(vec.x), Mathf.Abs(vec.y), Mathf.Abs(vec.z));
		}

		/*
		 *	Vector2Int Utils
		 */

		public static Vector2Int WithX(this Vector2Int vec, int x) {
			return new Vector2Int(x, vec.y);
		}

		public static Vector2Int WithY(this Vector2Int vec, int y) {
			return new Vector2Int(vec.x, y);
		}

		public static Vector2Int AddX(this Vector2Int vec, int x) {
			return new Vector2Int(vec.x + x, vec.y);
		}

		public static Vector2Int AddY(this Vector2Int vec, int y) {
			return new Vector2Int(vec.x, vec.y + y);
		}

		public static Vector2Int InvertX(this Vector2Int vec) {
			return new Vector2Int(-vec.x, vec.y);
		}

		public static Vector2Int InvertY(this Vector2Int vec) {
			return new Vector2Int(vec.x, -vec.y);
		}

		public static Vector2Int Invert(this Vector2Int vec) {
			return new Vector2Int(-vec.x, -vec.y);
		}

		public static Vector2Int Abs(this Vector2Int vec) {
			return new Vector2Int(Mathf.Abs(vec.x), Mathf.Abs(vec.y));
		}

		/*
		 *	Vector3Int Utils
		 */

		public static Vector3Int WithX(this Vector3Int vec, int x) {
			return new Vector3Int(x, vec.y, vec.z);
		}

		public static Vector3Int WithY(this Vector3Int vec, int y) {
			return new Vector3Int(vec.x, y, vec.z);
		}

		public static Vector3Int WithZ(this Vector3Int vec, int z) {
			return new Vector3Int(vec.x, vec.y, z);
		}

		public static Vector3Int AddX(this Vector3Int vec, int x) {
			return new Vector3Int(vec.x + x, vec.y, vec.z);
		}

		public static Vector3Int AddY(this Vector3Int vec, int y) {
			return new Vector3Int(vec.x, vec.y + y, vec.z);
		}

		public static Vector3Int AddZ(this Vector3Int vec, int z) {
			return new Vector3Int(vec.x, vec.y, vec.z + z);
		}

		public static Vector3Int InvertX(this Vector3Int vec) {
			return new Vector3Int(-vec.x, vec.y, vec.z);
		}

		public static Vector3Int InvertY(this Vector3Int vec) {
			return new Vector3Int(vec.x, -vec.y, vec.z);
		}

		public static Vector3Int InvertZ(this Vector3Int vec) {
			return new Vector3Int(vec.x, vec.y, -vec.z);
		}

		public static Vector3Int Invert(this Vector3Int vec) {
			return new Vector3Int(-vec.x, -vec.y, -vec.z);
		}

		public static Vector3Int Abs(this Vector3Int vec) {
			return new Vector3Int(Mathf.Abs(vec.x), Mathf.Abs(vec.y), Mathf.Abs(vec.z));
		}

		/*
		 *  VectorInt -> Vector
		 */

		public static Vector3 ToVector3(this Vector2Int v) {
			return new Vector3(v.x, v.y);
		}

		public static Vector3 ToVector3(this Vector3Int v) {
			return new Vector3(v.x, v.y, v.z);
		}

		public static Vector2 ToVector2(this Vector2Int v) {
			return new Vector2(v.x, v.y);
		}

		public static Vector2 ToVector2(this Vector3Int v) {
			return new Vector2(v.x, v.y);
		}

		/*
		 *  Vector -> VectorInt
		 */

		public static Vector3Int ToVector3Int(this Vector3 v) {
			return new Vector3Int((int) v.x, (int) v.y, (int) v.z);
		}

		public static Vector3Int ToVector3Int(this Vector2 v) {
			return new Vector3Int((int) v.x, (int) v.y, 0);
		}

		public static Vector2Int ToVector2Int(this Vector3 v) {
			return new Vector2Int((int) v.x, (int) v.y);
		}

		public static Vector2Int ToVector2Int(this Vector2 v) {
			return new Vector2Int((int) v.x, (int) v.y);
		}
	}
}