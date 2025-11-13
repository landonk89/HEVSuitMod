using EFT;
using HEVSuitMod.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudDamageIndicators : MonoBehaviour
{
    private readonly HudIcon[] indicators = new HudIcon[4]; // Order: Up, Right, Down, Left
    private HudController Hud => HEVMod.Instance.HudController;

    private Dictionary<int, HudIcon[]> Directions => new()
    {
        [0] = [indicators[0]], // Front
        [1] = [indicators[0], indicators[1]], // Front-Right
        [2] = [indicators[1]], // Right
        [3] = [indicators[1], indicators[2]], // Back-Right
        [4] = [indicators[2]], // Back
        [5] = [indicators[2], indicators[3]], // Back-Left
        [6] = [indicators[3]], // Left
        [7] = [indicators[3], indicators[0]] // Front-Left
    };

#pragma warning disable IDE0051
    private void Awake()
    {
        Image[] indicatorImg = transform.Find("HitIndicators").GetComponentsInChildren<Image>();
        indicators[0] = new(indicatorImg[0], transitionTime: HudController.FADE_TIME);
        indicators[1] = new(indicatorImg[1], transitionTime: HudController.FADE_TIME);
        indicators[2] = new(indicatorImg[2], transitionTime: HudController.FADE_TIME);
        indicators[3] = new(indicatorImg[3], transitionTime: HudController.FADE_TIME);
        foreach (Image image in indicatorImg)
            image.color = Color.clear; // Start transparent

        GamePlayerOwner.MyPlayer.BeingHitAction += OnPlayerHit;
    }

    private void OnDestroy()
    {
        GamePlayerOwner.MyPlayer.BeingHitAction -= OnPlayerHit;
    }

    private void Update()
    {
        // Unique State update, don't use IconUpdate
        if (indicators.All(x => x.State == EHudIconState.Inactive))
            return; // Only proceed if any indicators are active

        foreach (HudIcon indicator in indicators)
        {
            if (indicator.State != EHudIconState.Active)
                continue;

            indicator.Timer += Time.deltaTime;
            if (indicator.Timer >= indicator.TransitionTime)
            {
                indicator.Timer = 0f;
                indicator.Image.color = Color.clear;
                indicator.State = EHudIconState.Inactive;
                continue;
            }

            float t = indicator.Timer / indicator.TransitionTime;
            indicator.Image.color = Color.Lerp(Hud.damageIndicatorColor, Color.clear, t);
        }
    }
#pragma warning restore IDE0051

    private void OnPlayerHit(DamageInfoStruct damageInfo, EBodyPart bodyPart, float damage)
    {
        Vector3 lookDir = GamePlayerOwner.MyPlayer.LookDirection.normalized;
        Vector3 localDir = Quaternion.Inverse(Quaternion.LookRotation(lookDir)) * -damageInfo.Direction;
        localDir.y = 0;
        localDir.Normalize();

        // Get angle in degrees (0 = front, 90 = right, 180 = back, 270 = left)
        float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // Decide which hit indicators to show based on angle
        int dirIndex = Mathf.FloorToInt((angle + 22.5f) % 360f / 45f);
        foreach (var indicator in Directions[dirIndex])
        {
            indicator.Timer = 0f; // In case it's already active, reset Timer
            indicator.State = EHudIconState.Active;
        }
    }
}
