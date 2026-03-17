using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AncientSwap.Patches;

[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
public class AncientSwapOptionPatch
{
    private const string LOG_TAG = "[AncientSwap]";
    
    // Set to a relic type to force that relic, or null for random behavior
    private static readonly Type DebugForceRelic = null;
    // Example: private static readonly Type DebugForceRelic = typeof(SeaGlass);
    
    private static readonly HashSet<Type> ExcludedRelics = new HashSet<Type>
    {
        typeof(TouchOfOrobas),
        typeof(PaelsGrowth)
    };
    
    public static void Postfix(AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__instance is not Neow neow)
            return;
            
        if (neow.Owner.RunState.Modifiers.Count > 0)
            return;

        List<EventOption> options = __result.ToList();

        RelicModel starterRelic = neow.Owner.Relics.FirstOrDefault(r => r.Rarity == RelicRarity.Starter);
        string starterRelicName = starterRelic?.Title.GetFormattedText() ?? "Starter Relic";

        LocString title = new LocString("ancients", "NEOW.pages.INITIAL.options.STARTER_REMOVAL.title");
        LocString description = new LocString("ancients", "NEOW.pages.INITIAL.options.STARTER_REMOVAL.description");
        description.Add("StarterRelicName", starterRelicName);

        EventOption starterRemovalOption = new EventOption(
            neow,
            () => RemoveStarterRelic(neow),
            title,
            description,
            "STARTER_REMOVAL",
            Enumerable.Empty<IHoverTip>()
        );

        if (starterRelic != null)
        {
            try
            {
                RelicModel displayRelic = ModelDb.GetById<RelicModel>(starterRelic.Id).ToMutable();
                displayRelic.Owner = neow.Owner;
                starterRemovalOption.WithRelic(displayRelic);
            }
            catch (Exception e)
            {
                Log.Error("[AncientSwap] Failed to set display relic: " + e);
            }
        }

        options.Add(starterRemovalOption);
        __result = options;
    }
    
    private static async Task RemoveStarterRelic(Neow neow)
    {
        RelicModel starterRelic = neow.Owner.Relics.FirstOrDefault(r => r.Rarity == RelicRarity.Starter);
       
        if (starterRelic != null)
        {
            await RelicCmd.Remove(starterRelic);
        }
       
        RelicModel ancientRelic = null;
        
        // Debug: force a specific relic
        if (DebugForceRelic != null)
        {
            RelicModel forcedRelic = ModelDb.AllAncients
                .SelectMany(ancient => ancient.AllPossibleOptions)
                .Select(option => option.Relic?.CanonicalInstance)
                .OfType<RelicModel>()
                .FirstOrDefault(r => r.GetType() == DebugForceRelic);
            
            if (forcedRelic != null)
            {
                ancientRelic = SetupRelic(forcedRelic.ToMutable(), neow);
                if (ancientRelic == null)
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }
        else
        {
            ancientRelic = SelectRandomAncientRelic(neow);
        }
        
        if (ancientRelic == null)
            return;
       
        await RelicCmd.Obtain(ancientRelic, neow.Owner);
       
        PropertyInfo customDonePageProp = typeof(AncientEventModel).GetProperty("CustomDonePage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        customDonePageProp.SetValue(neow, "NEOW.pages.DONE.POSITIVE.description");
        
        MethodInfo doneMethod = typeof(AncientEventModel).GetMethod("Done",
            BindingFlags.NonPublic | BindingFlags.Instance);
        doneMethod.Invoke(neow, null);
    }
    
    private static RelicModel SelectRandomAncientRelic(Neow neow)
    {
        List<RelicModel> allAncientRelics = ModelDb.AllAncients
            .Where(ancient => ancient is not Neow)
            .SelectMany(ancient => ancient.AllPossibleOptions)
            .Select(option => option.Relic?.CanonicalInstance)
            .OfType<RelicModel>()
            .Where(relic => relic.Rarity == RelicRarity.Ancient)
            .Where(relic => !ExcludedRelics.Contains(relic.GetType()))
            .Distinct()
            .ToList();
   
        if (allAncientRelics.Count == 0)
        {
            return null;
        }
   
        int attempts = 0;
        const int maxAttempts = 100;
   
        while (attempts < maxAttempts)
        {
            RelicModel selectedRelic = neow.Rng.NextItem(allAncientRelics);
            RelicModel result = SetupRelic(selectedRelic.ToMutable(), neow);
            if (result != null)
                return result;
            attempts++;
        }
        
        return null;
    }
    
    private static RelicModel SetupRelic(RelicModel mutableRelic, Neow neow)
    {
        if (mutableRelic is SeaGlass seaGlass)
        {
            CharacterModel targetChar = neow.Rng.NextItem(
                neow.Owner.UnlockState.Characters.Where(c => c.Id != neow.Owner.Character.Id)
            ) ?? neow.Owner.Character;
            seaGlass.CharacterId = targetChar.Id;
            return mutableRelic;
        }
    
        if (mutableRelic is ArchaicTooth archaicTooth)
        {
            return archaicTooth.SetupForPlayer(neow.Owner) ? mutableRelic : null;
        }
    
        if (mutableRelic is DustyTome dustyTome)
        {
            dustyTome.SetupForPlayer(neow.Owner);
            return mutableRelic;
        }
    
        return mutableRelic;
    }
}