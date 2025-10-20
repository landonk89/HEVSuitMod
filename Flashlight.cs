using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace HEVSuitMod
{
	/// <summary>
	/// Simple HL1 style flashlight
	/// </summary>
	public class Flashlight : MonoBehaviour
	{
		//private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.Flashlight");

		// Singleton
		public static Flashlight Instance { get; private set; }

		private Light lightSource;
		private AudioSource audioSource; // TODO: use BetterAudio
		public bool isOn;
		private float batteryLevel = 1f; // 0..1
		private float batteryDrainRate = 0.01f;
		private float batteryChargeRate = 0.05f;

		private void Awake()
		{
			if (Instance == null)
				Instance = this;

			GameObject lightGo = new("FlashlightContainer");
			audioSource = lightGo.AddComponent<AudioSource>();
			audioSource.clip = HEVMod.Instance.Assets.LoadAsset<AudioClip>("assets/sounds/flashlight.wav");
			lightSource = lightGo.AddComponent<Light>();
			lightSource.type = LightType.Spot;
			lightSource.spotAngle = 65f;
			lightSource.enabled = false;
			lightGo.transform.SetPositionAndRotation(GamePlayerOwner.MyPlayer.CameraPosition.position, GamePlayerOwner.MyPlayer.CameraPosition.rotation);
			lightGo.transform.parent = GamePlayerOwner.MyPlayer.CameraPosition;
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.J)) // Temporary key
				Toggle();

			if (isOn)
			{
				batteryLevel -= batteryDrainRate * Time.deltaTime;
				if (batteryLevel <= 0f)
					Toggle();
			}
			else
			{
				if (batteryLevel < 1f)
					batteryLevel = Mathf.Clamp01(batteryLevel + batteryChargeRate * Time.deltaTime);
			}

			HudController.Instance.SetFlashlightBattery(batteryLevel, isOn);
		}

		public void Toggle()
		{
			if (isOn)
				TurnOff();
			else
				TurnOn();

			audioSource.Play();
		}

		private void TurnOn()
		{
			lightSource.enabled = true;
			isOn = true;
			HudController.Instance.FlashlightOn();
		}

		private void TurnOff()
		{
			lightSource.enabled = false;
			isOn = false;
			HudController.Instance.FlashlightOff();
		}
	}
}
