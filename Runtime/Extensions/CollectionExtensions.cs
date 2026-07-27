using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace gishadev.tools.Extensions
{
    public static class CollectionExtensions
    {
        public static T GetNextValue<T>(this IEnumerable<T> source, int currentIndex)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (currentIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(currentIndex), "Index is out of range.");

            List<T> list = source.ToList();
            if (list.Count == 0)
                throw new ArgumentException("The source collection is empty.");

            int nextIndex = (currentIndex + 1) % list.Count;
            return list[nextIndex];
        }

        public static T GetRandomElement<T>(this IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return default;

            return source.Count == 1 ? source[0] : source[Random.Range(0, source.Count)];
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source == null || !source.Any();
        }
    }
}
