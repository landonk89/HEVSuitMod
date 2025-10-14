#if DEBUG
using EFT;
using System.Text;
using UnityEngine;

namespace HEVSuitMod
{
	// Temporary!! Super inefficient...
	public class DebugUICompass : MonoBehaviour
	{
		private readonly string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
		private const int NUM_DIRECTIONS = 8;
		private int range = 3;
		private int normalFontSize = 22;
		private int highlightFontSize = 28;
		private GUIStyle style;

		void OnGUI()
		{
			if (GamePlayerOwner.MyPlayer == null)
				return;

			// Stupid, but can't you call gui methods outside of OnGUI
			if (style == null)
			{
				style = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.UpperCenter,
					richText = true,
					fontSize = normalFontSize,
					normal = { textColor = Color.white }
				};
			}

			float heading = Compass.GetBearing(GamePlayerOwner.MyPlayer.LookDirection);
			string compass = GetCompassString(heading);
			GUI.Label(new Rect(0, 10, Screen.width, 40), compass, style);
		}

		private string GetCompassString(float heading)
		{
			int index = GetFacingIndex(heading);

			// Build string with fixed pattern "Dir - Dir - Dir"
			StringBuilder sb = new StringBuilder();

			for (int offset = -range; offset <= range; offset++)
			{
				int dirIndex = (index + offset + NUM_DIRECTIONS) % NUM_DIRECTIONS;
				string dir = directions[dirIndex];

				if (offset == 0)
				{
					// Highlight the current facing direction with larger font
					sb.Append($"<size={highlightFontSize}><b>{dir}</b></size>");
				}
				else
				{
					sb.Append(dir);
				}

				// Add " - " separator *between* entries, not after last one
				if (offset < range)
					sb.Append(" - ");
			}

			return sb.ToString();
		}

		private int GetFacingIndex(float heading)
		{
			float normalized = (heading + 22.5f) % 360;
			int index = Mathf.FloorToInt(normalized / 45f);
			return index;
		}
	}
}
#endif