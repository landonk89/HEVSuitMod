using BepInEx.Logging;
using EFT;
using HEVSuitMod.Types;
using UnityEngine;
using UnityEngine.UI;

namespace HEVSuitMod.Components;

public class HudCrosshair : MonoBehaviour
{
    private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(HudCrosshair).FullName}");
    private Player.FirearmController currentFirearm;
    private HudIcon crosshair;
    private HudIcon crosshairOnTarget;

#pragma warning disable IDE0051
    private void Awake()
    {
        crosshair = new(transform.Find("Crosshair").GetComponent<Image>());
        crosshair.Image.enabled = false;
        crosshairOnTarget = new(transform.Find("CrosshairOnTarget").GetComponent<Image>());
        crosshairOnTarget.Image.enabled = false;

        if (GamePlayerOwner.MyPlayer.HandsController is not Player.FirearmController faController)
            log.LogWarning("Start() HandsController is not Player.FirearmController.");
        else
            currentFirearm = faController;

        GamePlayerOwner.MyPlayer.HandsChangedEvent += HandsChanged;
    }

    private void OnDestroy()
    {
        GamePlayerOwner.MyPlayer.HandsChangedEvent -= HandsChanged;
    }

    private void Update()
    {
        if (currentFirearm == null)
            return;

        // TODO: Get muzzle direction, RayCast to hit point, convert to screen space, reposition crosshair.
        // Also enable crosshairOnTarget if hit point is a living thing
    }
#pragma warning restore IDE0051

    private void HandsChanged(IHandsController controller)
    {
        if (controller is Player.FirearmController faController)
        {
            currentFirearm = faController;
            crosshair.Image.enabled = true;
        }
        else
        {
            currentFirearm = null;
            crosshair.Image.enabled = false;
            crosshairOnTarget.Image.enabled = false;
        }
    }
}
