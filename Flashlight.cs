using BepInEx.Logging;
using System;
using EFT;
using UnityEngine;

namespace HEVSuitMod;

/// <summary>
/// Simple HL1 style flashlight
/// </summary>
public class Flashlight : MonoBehaviour
{
	//private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.Flashlight");
	private Light lightSource;
	private AudioSource audioSource; // TODO: use BetterAudio
	private bool isOn = false;
	private bool lowBattery = false;
	private float lowBatteryThreshold = 0.25f;
	private float batteryLevel = 1f; // 0..1
	private float batteryDrainRate = 0.01f;
	private float batteryChargeRate = 0.05f;

	public event Action<bool> Toggled;
	public event Action<float> BatteryUpdate;
	public event Action<bool> BatteryLow;

	private void Awake()
	{
		GameObject flashlight = new("FlashlightContainer");
		audioSource = flashlight.AddComponent<AudioSource>();
		audioSource.clip = HEVMod.Instance.Assets.LoadAsset<AudioClip>("assets/sounds/flashlight.wav");
		lightSource = flashlight.AddComponent<Light>();
		lightSource.type = LightType.Spot;
		lightSource.spotAngle = 65f;
		lightSource.enabled = false;
		flashlight.transform.SetPositionAndRotation(GamePlayerOwner.MyPlayer.CameraPosition.position, GamePlayerOwner.MyPlayer.CameraPosition.rotation);
		flashlight.transform.parent = GamePlayerOwner.MyPlayer.CameraPosition;
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

		BatteryUpdate.Invoke(batteryLevel);

		bool lowBatteryThisFrame = batteryLevel < lowBatteryThreshold;
		if (lowBatteryThisFrame != lowBattery)
		{
			lowBattery = lowBatteryThisFrame;
			BatteryLow.Invoke(lowBattery);
		}
	}

	public void Toggle()
	{
		if (isOn)
		{
			lightSource.enabled = false;
			isOn = false;
		}
		else
		{
			lightSource.enabled = true;
			isOn = true;
		}

		audioSource.Play();
		Toggled.Invoke(isOn);
	}
}
