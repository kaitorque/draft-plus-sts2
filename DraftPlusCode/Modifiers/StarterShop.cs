using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

using DraftPlus.Code;

namespace DraftPlus.Code.Modifiers;

/// <summary>Shop card slots: Basic starters eligible when roll is Common (per CardType).</summary>
public class StarterShop : ModifierModel, ILocalizationProvider
{
    protected override string IconPath => ImageHelper.GetImagePath("packed/modifiers/draft.png");

    public string? LocTable => "modifiers";

    public List<(string, string)>? Localization =>
    [
        ("title", "Starter Shops"),
        ("description", "Starter cards can appear in merchant card offers when the shop rolls a Common card for that slot.")
    ];

    internal static bool Enabled(IRunState runState)
        => runState.Modifiers.Any(m => m is StarterShop);

    private static decimal MerchantUpgradeOddScaling
        => AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);

    private static IEnumerable<CardModel> FilterForPlayerCount(IRunState runState, IEnumerable<CardModel> options)
    {
        if (runState.Players.Count > 1)
            return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly);
        return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
    }

    private static void MerchantRollForUpgrade(Player player, CardModel card, decimal baseChance, Rng rng)
    {
        decimal roll = (decimal)rng.NextFloat();
        if (!card.IsUpgradable)
            return;

        decimal odds = baseChance;
        if (card.Rarity != CardRarity.Rare)
        {
            int act = player.RunState.CurrentActIndex;
            odds += (decimal)act * MerchantUpgradeOddScaling;
        }

        odds = Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, odds);
        if (roll <= odds)
            CardCmd.Upgrade(card);
    }

    private static CardCreationResult CreateMerchantCardWithStarters(Player player, IEnumerable<CardModel> options, CardType type)
    {
        if (player.Character is Deprived)
        {
            throw new InvalidOperationException(
                "Merchant inventory can't be generated for the test character. Update your test to use Ironclad.");
        }

        HashSet<ModelId> starterIds = StarterDeckHelpers.GetMergedStarterIds(player);

        options = Hook.ModifyMerchantCardPool(player.RunState, player, options);
        options = options.Where(c => c.Rarity != CardRarity.Basic || starterIds.Contains(c.Id));
        options = FilterForPlayerCount(player.RunState, options);
        CardModel[] source = options.ToArray();

        CardRarity rolledRarity = Hook.ModifyMerchantCardRarity(
            player.RunState,
            player,
            player.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop));

        List<CardModel> list = BuildMerchantOfferList(source, rolledRarity, type, starterIds);

        while (list.Count == 0)
        {
            rolledRarity = rolledRarity.GetNextHighestRarity();
            if (rolledRarity == CardRarity.None)
            {
                throw new InvalidOperationException(
                    "Can't generate a valid rarity for the merchant card options passed.");
            }

            list = BuildMerchantOfferList(source, rolledRarity, type, starterIds);
        }

        CardModel picked = player.PlayerRng.Shops.NextItem(list)
            ?? throw new InvalidOperationException("Merchant card picker returned null.");
        CardModel cardModel = player.RunState.CreateCard(picked, player);
        MerchantRollForUpgrade(player, cardModel, -999999999m, player.PlayerRng.Rewards);
        return new CardCreationResult(cardModel);
    }

    private static List<CardModel> BuildMerchantOfferList(
        CardModel[] source,
        CardRarity rolledRarity,
        CardType type,
        HashSet<ModelId> starterIds)
    {
        List<CardModel> list = source.Where(c => c.Rarity == rolledRarity && c.Type == type).ToList();

        if (rolledRarity == CardRarity.Common)
        {
            IEnumerable<CardModel> basics = source.Where(c =>
                c.Rarity == CardRarity.Basic && c.Type == type && starterIds.Contains(c.Id));
            list = list.Concat(basics).GroupBy(c => c.Id).Select(g => g.First()).ToList();
        }

        return list;
    }

    private static CardCreationResult CreateMerchantCardWithStarters(Player player, IEnumerable<CardModel> options, CardRarity rarity)
    {
        HashSet<ModelId> starterIds = StarterDeckHelpers.GetMergedStarterIds(player);

        options = Hook.ModifyMerchantCardPool(player.RunState, player, options);
        options = options.Where(c => c.Rarity != CardRarity.Basic || starterIds.Contains(c.Id));
        options = FilterForPlayerCount(player.RunState, options);
        CardModel[] source = options.ToArray();

        CardRarity modifiedRarity = Hook.ModifyMerchantCardRarity(player.RunState, player, rarity);

        IEnumerable<CardModel> items = source.Where(c => c.Rarity == modifiedRarity);
        if (modifiedRarity == CardRarity.Common)
        {
            items = items.Concat(source.Where(c =>
                c.Rarity == CardRarity.Basic && starterIds.Contains(c.Id)));
        }

        items = items.GroupBy(c => c.Id).Select(g => g.First());

        CardModel picked = player.PlayerRng.Shops.NextItem(items)
            ?? throw new InvalidOperationException("Merchant card picker returned null.");
        CardModel cardModel = player.RunState.CreateCard(picked, player);
        MerchantRollForUpgrade(player, cardModel, -999999999m, player.PlayerRng.Rewards);
        return new CardCreationResult(cardModel);
    }

    [HarmonyPatch(typeof(ModelDb), "get_GoodModifiers")]
    private static class AddToGoodModifiersPatch
    {
        private static void Postfix(ref IReadOnlyList<ModifierModel> __result)
        {
            try
            {
                if (__result.Any(m => m is StarterShop))
                    return;

                __result = __result.Concat(new ModifierModel[] { ModelDb.Modifier<StarterShop>() }).ToArray();
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType))]
    private static class MerchantByTypePatch
    {
        private static bool Prefix(Player player, IEnumerable<CardModel> options, CardType type, ref CardCreationResult __result)
        {
            if (!Enabled(player.RunState))
                return true;

            __result = CreateMerchantCardWithStarters(player, options, type);
            return false;
        }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardRarity))]
    private static class MerchantByRarityPatch
    {
        private static bool Prefix(Player player, IEnumerable<CardModel> options, CardRarity rarity, ref CardCreationResult __result)
        {
            if (!Enabled(player.RunState))
                return true;

            __result = CreateMerchantCardWithStarters(player, options, rarity);
            return false;
        }
    }
}
