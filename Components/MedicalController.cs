using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HEVSuitMod.Components;

public class MedicalController : MonoBehaviour
{
	private readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(MedicalController).FullName}");
	private readonly HashSet<IEffect> activeEffects = [];

	private ActiveHealthController HealthController => GamePlayerOwner.MyPlayer.ActiveHealthController;

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

#pragma warning disable IDE0051
	private void OnEnable()
	{
		HealthController.EffectStartedEvent += EffectStarted;
		HealthController.EffectRemovedEvent += EffectRemoved;
		HealthController.BodyPartDestroyedEvent += BodyPartDestroyed;
	}

	private void OnDisable()
	{
		HealthController.EffectStartedEvent -= EffectStarted;
		HealthController.EffectRemovedEvent -= EffectRemoved;
		HealthController.BodyPartDestroyedEvent -= BodyPartDestroyed;
	}
#pragma warning restore IDE0051

	/// <summary>
	/// Try to use a medical injector on the player
	/// </summary>
	/// <param name="injectorName">The name of the injector to use defined by <paramref name="medInjectors"/> dictionary</param>
	// TODO: Make private when testing is complete
	public void UseInjector(string injectorName)
	{
		if (medInjectors.TryGetValue(injectorName, out var injectorId) == false)
		{
			log.LogError($"UseInjector() - '{injectorName}' is undefined.");
			return;
		}

		// We need to create a stim item and add it to some stashgrid before we can call DoMedEffect
		ItemFactoryClass itemFactory = Singleton<ItemFactoryClass>.Instance;
		Item stim = itemFactory.GetPresetItem(injectorId);
		GStruct154<GClass3415> addAnywhereResult = GamePlayerOwner.MyPlayer.Inventory.SortingTable.Grid.AddAnywhere(stim, EErrorHandlingType.Log);
		if (addAnywhereResult.Succeeded)
		{
			HealthController.DoMedEffect(stim, EBodyPart.Head);
			log.LogDebug($"Used {injectorName} injector.");
		}
		else
			log.LogDebug($"Couldn't get stim: {injectorName}");
	}

	private void BodyPartDestroyed(EBodyPart part, EDamageType damageType)
	{
		// There's a chance that a leg can be destroyed but no fracture, so give propital if that happens.
		if (activeEffects.Any(x => x.GetType().Name == "PainKiller"))
			return; // Don't double up

		if (part == EBodyPart.LeftLeg || part == EBodyPart.RightLeg)
		{
			log.LogDebug("Leg destroyed, use propital");
			UseInjector("propital");
		}
	}

	private void EffectStarted(IEffect effect)
	{
		if (activeEffects.Contains(effect))
		{
			log.LogDebug($"Duplicate effect {effect.GetType().Name}");
			return;
		}

		// Use GetType().Name because IEffect.Type returns a GInterface name insead of the effect class name
		bool handled = false;
		switch (effect.GetType().Name)
		{
			case "Fracture":
				// Make sure we're not already blitzed
				if (activeEffects.Any(x => x.GetType().Name == "PainKiller"))
					break;

				// Only stim if a leg is fractured, arm doesn't need it
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

			case "PainKiller":
				// Just add it so we can check for it later
				handled = true;
				break;

			default:
				log.LogDebug($"Unhandled health effect {effect.GetType().Name}");
				break;
		}

		if (handled)
		{
			activeEffects.Add(effect);
			log.LogDebug($"Effect {effect.GetType().Name} added");
		}
	}

	public void EffectRemoved(IEffect effect)
	{
		if (activeEffects.Remove(effect))
			log.LogDebug($"Effect {effect.GetType().Name} removed.");
	}
}
