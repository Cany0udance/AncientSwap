using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace AncientSwap.Patches;

// Sets the starter relic preview in the Ancient swap option to be grayscale
[HarmonyPatch(typeof(NEventOptionButton), "_Ready")]
public class AncientSwapRelicIconPatch
{
    public static void Postfix(NEventOptionButton __instance)
    {
        if (__instance.Option.TextKey != "STARTER_REMOVAL")
            return;

        if (__instance.Event is not AncientEventModel || __instance.Option.Relic == null)
            return;

        TextureRect relicIcon = __instance.GetNode<TextureRect>((NodePath) "%RelicIcon");
        ShaderMaterial mat = (ShaderMaterial) PreloadManager.Cache.GetMaterial("res://materials/ui/relic_mat.tres").Duplicate(true);
        relicIcon.Material = (Material) mat;
        __instance.Option.Relic.Status = RelicStatus.Disabled;
        __instance.Option.Relic.UpdateTexture(relicIcon);
        relicIcon.Modulate = new Color("#808080");
    }
}