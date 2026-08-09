using System;
using FM26PlayerExport.Handlers;

internal static class StarRatingParserTests
{
	private static readonly string[] GoldFull = { "ability-max-potential-level-fullfilled-youth-false", "fm-star-rating-star" };

	private static readonly string[] GoldHalf = { "ability-half-potential-level-none-youth-false", "fm-star-rating-star" };

	private static readonly string[] SilverFullYouth = { "ability-max-potential-level-fullfilled-youth-true", "fm-star-rating-star" };

	private static readonly string[] SilverHalf = { "ability-half-potential-level-none-youth-true", "fm-star-rating-star" };

	private static readonly string[] SilverFullMinimum = { "ability-minimum-potential-level-full-youth-false", "fm-star-rating-star" };

	private static readonly string[] Empty = { "ability-minimum-potential-level-none-youth-false", "fm-star-rating-star" };

	private static readonly string[] FinalEmpty = { "ability-minimum-potential-level-none-youth-false", "fm-star-rating-star-no-margin" };

	private static readonly string[] Unrelated = { "button", "condition", "icon", "on" };

	private static void Main()
	{
		AssertRating("empty", 0f, 0f, Empty, Empty, Empty, Empty, FinalEmpty);
		AssertRating("Vicky Ability", 1.5f, 0f, GoldFull, GoldHalf, Empty, Empty, FinalEmpty);
		AssertRating("Vicky Potential", 2.5f, 0f, GoldFull, GoldFull, GoldHalf, Empty, FinalEmpty);
		AssertRating("silver-only 2.5", 0f, 2.5f, SilverFullYouth, SilverFullYouth, SilverHalf, Empty, FinalEmpty);
		AssertRating("silver-only 4.0", 0f, 4f, SilverFullYouth, SilverFullYouth, SilverFullMinimum, SilverFullMinimum, FinalEmpty);
		AssertRating("mixed 2.5 gold + 2.0 silver", 2.5f, 2f, GoldFull, GoldFull, GoldHalf, SilverFullMinimum, SilverFullMinimum);
		AssertRating("mixed 3.0 gold + 1.5 silver", 3f, 1.5f, GoldFull, GoldFull, GoldFull, SilverFullMinimum, SilverHalf);
		AssertRating("five gold", 5f, 0f, GoldFull, GoldFull, GoldFull, GoldFull, GoldFull);
		AssertRating("unrelated descendants", 1.5f, 0f, GoldFull, GoldHalf, Empty, Empty, FinalEmpty, Unrelated);
		AssertInvalid("unknown star", new[] { "fm-star-rating-star", "condition", "on" }, Empty, Empty, Empty, FinalEmpty);
		AssertInvalid("silver before gold", SilverFullMinimum, GoldFull, Empty, Empty, FinalEmpty);
		Console.WriteLine("StarRatingParser regression tests passed.");
	}

	private static void AssertRating(string name, float expectedGold, float expectedSilver, params string[][] starClassLists)
	{
		if (!StarRatingParser.TryParseRating(starClassLists, out StarRatingResult result) || result.GoldStars != expectedGold || result.SilverStars != expectedSilver || result.DisplayedStars != expectedGold + expectedSilver)
		{
			throw new InvalidOperationException(name + ": expected " + expectedGold + " gold / " + expectedSilver + " silver.");
		}
	}

	private static void AssertInvalid(string name, params string[][] starClassLists)
	{
		if (StarRatingParser.TryParseRating(starClassLists, out _))
		{
			throw new InvalidOperationException(name + ": invalid star sequence was accepted.");
		}
	}
}
