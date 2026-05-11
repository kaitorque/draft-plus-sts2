using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace DraftPlus.Code.Modifiers;

/// <summary>With Starter+, Starter Rewards, or Starter Shops: starter cards exclude Strike/Defend.</summary>
public class MinusStrikeDefendStarters : ModifierModel, ILocalizationProvider
{
    protected override string IconPath => ImageHelper.GetImagePath("packed/modifiers/draft.png");

    public string? LocTable => "modifiers";

    public List<(string, string)>? Localization =>
    [
        ("title", "Minus Strike & Defend"),
        ("description", "With Starter+, Starter Rewards, or Starter Shops: starter cards exclude Strike/Defend.")
    ];

    [HarmonyPatch(typeof(ModelDb), "get_GoodModifiers")]
    private static class AddToGoodModifiersPatch
    {
        private static void Postfix(ref IReadOnlyList<ModifierModel> __result)
        {
            try
            {
                ModifierModel minus = ModelDb.Modifier<MinusStrikeDefendStarters>();

                // Ensure it appears right after StarterShop (end of the Starter* block).
                List<ModifierModel> list = __result.ToList();
                list.RemoveAll(m => m.GetType() == typeof(MinusStrikeDefendStarters));

                int starterShopIndex = list.FindIndex(m => m.GetType() == typeof(StarterShop));
                if (starterShopIndex >= 0)
                {
                    list.Insert(starterShopIndex + 1, minus);
                }
                else
                {
                    list.Add(minus);
                }

                __result = list;
            }
            catch
            {
            }
        }
    }
}
