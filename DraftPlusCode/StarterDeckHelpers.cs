using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;

namespace DraftPlus.Code;

/// <summary>StartingDeck ∪ CharacterCards extra characters.</summary>
internal static class StarterDeckHelpers
{
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

        return starters.Concat(extraStarters);
    }

    internal static HashSet<ModelId> GetMergedStarterIds(Player player)
        => GetMergedStarterCards(player).Select(c => c.Id).ToHashSet();
}
