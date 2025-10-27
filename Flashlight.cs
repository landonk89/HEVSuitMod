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
	private const float BATT_LOW_THRESHOLD = 0.25f;
	private const float BATT_DRAIN_RATE = 0.01f;
	private const float BATT_CHARGE_RATE = 0.05f;

	public static Flashlight Instance { get; private set; }
	private AssetBundle assets = HEVMod.Instance.Assets;
	//private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.Flashlight");
	private Light lightSource;
	private AudioSource audioSource; // TODO: use BetterAudio
	private bool isOn = false;
	private bool lowBattery = false;
	private float batteryLevel = 1f; // 0..1

	public event Action<bool> Toggled;
	public event Action<float> BatteryUpdate;
	public event Action<bool> BatteryLow;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this);
			return;
		}
		else
			Instance = this;

		GameObject flashlight = new("FlashlightContainer");
		audioSource = flashlight.AddComponent<AudioSource>();
		audioSource.clip = assets.LoadAsset<AudioClip>("assets/sounds/flashlight.wav");
		lightSource = flashlight.AddComponent<Light>();
		lightSource.type = LightType.Spot;
		lightSource.spotAngle = 45f;
		lightSource.range = 50f;
		lightSource.cookie = assets.LoadAsset<Texture2D>("assets/sprites/hl2flashlightcookie.tga");
		lightSource.enabled = false;
		//flashlight.transform.SetPositionAndRotation(GamePlayerOwner.MyPlayer.CameraPosition.position, GamePlayerOwner.MyPlayer.CameraPosition.rotation);
		flashlight.transform.parent = GamePlayerOwner.MyPlayer.CameraPosition;
		flashlight.transform.localPosition = new(0f, -0.25f, 0.25f);
	}

	private void OnDisable()
	{
		if (isOn)
			Toggle();
	}

	private void OnDestroy()
	{
		if (this == Instance)
			Instance = null;
	}

	private void Update()
	{
		if (Input.GetKeyDown(HEVMod.Instance.flashlightKey.Value.MainKey))
			Toggle();

		if (isOn)
		{
			batteryLevel -= BATT_DRAIN_RATE * Time.deltaTime;
			if (batteryLevel <= 0f)
				Toggle();
		}
		else
		{
			if (batteryLevel < 1f)
				batteryLevel = Mathf.Clamp01(batteryLevel + BATT_CHARGE_RATE * Time.deltaTime);
		}

		BatteryUpdate?.Invoke(batteryLevel);

		bool lowBatteryThisFrame = batteryLevel < BATT_LOW_THRESHOLD;
		if (lowBatteryThisFrame != lowBattery)
		{
			lowBattery = lowBatteryThisFrame;
			BatteryLow?.Invoke(lowBattery);
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
		Toggled?.Invoke(isOn);
	}
}
