using UnityEngine;

namespace HEVSuitMod
{
	public static class Compass
	{
		// TODO: Make sure this works when using something other than MyPlayer.LookDirection
		public static int GetBearing(Vector3 direction)
		{
			direction = -direction; // FIXME: Player.LookDirection needs to be negated to get the correct direction
			direction.y = 0f;
			if (direction == Vector3.zero)
				return 0;

			float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
			if (angle < 0f)
				angle += 360f;

			int bearing = ((int)angle + 270) % 360;
			return bearing;
		}
	}
}
