using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod;

public class MedicalSystem : MonoBehaviour
{
    private ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("HEVSuitMod.MedicalSystem");
    private IReadOnlyDictionary<string, string> medInjectors = new Dictionary<string, string>
        {
            { "morphine", "544fb3f34bdc2d03748b456a" },
            { "adrenaline", "5c10c8fd86f7743d7d706df3" },
            { "propital", "5c0e530286f7747fa1419862" },
            { "etgchange", "5c0e534186f7747fa1419867" },
            { "antidote", "5fca138c2a7b221b2852a5c6" },
            { "zagustin", "5c0e533786f7747fa23f4d47" }
        };

    private Dictionary<string, float> activeStatusEffects = [];
    private readonly List<string> effectsToRemove = [];

    private void OnEnable()
    {
        GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent += EffectStartedHandler;
    }

    private void OnDisable()
    {
        GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent -= EffectStartedHandler;
    }

    private void Update()
    {
        effectsToRemove.Clear();
        foreach (var effect in activeStatusEffects)
        {
            activeStatusEffects[effect.Key] -= Time.deltaTime;
            if (activeStatusEffects[effect.Key] <= 0f)
            {
                effectsToRemove.Add(effect.Key);
            }
        }
        foreach (var effectName in effectsToRemove)
        {
            activeStatusEffects.Remove(effectName);
            log.LogDebug($"Effect {effectName} expired");
        }
    }

    private void EffectStartedHandler(IEffect effect)
    {
        Type effectType = effect.GetType(); // All effect classes are protected
        string effectName = effectType.Name;
        if (activeStatusEffects.ContainsKey(effectName))
        {
            log.LogDebug($"Duplicate effect {effectName}");
            return;
        }

        switch (effectName)
        {
            case "Fracture":
                switch (effect.BodyPart)
                {
                    case EBodyPart.LeftLeg:
                    case EBodyPart.RightLeg:
                        // Only stim if a leg is fractured, arm doesn't need it
                        UseInjector("morphine");
                        break;
                }
                break;

            case "HeavyBleeding":
            case "LightBleeding":
                UseInjector("zagustin");
                break;

            case "LowEdgeHealth":
                UseInjector("etgchange");
                break;

            case "LethalIntoxication":
            case "Intoxication": // TODO: Need to verify this is the effect for being stabbed by cultist knife
                UseInjector("antidote");
                break;

            default:
                log.LogWarning($"Unhandled health effect {effectName}");
                break;
        }
    }

    /// <summary>
    /// Try to use a medical injector on the player
    /// </summary>
    /// <param name="injectorName">The name of the injector to use defined by <paramref name="medInjectors"/> dictionary</param>
    /// <returns>True if the injector was used, false otherwise</returns>
    public void UseInjector(string injectorName) // Defualt is Morphine for testiing
    {
        Player player = GamePlayerOwner.MyPlayer;
        if (player == null)
        {
            log.LogError("UseInjector() - MyPlayer is null!");
            return;
        }

        ActiveHealthController healthController = player.ActiveHealthController;
        if (healthController == null)
        {
            log.LogError("UseInjector() - ActiveHealthController is null!");
            return;
        }

        if (medInjectors.TryGetValue(injectorName, out var injectorId) == false)
        {
            log.LogError($"Injector {injectorName} is undefined.");
            return;
        }

        ItemFactoryClass itemFactory = Singleton<ItemFactoryClass>.Instance;
        Item stim = itemFactory.GetPresetItem(injectorId);
        GStruct154<GClass3415> addAnywhereResult = player.Inventory.SortingTable.Grid.AddAnywhere(stim, EErrorHandlingType.Log);
        if (addAnywhereResult.Succeeded)
        {
            healthController.DoMedEffect(stim, EBodyPart.Head);
            log.LogInfo($"Used {stim.Template.ShortName} injector.");
        }
    }
}
