using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIChess.Core.Helpers;

public static class RandomSelector
{
    private static readonly Random Random = new();

    extension<T>(IList<T> items)
    {
        public List<T> SelectRandomUniqueItems(int maxCount = 5)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be non-negative.");

            int count = Math.Min(maxCount, items.Count);
            var itemList = items.ToList();
            itemList.Shuffle();
            return itemList.Take(count).ToList();
            //return items.OrderBy(x => Random.Next()).Take(count).ToList();
        }

        public void Shuffle()
        {
            var n = items.Count;
            while (n > 1)
            {
                n--;
                var k = Random.Next(n + 1);
                (items[k], items[n]) = (items[n], items[k]);
            }
        }

        public IList<T> ShuffledList()
        {
            items.Shuffle();
            return items;
        }
    }
}