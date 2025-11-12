using EFT;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod;

public class HudHealthCounter : MonoBehaviour
{
    private readonly HudIcon[] healthIcons = new HudIcon[4]; // 3 digits[0,1,2] + icon[3]
    private Action<EBodyPart, float, DamageInfoStruct> HealthChangedAction;
    private GDelegate70 PlayerDeadAction;
    
    private HudIcon[] HealthNumbers => [healthIcons[0], healthIcons[1], healthIcons[2]];
    private HudController Hud => HEVMod.Instance.HudController;

#pragma warning disable IDE0051
	private void Awake()
    {
        HealthChangedAction = (_, _, _) => HealthChanged();
        PlayerDeadAction = (_, _, _, _) => HealthChanged(false);
        healthIcons[3] = new(transform.Find("HealthAndSuitPower/HealthIcon").GetComponent<Image>());
        for (int i = 0; i < 3; i++)
            healthIcons[i] = new(transform.Find($"HealthAndSuitPower/HealthValue/Digit{i}").GetComponent<Image>());

        GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent += HealthChangedAction;
        GamePlayerOwner.MyPlayer.OnPlayerDead += PlayerDeadAction;
    }

    private void Start()
    {
        HealthChanged();
    }

    private void OnDestroy()
    {
        GamePlayerOwner.MyPlayer.ActiveHealthController.HealthChangedEvent -= HealthChangedAction;
        GamePlayerOwner.MyPlayer.OnPlayerDead -= PlayerDeadAction;
    }

    private void Update()
    {
        Hud.IconUpdate(healthIcons);
        if (Time.time % HudController.FLASH_TIME < Time.deltaTime)
        {
            if (healthIcons[0].critical)
                Hud.IconFlash(healthIcons);
        }
    }
#pragma warning restore IDE0051

	private void HealthChanged(bool alive = true)
    {
        // FIXME/TODO: Assumes normal 440 max health player, may break if health is modded higher
        int normalizedHealth = 0;
        if (alive)
        {
            float health = GamePlayerOwner.MyPlayer.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common).Current;
            normalizedHealth = Mathf.CeilToInt(health / 440f * 100f);
        }
        Hud.IconSetDigits(HealthNumbers, normalizedHealth);
        Hud.IconSetCritical(healthIcons, normalizedHealth <= HudController.HEALTH_CRITICAL);
        Hud.IconFlash(healthIcons);
    }
}
