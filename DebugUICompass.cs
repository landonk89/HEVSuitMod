using EFT;
using UnityEngine;

namespace HEVSuitMod
{
	public class DebugUICompass : MonoBehaviour
	{
		private float radius = 50f;
		private Color circleColor = Color.white;
		private Color lineColor = Color.red;
		private float lineWidth = 2f;
		private float padding = 20f;
		private Texture2D lineTex;

		private void Start()
		{
			lineTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			lineTex.SetPixel(0, 0, Color.white);
			lineTex.Apply();
		}

		private void OnGUI()
		{
			if (GamePlayerOwner.MyPlayer == null)
				return;

			Vector2 center = new Vector2((Screen.width / 2) + radius, padding + radius);
			DrawCircle(center, radius, circleColor, 64);
			DrawBearingLine(center, radius, Compass.GetBearing(GamePlayerOwner.MyPlayer.LookDirection), lineColor);
		}

		// Draws a circle outline using GUI lines
		private void DrawCircle(Vector2 center, float radius, Color color, int segments)
		{
			GUI.color = color;
			Vector2 prevPoint = Vector2.zero;
			for (int i = 0; i <= segments; i++)
			{
				float angle = (float)i / segments * Mathf.PI * 2f;
				Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius + center;

				if (i > 0)
				{
					DrawLine(prevPoint, point, color, 1f);
				}
				prevPoint = point;
			}
			GUI.color = Color.white;
		}

		// Draws a bearing line from center in the given direction (0 = north)
		private void DrawBearingLine(Vector2 center, float radius, int bearing, Color color)
		{
			float angleRad = (bearing - 90f) * Mathf.Deg2Rad; // rotate so 0° = up
			Vector2 end = center + new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
			DrawLine(center, end, color, lineWidth);
		}

		private void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
		{
			// Save current GUI color
			Color oldColor = GUI.color;
			GUI.color = color;

			Vector2 delta = pointB - pointA;
			float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
			float length = delta.magnitude;

			Matrix4x4 oldMatrix = GUI.matrix;

			GUIUtility.RotateAroundPivot(angle, pointA);
			GUI.DrawTexture(new Rect(pointA.x, pointA.y - (width * 0.5f), length, width), lineTex);
			GUI.matrix = oldMatrix;

			GUI.color = oldColor;
		}
	}
}
