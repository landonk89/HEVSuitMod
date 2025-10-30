using System;
using EFT;
using UnityEngine;

namespace HEVSuitMod;

public class Flashlight : MonoBehaviour
{
	private enum EState
	{
		On,
		TurningOn,
		TurningOff,
		OffCharged,
		OffCharging,
	}

	private const float BATT_LOW_THRESHOLD = 0.25f;
	private const float BATT_DRAIN_RATE = 0.01f;
	private const float BATT_CHARGE_RATE = 0.05f;
	private const float TRANSITION_TIME = 0.1f; // Thermal inertia time (on->off)

	private AssetBundle assets;
	private HudController hud;
	private Light lightSource;
	private AudioSource audioSource; // TODO: use BetterAudio
	private bool batteryCritical = false;
	private float batteryLevel = 1f; // 0..1
	private EState state = EState.OffCharged;
	GameObject flashlight = new("FlashlightContainer");

	public event Action<bool> Toggled;
	public event Action<float> BatteryUpdate;
	public event Action<bool> BatteryStateChanged;

	private void Awake()
	{
		assets = HEVMod.Instance.Assets;
		hud = HEVMod.Instance.HudController;
		audioSource = flashlight.AddComponent<AudioSource>();
		audioSource.clip = assets.LoadAsset<AudioClip>("assets/sounds/flashlight.wav");
		lightSource = flashlight.AddComponent<Light>();
		lightSource.type = LightType.Spot;
		lightSource.spotAngle = 40f;
		lightSource.range = 50f;
		lightSource.cookie = assets.LoadAsset<Texture2D>("assets/sprites/hl2flashlightcookie.tga");
		lightSource.enabled = false;
		flashlight.transform.parent = GamePlayerOwner.MyPlayer.CameraPosition; // Attasch to our face and reposition
		flashlight.transform.localPosition = new(0f, -0.25f, 0.25f);
		flashlight.transform.localRotation = Quaternion.identity;
	}

	private void OnDestroy()
	{
		Destroy(flashlight);
	}

	private void OnDisable()
	{
		lightSource.enabled = false;
		lightSource.intensity = 0f;
		batteryLevel = 1f;
		state = EState.OffCharged;
	}

	private void Update()
	{
		if (Input.GetKeyDown(HEVMod.Instance.flashlightKey.Value.MainKey))
			Toggle();

		switch (state)
		{
			case EState.OffCharged:
				return; // Nothing to do

			case EState.OffCharging:
				if (batteryLevel < 1f)
					batteryLevel = Mathf.Clamp01(batteryLevel + BATT_CHARGE_RATE * Time.deltaTime);
				
				BatteryUpdate?.Invoke(batteryLevel);
				if (batteryLevel > 0.99f)
					state = EState.OffCharged;
				break;

			case EState.TurningOff:
				lightSource.intensity -= Time.deltaTime / TRANSITION_TIME;
				if (lightSource.intensity <= 0f)
				{
					lightSource.enabled = false;
					state = EState.OffCharging;
				}
				break;

			case EState.TurningOn:
				lightSource.intensity += Time.deltaTime / TRANSITION_TIME;
				if (lightSource.intensity >= 1f)
					state = EState.On;
				break;

			case EState.On:
				batteryLevel -= BATT_DRAIN_RATE * Time.deltaTime;
				if (batteryLevel <= 0f)
					Toggle();
				break;
		}

		BatteryUpdate?.Invoke(batteryLevel); // For HUD
		bool lowBatteryThisFrame = batteryLevel < BATT_LOW_THRESHOLD;
		if (lowBatteryThisFrame != batteryCritical)
		{
			batteryCritical = lowBatteryThisFrame;
			BatteryStateChanged?.Invoke(batteryCritical);
		}
	}

	public void Toggle()
	{
		switch (state)
		{
			case EState.On:
			case EState.TurningOn:
				state = EState.TurningOff;
				Toggled?.Invoke(false);
				break;

			case EState.OffCharged:
			case EState.OffCharging:
			case EState.TurningOff:
				lightSource.enabled = true;
				state = EState.TurningOn;
				Toggled?.Invoke(true);
				break;
		}

		audioSource.Play();
	}
}
