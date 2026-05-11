using System;
using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using DraftPlus.Code;

namespace DraftPlus.Code.Modifiers;

/// <summary>Encounter card rewards can roll starter Basics when rarity is Common.</summary>
public class StarterRewards : ModifierModel, ILocalizationProvider
{
    protected override string IconPath => ImageHelper.GetImagePath("packed/modifiers/draft.png");

    public string? LocTable => "modifiers";

    public List<(string, string)>? Localization =>
    [
        ("title", "Starter Rewards"),
        ("description", "Starter cards can appear in normal card rewards.")
    ];

    private static bool Enabled(IRunState runState)
        => runState.Modifiers.Any(m => m is StarterRewards);

    private static IEnumerable<CardModel> GetStarters(Player player)
        => StarterDeckHelpers.GetMergedStarterCards(player);

    [HarmonyPatch(typeof(ModelDb), "get_GoodModifiers")]
    private static class AddToGoodModifiersPatch
    {
        private static void Postfix(ref IReadOnlyList<ModifierModel> __result)
        {
            try
            {
                if (__result.Any(m => m is StarterRewards))
                    return;

                __result = __result.Concat(new ModifierModel[] { ModelDb.Modifier<StarterRewards>() }).ToArray();
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(CardFactory), "CreateForReward", new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardCreationOptions) })]
    private static class IncludeStarterCardsInNormalRewardsPatch
    {
        private static bool Prefix(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options, ref CardModel __result)
        {
            if (!Enabled(player.RunState))
                return true;

            if (options.Source != CardCreationSource.Encounter)
                return true;

            if (options.RarityOdds == CardRarityOddsType.Uniform)
                return true;

            options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

            IEnumerable<CardModel> possible = options
                .GetPossibleCards(player)
                .Concat(GetStarters(player))
                .Except(blacklist)
                .ToList();

            possible = FilterForPlayerCount(player.RunState, possible).ToArray();

            // Basic starters only surface when rolled rarity is Common.
            HashSet<CardRarity> allowedRarities = possible.Select(c => c.Rarity).ToHashSet();
            CardRarity selectedRarity = RollForRarity(player, options.RarityOdds, options.Source, allowedRarities,
                options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange));

            if (selectedRarity == CardRarity.None)
                return true;

            IEnumerable<CardModel> items = possible.Where(c => c.Rarity == selectedRarity);
            if (selectedRarity == CardRarity.Common)
            {
                items = items.Concat(possible.Where(c => c.Rarity == CardRarity.Basic));
            }

            Rng rng = options.RngOverride ?? player.PlayerRng.Rewards;
            CardModel? canonical = rng.NextItem(items);
            if (canonical == null)
                return true;

            __result = player.RunState.CreateCard(canonical, player);
            return false;
        }

        private static IEnumerable<CardModel> FilterForPlayerCount(IRunState runState, IEnumerable<CardModel> options)
        {
            if (runState.Players.Count > 1)
                return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly);
            return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
        }

        private static CardRarity RollForRarity(Player player, CardRarityOddsType rollMethod, CardCreationSource source,
            HashSet<CardRarity> allowedRarities, bool forceRarityOddsChange)
        {
            bool useRollThatChangesFutureOdds =
                forceRarityOddsChange ||
                (source == CardCreationSource.Encounter &&
                 (rollMethod == CardRarityOddsType.RegularEncounter ||
                  rollMethod == CardRarityOddsType.EliteEncounter ||
                  rollMethod == CardRarityOddsType.BossEncounter));

            CardRarity rolled = useRollThatChangesFutureOdds
                ? player.PlayerOdds.CardRarity.Roll(rollMethod)
                : player.PlayerOdds.CardRarity.RollWithBaseOdds(rollMethod);

            while (!allowedRarities.Contains(rolled) && rolled != CardRarity.None)
            {
                rolled = rolled.GetNextHighestRarity();
            }
            return rolled;
        }
    }
}

