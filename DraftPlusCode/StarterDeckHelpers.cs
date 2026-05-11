using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Runs;

namespace DraftPlus.Code;

/// <summary>StartingDeck ∪ CharacterCards extra characters, optional Strike/Defend omission.</summary>
internal static class StarterDeckHelpers
{
    internal static bool HasStarterEconomyModifier(IRunState runState)
        => runState.Modifiers.Any(static m =>
            m is Modifiers.DraftPlus or Modifiers.StarterRewards or Modifiers.StarterShop);

    internal static bool OmitsStrikeDefendStarters(IRunState runState)
        => runState.Modifiers.Any(static m => m is Modifiers.MinusStrikeDefendStarters) &&
           HasStarterEconomyModifier(runState);

    internal static IEnumerable<CardModel> FilterStrikeDefendIfEnabled(IRunState runState, IEnumerable<CardModel> cards)
    {
        if (!OmitsStrikeDefendStarters(runState))
            return cards;

        // This is intentionally broader than "starters": Draft+/Starter+ can source Basics from character pools too.
        return cards.Where(c => !c.IsBasicStrikeOrDefend);
    }

    internal static IEnumerable<CardModel> GetMergedStarterCards(Player player)
    {
        IEnumerable<CardModel> starters = player.Character.StartingDeck;

        IEnumerable<CardModel> extraStarters =
            from m in player.RunState.Modifiers
            where m is CharacterCards
            let cc = (CharacterCards)m
            select ModelDb.GetById<CharacterModel>(cc.CharacterModel).StartingDeck
                into deck
            from c in deck
            select c;

        IEnumerable<CardModel> merged = starters.Concat(extraStarters);
        return FilterStrikeDefendIfEnabled(player.RunState, merged)
            .GroupBy(c => c.Id)
            .Select(g => g.First());
    }

    internal static HashSet<ModelId> GetMergedStarterIds(Player player)
        => GetMergedStarterCards(player).Select(c => c.Id).ToHashSet();
}
