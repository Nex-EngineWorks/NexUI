using System;
using System.Collections.Generic;

namespace emiteat.NexUI.Samples.CollectionDemo
{
    /// <summary>One row of the demo data. A plain class, deliberately: the collection never sees it.</summary>
    [Serializable]
    public sealed class CollectionDemoItem
    {
        public int Id;
        public string Name;
        public string Rarity;
        public int Amount;

        public override string ToString() => $"{Name} x{Amount}";
    }

    /// <summary>
    /// Generates the demo rows and stands in for whatever a real game would use - an inventory
    /// service, a quest log, a server page.
    /// </summary>
    /// <remarks>
    /// The point of the sample is that this type knows nothing about UI, and the collection knows
    /// nothing about items. They meet at <c>INXCollectionSource</c> plus a bind callback.
    /// </remarks>
    public static class CollectionDemoData
    {
        private static readonly string[] Rarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        private static readonly string[] Nouns =
        {
            "Potion", "Blade", "Shield", "Charm", "Rune", "Scroll", "Ember", "Sigil", "Relic", "Token"
        };

        /// <summary>Builds <paramref name="count"/> deterministic rows, so screenshots stay comparable.</summary>
        public static List<CollectionDemoItem> Build(int count)
        {
            var items = new List<CollectionDemoItem>(Math.Max(0, count));
            for (var i = 0; i < count; i++)
            {
                items.Add(new CollectionDemoItem
                {
                    Id = i,
                    Name = $"{Nouns[i % Nouns.Length]} {i:0000}",
                    Rarity = Rarities[i % Rarities.Length],
                    Amount = 1 + i % 99
                });
            }
            return items;
        }
    }
}
