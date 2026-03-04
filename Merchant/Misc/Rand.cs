namespace Merchant.Misc;

public static class Rand
{
    public static void ShuffleInPlace<T>(this Random rand, List<T> listToShuffle)
    {
        int n = listToShuffle.Count;
        while (n > 1)
        {
            // https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
            n--;
            int k = rand.Next(n + 1);
            (listToShuffle[n], listToShuffle[k]) = (listToShuffle[k], listToShuffle[n]);
        }
    }

    public static List<int> GetShuffledIdx(this Random rand, int startingIdx, int listSize, int maxCount)
    {
        if (maxCount <= 0 || startingIdx >= listSize)
            return [];
        int targetMax = Math.Min(listSize - startingIdx, maxCount);
        List<int> ranges = Enumerable.Range(startingIdx, targetMax).ToList();
        rand.ShuffleInPlace(ranges);
        return ranges;
    }
}
