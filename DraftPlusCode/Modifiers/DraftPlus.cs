using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using DraftPlus.Code;

namespace DraftPlus.Code.Modifiers;

/// <summary>Optional Neow add-on for Draft, Sealed Deck, or Insanity: starter cards can appear in that Neow.</summary>
public class DraftPlus : ModifierModel, ILocalizationProvider
{
    public override bool ClearsPlayerDeck => false;

    protected override string IconPath => ImageHelper.GetImagePath("packed/modifiers/draft.png");

    public string? LocTable => "modifiers";

    public List<(string, string)>? Localization =>
    [
        ("title", "Starter+"),
        ("description", "Choose with Draft, Sealed Deck, or Insanity. Starter cards can appear in that Neow.")
    ];

    internal static bool HasStarterPlus(IRunState runState)
        => runState.Modifiers.Any(m => m is DraftPlus);

    private static bool IsStrikeOrDefend(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        return entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase) ||
               entry.Contains("DEFEND", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CardModel> BuildNeowPoolWithStarters(Player player)
    {
        IEnumerable<CardModel> fromPool = player.Character.CardPool.GetUnlockedCards(
            player.UnlockState,
            player.RunState.CardMultiplayerConstraint
        );

        IEnumerable<CardModel> starters = StarterDeckHelpers.GetMergedStarterCards(player);

        IEnumerable<CardModel> baseAndStarters = fromPool
            .Concat(starters)
            .GroupBy(c => c.Id)
            .Select(g => g.First());

        return StarterDeckHelpers.FilterStrikeDefendIfEnabled(player.RunState, baseAndStarters)
            .GroupBy(c => c.Id)
            .Select(g => g.First());
    }

    private static void ApplyPandorasBoxPolicy(Player player)
    {
        bool anyPlayerHasStrikeOrDefend = player.RunState.Players
            .Any(p => p.Deck.Cards.Any(c => IsStrikeOrDefend(c.Id.Entry)));

        if (anyPlayerHasStrikeOrDefend)
            return;

        foreach (Player p in player.RunState.Players)
        {
            p.RelicGrabBag.Remove<MegaCrit.Sts2.Core.Models.Relics.PandorasBox>();
        }

        player.RunState.SharedRelicGrabBag.Remove<MegaCrit.Sts2.Core.Models.Relics.PandorasBox>();
    }

    private static async Task OfferDraftNeowWithStarters(Player player)
    {
        IEnumerable<CardModel> pool = BuildNeowPoolWithStarters(player);

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

        ApplyPandorasBoxPolicy(player);
    }

    private static CardCreationOptions BuildSealedNeowOptions(Player player)
    {
        // Do not call Hook here — CardFactory.CreateForReward runs ModifyCardRewardCreationOptions once.
        // CharacterCards would otherwise concat the extra character pool twice.
        CardCreationOptions seed = new CardCreationOptions(
                new[] { player.Character.CardPool },
                CardCreationSource.Other,
                CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.ForceRarityOddsChange |
                       CardCreationFlags.IsCardReward);

        List<CardModel> merged = seed.GetPossibleCards(player)
            .Concat(StarterDeckHelpers.GetMergedStarterCards(player))
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        merged = StarterDeckHelpers.FilterStrikeDefendIfEnabled(player.RunState, merged)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        return new CardCreationOptions(merged, CardCreationSource.Other, CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.ForceRarityOddsChange |
                       CardCreationFlags.IsCardReward);
    }

    private static async Task ChooseSealedNeowWithStarters(Player player)
    {
        CardCreationOptions options = BuildSealedNeowOptions(player);
        IEnumerable<CardCreationResult> source = CardFactory.CreateForReward(player, 30, options).ToList();
        CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("modifiers", "SEALED_DECK.selectionPrompt"), 10)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
            Comparison = CompareSealedCards
        };
        List<CardModel> cards = (await CardSelectCmd.FromSimpleGridForRewards(
            new BlockingPlayerChoiceContext(), source.ToList(), player, prefs)).ToList();
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(cards, PileType.Deck), 1.2f, CardPreviewStyle.GridLayout);
        ApplyPandorasBoxPolicy(player);
    }

    private static int CompareSealedCards(CardModel card1, CardModel card2)
    {
        if (card1.Rarity != card2.Rarity)
            return card1.Rarity.CompareTo(card2.Rarity);

        return string.Compare(card1.Title, card2.Title, LocManager.Instance.CultureInfo, CompareOptions.None);
    }

    private static CardCreationOptions BuildInsanityNeowOptions(Player player)
    {
        // Same as sealed: Hook runs inside CreateForReward; avoid double CharacterCards pool merge.
        CardCreationOptions baseOpts = CardCreationOptions
                .ForNonCombatWithUniformOdds(new[] { player.Character.CardPool })
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.IsCardReward);

        List<CardModel> merged = baseOpts.GetPossibleCards(player)
            .Concat(StarterDeckHelpers.GetMergedStarterCards(player))
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        merged = StarterDeckHelpers.FilterStrikeDefendIfEnabled(player.RunState, merged)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();

        return new CardCreationOptions(merged, CardCreationSource.Other, CardRarityOddsType.Uniform)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.NoRarityModification |
                       CardCreationFlags.IsCardReward);
    }

    private static async Task ObtainInsanityNeowWithStarters(Player player)
    {
        List<CardPileAddResult> results = new List<CardPileAddResult>();
        for (int i = 0; i < 30; i++)
        {
            CardCreationOptions options = BuildInsanityNeowOptions(player);
            CardModel card = CardFactory.CreateForReward(player, 1, options).First().Card;
            results.Add(await CardPileCmd.Add(card, PileType.Deck));
        }

        foreach (CardPileAddResult item in results)
        {
            CardCmd.PreviewCardPileAdd(item, 1.2f, CardPreviewStyle.MessyLayout);
            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }

        await Cmd.CustomScaledWait(0.6f, 1.2f);
        ApplyPandorasBoxPolicy(player);
    }

    [HarmonyPatch(typeof(Draft), nameof(Draft.GenerateNeowOption))]
    private static class DraftNeowStarterPlusPatch
    {
        private static void Postfix(EventModel eventModel, ref Func<Task>? __result)
        {
            if (__result == null || !HasStarterPlus(eventModel.Owner!.RunState))
                return;

            __result = () => OfferDraftNeowWithStarters(eventModel.Owner!);
        }
    }

    [HarmonyPatch(typeof(SealedDeck), nameof(SealedDeck.GenerateNeowOption))]
    private static class SealedNeowStarterPlusPatch
    {
        private static void Postfix(EventModel eventModel, ref Func<Task>? __result)
        {
            if (__result == null || !HasStarterPlus(eventModel.Owner!.RunState))
                return;

            __result = () => ChooseSealedNeowWithStarters(eventModel.Owner!);
        }
    }

    [HarmonyPatch(typeof(Insanity), nameof(Insanity.GenerateNeowOption))]
    private static class InsanityNeowStarterPlusPatch
    {
        private static void Postfix(EventModel eventModel, ref Func<Task>? __result)
        {
            if (__result == null || !HasStarterPlus(eventModel.Owner!.RunState))
                return;

            __result = () => ObtainInsanityNeowWithStarters(eventModel.Owner!);
        }
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

    [HarmonyPatch(typeof(CardFactory), "CreateForReward", new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardCreationOptions) })]
    private static class RewardPoolStarterPlusPatches
    {
        private static bool Prefix(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options, ref CardModel __result)
        {
            if (!HasStarterPlus(player.RunState))
                return true;

            if (options.RarityOdds == CardRarityOddsType.Uniform)
                return PrefixUniform(player, blacklist, options, ref __result);

            if (options.RarityOdds == CardRarityOddsType.RegularEncounter
                && options.Source == CardCreationSource.Other
                && options.Flags.HasFlag(CardCreationFlags.IsCardReward)
                && player.RunState.Modifiers.Any(m => m is SealedDeck))
            {
                return PrefixSealedStyleReward(player, blacklist, options, ref __result);
            }

            return true;
        }

        private static bool PrefixUniform(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options,
            ref CardModel __result)
        {
            options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

            IEnumerable<CardModel> possible = options.GetPossibleCards(player).Except(blacklist).ToList();
            possible = FilterForPlayerCount(player.RunState, possible).ToArray();

            IEnumerable<CardModel> items = possible.Where(c => c.Rarity != CardRarity.Ancient);
            items = StarterDeckHelpers.FilterStrikeDefendIfEnabled(player.RunState, items);

            Rng rng = options.RngOverride ?? player.PlayerRng.Rewards;
            CardModel? canonical = rng.NextItem(items);
            if (canonical == null)
                return true;

            __result = player.RunState.CreateCard(canonical, player);
            return false;
        }

        private static bool PrefixSealedStyleReward(Player player, IEnumerable<CardModel> blacklist, CardCreationOptions options,
            ref CardModel __result)
        {
            options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);

            IEnumerable<CardModel> possible = options.GetPossibleCards(player).Except(blacklist).ToList();
            possible = FilterForPlayerCount(player.RunState, possible).ToArray();
            possible = StarterDeckHelpers.FilterStrikeDefendIfEnabled(player.RunState, possible).ToArray();

            HashSet<CardRarity> allowedRarities = possible.Select(c => c.Rarity).ToHashSet();
            CardRarity selectedRarity = RollForRarity(player, options.RarityOdds, options.Source, allowedRarities,
                options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange));

            if (selectedRarity == CardRarity.None)
                return true;

            IEnumerable<CardModel> items = possible.Where(c => c.Rarity == selectedRarity);
            if (selectedRarity == CardRarity.Common)
                items = items.Concat(possible.Where(c => c.Rarity == CardRarity.Basic));

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

    /// <summary>
    /// Custom run UI: Starter+ requires a Neow deck mode; Minus Strike and Defend requires Starter+, Starter Rewards, or Starter Shops.
    /// </summary>
    [HarmonyPatch(typeof(NCustomRunModifiersList), "AfterModifiersChanged")]
    private static class CustomRunModifierDependencyPatch
    {
        private static void Postfix(NCustomRunModifiersList __instance, NRunModifierTickbox tickbox)
        {
            try
            {
                object? raw = Traverse.Create(__instance).Field("_modifierTickboxes").GetValue();
                if (raw is not List<NRunModifierTickbox> boxes)
                    return;

                bool changed = false;
                changed |= EnforceStarterPlusRequiresNeowDeck(boxes);
                changed |= EnforceMinusStrikeDefendRequiresEconomy(boxes);

                if (changed)
                {
                    AccessTools.DeclaredMethod(typeof(NCustomRunModifiersList), "EmitSignalModifiersChanged")
                        ?.Invoke(__instance, null);
                }
            }
            catch
            {
            }
        }

        private static bool EnforceStarterPlusRequiresNeowDeck(List<NRunModifierTickbox> boxes)
        {
            NRunModifierTickbox? starterTick = boxes.FirstOrDefault(t => t.Modifier is DraftPlus);
            if (starterTick?.IsTicked != true)
                return false;

            if (boxes.Any(t => t.IsTicked && IsNeowDeckModeModifier(t.Modifier)))
                return false;

            starterTick.IsTicked = false;
            return true;
        }

        private static bool EnforceMinusStrikeDefendRequiresEconomy(List<NRunModifierTickbox> boxes)
        {
            NRunModifierTickbox? minusTick = boxes.FirstOrDefault(t => t.Modifier is MinusStrikeDefendStarters);
            if (minusTick?.IsTicked != true)
                return false;

            if (boxes.Any(t => t.IsTicked && IsStarterEconomyModifier(t.Modifier)))
                return false;

            minusTick.IsTicked = false;
            return true;
        }

        private static bool IsNeowDeckModeModifier(ModifierModel? m)
            => m is Draft or SealedDeck or Insanity;

        private static bool IsStarterEconomyModifier(ModifierModel? m)
            => m is DraftPlus or StarterRewards or StarterShop;
    }
}
