using System;
using System.Collections.Generic;

namespace FM26PlayerExport.Handlers;

public enum StarVisualState
{
	NotStar,
	Empty,
	GoldHalf,
	GoldFull,
	SilverHalf,
	SilverFull,
	GoldSilverHalf,
	Unknown
}

public sealed class StarRatingResult
{
	public static readonly StarRatingResult Unrecognised = new StarRatingResult(0f, 0f, false);

	public float GoldStars { get; }

	public float SilverStars { get; }

	public float DisplayedStars => GoldStars + SilverStars;

	public bool Recognised { get; }

	public StarRatingResult(float goldStars, float silverStars, bool recognised)
	{
		GoldStars = goldStars;
		SilverStars = silverStars;
		Recognised = recognised;
	}
}

public static class StarRatingParser
{
	private const string StarClass = "fm-star-rating-star";

	private const string StarNoMarginClass = "fm-star-rating-star-no-margin";

	private const string GoldFullClass = "ability-max-potential-level-fullfilled-youth-false";

	private const string GoldHalfClass = "ability-half-potential-level-none-youth-false";

	private const string SilverFullYouthClass = "ability-max-potential-level-fullfilled-youth-true";

	private const string SilverHalfClass = "ability-half-potential-level-none-youth-true";

	private const string SilverFullMinimumClass = "ability-minimum-potential-level-full-youth-false";

	private const string GoldSilverHalfClass = "ability-half-potential-level-half-youth-false";

	private const string SilverHalfPotentialClass = "ability-minimum-potential-level-half-youth-false";

	private const string EmptyClass = "ability-minimum-potential-level-none-youth-false";

	private const string EmptyYouthClass = "ability-minimum-potential-level-none-youth-true";

	public static StarVisualState Classify(IEnumerable<string> classNames)
	{
		bool isStar = false;
		StarVisualState state = StarVisualState.Unknown;
		foreach (string className in classNames)
		{
			if (className == StarClass || className == StarNoMarginClass)
			{
				isStar = true;
				continue;
			}
			StarVisualState candidate = className switch
			{
				GoldFullClass => StarVisualState.GoldFull,
				GoldHalfClass => StarVisualState.GoldHalf,
				SilverFullYouthClass => StarVisualState.SilverFull,
				SilverHalfClass => StarVisualState.SilverHalf,
				SilverFullMinimumClass => StarVisualState.SilverFull,
				GoldSilverHalfClass => StarVisualState.GoldSilverHalf,
				SilverHalfPotentialClass => StarVisualState.SilverHalf,
				EmptyClass => StarVisualState.Empty,
				EmptyYouthClass => StarVisualState.Empty,
				_ => StarVisualState.NotStar
			};
			if (candidate == StarVisualState.NotStar)
			{
				continue;
			}
			if (state != StarVisualState.Unknown && state != candidate)
			{
				return StarVisualState.Unknown;
			}
			state = candidate;
		}
		return isStar ? state : StarVisualState.NotStar;
	}

	public static bool TryParseRating(IEnumerable<IEnumerable<string>> starClassLists, out StarRatingResult result)
	{
		int starCount = 0;
		float goldStars = 0f;
		float silverStars = 0f;
		bool sawSilver = false;
		foreach (IEnumerable<string> classNames in starClassLists)
		{
			StarVisualState state = Classify(classNames);
			switch (state)
			{
			case StarVisualState.NotStar:
				continue;
			case StarVisualState.Empty:
				starCount++;
				break;
			case StarVisualState.GoldFull:
			case StarVisualState.GoldHalf:
				if (sawSilver)
				{
					result = StarRatingResult.Unrecognised;
					return false;
				}
				starCount++;
				goldStars += state == StarVisualState.GoldFull ? 1f : 0.5f;
				break;
			case StarVisualState.GoldSilverHalf:
				starCount++;
				goldStars += 0.5f;
				silverStars += 0.5f;
				sawSilver = true;
				break;
			case StarVisualState.SilverFull:
			case StarVisualState.SilverHalf:
				starCount++;
				sawSilver = true;
				silverStars += state == StarVisualState.SilverFull ? 1f : 0.5f;
				break;
			default:
				result = StarRatingResult.Unrecognised;
				return false;
			}
		}
		if (starCount != 5)
		{
			result = StarRatingResult.Unrecognised;
			return false;
		}
		result = new StarRatingResult(goldStars, silverStars, true);
		return true;
	}
}
