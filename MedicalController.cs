using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HEVSuitMod;

public class MedicalController : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(MedicalController).FullName}");
	private HashSet<IEffect> activeEffects = [];

	// TODO: In an ideal world, I would search the template id from the server
	// but I don't know shit about how that works yet... Revisit this later
	private readonly Dictionary<string, string> medInjectors = new()
	{
			{ "morphine", "544fb3f34bdc2d03748b456a" },
			{ "adrenaline", "5c10c8fd86f7743d7d706df3" },
			{ "propital", "5c0e530286f7747fa1419862" },
			{ "etgchange", "5c0e534186f7747fa1419867" },
			{ "antidote", "5fca138c2a7b221b2852a5c6" },
			{ "zagustin", "5c0e533786f7747fa23f4d47" }
	};


    private void OnEnable()
	{
		GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent += EffectStarted;
		GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent += EffectRemoved;
	}

	private void OnDisable()
	{
		GamePlayerOwner.MyPlayer.HealthController.EffectStartedEvent -= EffectStarted;
		GamePlayerOwner.MyPlayer.HealthController.EffectRemovedEvent -= EffectRemoved;
	}

	/// <summary>
	/// Try to use a medical injector on the player
	/// </summary>
	/// <param name="injectorName">The name of the injector to use defined by <paramref name="medInjectors"/> dictionary</param>
	/// <returns>True if the injector was used, false otherwise</returns>
	// TODO: Make private when testing is complete
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
			log.LogError($"UseInjector() - '{injectorName}' is undefined.");
			return;
		}

		// We need to create a stim item and add it to some stashgrid before we can call DoMedEffect
		ItemFactoryClass itemFactory = Singleton<ItemFactoryClass>.Instance;
		Item stim = itemFactory.GetPresetItem(injectorId);
		GStruct154<GClass3415> addAnywhereResult = player.Inventory.SortingTable.Grid.AddAnywhere(stim, EErrorHandlingType.Log);
		if (addAnywhereResult.Succeeded)
		{
			healthController.DoMedEffect(stim, EBodyPart.Head);
			log.LogInfo($"Used {stim.Template.ShortName.Localized()} injector.");
		}
	}

	public void EffectStarted(IEffect effect)
	{
		Type effectType = effect.GetType(); // All effect classes are protected
		string effectName = effectType.Name;
		if (activeEffects.Contains(effect))
		{
			log.LogDebug($"Duplicate effect {effectName}");
			return;
		}

		bool handled = false;
		switch (effectName)
		{
			case "Fracture":    // Only stim if a leg is fractured, arm doesn't need it
				if (effect.BodyPart == EBodyPart.LeftLeg || effect.BodyPart == EBodyPart.RightLeg)
					UseInjector("morphine");
				handled = true;
				break;

			case "HeavyBleeding":
			case "LightBleeding":
				UseInjector("zagustin");
				handled = true;
				break;

			// TODO: Make this configurable? The user may or may not want this level of assistance
			case "LowEdgeHealth":
				UseInjector("etgchange");
				handled = true;
				break;

			// TODO: Need to verify this is the effect for being stabbed by cultist knife
			case "LethalIntoxication":
			case "Intoxication":
				UseInjector("antidote");
				handled = true;
				break;

			default:
				log.LogDebug($"Unhandled health effect {effectName}");
				break;
		}

		if (handled)
		{
			activeEffects.Add(effect);
			log.LogDebug($"Effect {effectName} added");
		}
	}

	public void EffectRemoved(IEffect effect)
    {
		log.LogDebug($"Effect {effect.GetType().Name} removed.");
		activeEffects.Remove(effect);
    }
}
