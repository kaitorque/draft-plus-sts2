using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using DraftPlus.Code;

namespace DraftPlus.Code.Modifiers;

/// <summary>Draft-style modifier with starters merged into the Neow draft pool.</summary>
public class DraftPlus : ModifierModel, ILocalizationProvider
{
    public override bool ClearsPlayerDeck => true;

    protected override string IconPath => ImageHelper.GetImagePath("packed/modifiers/draft.png");

    public string? LocTable => "modifiers";

    public List<(string, string)>? Localization =>
    [
        ("title", "Draft+"),
        ("description", "Draft your starting deck as normal, but starter cards can also appear in draft rewards.")
    ];

    public override Func<Task> GenerateNeowOption(EventModel eventModel)
    {
        return () => OfferRewards(eventModel.Owner!);
    }

    private static async Task OfferRewards(Player player)
    {
        IEnumerable<CardModel> pool = BuildDraftPoolIncludingStarters(player);

        CardCreationOptions creationOptions = new CardCreationOptions(pool, CardCreationSource.Other, CardRarityOddsType.Uniform)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

        for (int i = 0; i < 10; i++)
        {
            CardReward cardReward = new CardReward(creationOptions, 3, player)
            {
                CanSkip = false
            };
            cardReward.Populate();
            await cardReward.SelectUnsynchronized();
        }

        bool anyPlayerDraftedStrikeOrDefend = player.RunState.Players
            .Any(p => p.Deck.Cards.Any(c => IsStrikeOrDefend(c.Id.Entry)));

        // Pandora off grab bags unless any Strike/Defend slug found post-draft.
        if (!anyPlayerDraftedStrikeOrDefend)
        {
            foreach (Player p in player.RunState.Players)
            {
                p.RelicGrabBag.Remove<MegaCrit.Sts2.Core.Models.Relics.PandorasBox>();
            }
            player.RunState.SharedRelicGrabBag.Remove<MegaCrit.Sts2.Core.Models.Relics.PandorasBox>();
        }
    }

    private static bool IsStrikeOrDefend(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        return entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase) ||
               entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CardModel> BuildDraftPoolIncludingStarters(Player player)
    {
        IEnumerable<CardModel> fromPool = player.Character.CardPool.GetUnlockedCards(
            player.UnlockState,
            player.RunState.CardMultiplayerConstraint
        );

        IEnumerable<CardModel> starters = StarterDeckHelpers.GetMergedStarterCards(player);

        return fromPool
            .Concat(starters)
            .GroupBy(c => c.Id)
            .Select(g => g.First());
    }

    private static bool HasDraftPlus(IRunState runState)
    {
        return runState.Modifiers.Any(m => m is DraftPlus);
    }

    [HarmonyPatch(typeof(ModelDb), "get_GoodModifiers")]
    private static class AddToGoodModifiersPatch
    {
        private static void Postfix(ref IReadOnlyList<ModifierModel> __result)
        {
            try
            {
                if (__result.Any(m => m is DraftPlus))
                    return;

                __result = __result.Concat(new ModifierModel[] { ModelDb.Modifier<DraftPlus>() }).ToArray();
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(ModelDb), "get_MutuallyExclusiveModifiers")]
    private static class MakeMutuallyExclusiveWithDraftPatch
    {
        private static void Postfix(ref IReadOnlyList<IReadOnlySet<ModifierModel>> __result)
        {
            try
            {
                ModifierModel draft = ModelDb.Modifier<Draft>();
                ModifierModel draftPlus = ModelDb.Modifier<DraftPlus>();

                bool alreadyPresent = __result.Any(set => set.Any(m => m.GetType() == typeof(DraftPlus)));
                if (alreadyPresent)
                    return;

                HashSet<ModifierModel> newSet = new HashSet<ModifierModel> { draft, draftPlus };
                __result = __result.Concat(new IReadOnlySet<ModifierModel>[] { newSet }).ToArray();
            }
            catch
            {
            }
        }
    }

    [HarmonyPatch(typeof(CardFactory), "CreateForReward", new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardCreationOptions) })]
    private static class IncludeBasicCardsInUniformRewardPoolPatch
    {
        private static bool Prefix(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options, ref CardModel __result)
        {
            if (options.RarityOdds != CardRarityOddsType.Uniform)
                return true;

            if (!HasDraftPlus(player.RunState))
                return true;

            options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

            IEnumerable<CardModel> possible = options.GetPossibleCards(player).Except(blacklist).ToList();
            possible = FilterForPlayerCount(player.RunState, possible).ToArray();

            IEnumerable<CardModel> items = possible.Where(c => c.Rarity != CardRarity.Ancient);

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
            {
                return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly);
            }
            return options.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
        }
    }
}

