using UnityEngine;

namespace HEVSuitMod
{
	public static class Compass
	{
		public static int GetBearing(Vector3 lookDir)
		{
			lookDir = -lookDir;
			lookDir.y = 0f;
			if (lookDir == Vector3.zero)
				return 0;

			float angle = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
			if (angle < 0f)
				angle += 360f;

			int bearing = ((int)angle + 270) % 360;
			return bearing;
		}
	}
}
