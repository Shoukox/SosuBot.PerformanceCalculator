﻿using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

// ReSharper disable InconsistentNaming

namespace SosuBot.PerformanceCalculator;

public static class AccuracyTools
{
    public static class Osu
    {
        // todo
        // methodparams as a single param class
        // create an abstract class for every ruleset class 
        public static Dictionary<HitResult, int> GenerateHitResults(IBeatmap beatmap, Mod[] mods, double accuracy,
            int? goods = null, int? mehs = null, int misses = 0, int largeTickMisses = 0, int sliderTailMisses = 0)
        {
            // Use lazer info only if score has sliderhead accuracy
            if (mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value))
                return generateHitResults(beatmap, accuracy, misses, mehs, goods, null, null);

            return generateHitResults(beatmap, accuracy, misses, mehs, goods, largeTickMisses,
                sliderTailMisses);
        }

        private static Dictionary<HitResult, int> generateHitResults(IBeatmap beatmap, double accuracy,
            int countMiss, int? countMeh, int? countGood, int? countLargeTickMisses, int? countSliderTailMisses)
        {
            int countGreat;

            var totalResultCount = beatmap.HitObjects.Count;

            if (countMeh != null || countGood != null)
            {
                countGreat = totalResultCount - (countGood ?? 0) - (countMeh ?? 0) - countMiss;
            }
            else
            {
                // Total result count excluding countMiss
                var relevantResultCount = totalResultCount - countMiss;

                // Accuracy excluding countMiss. We need that because we're trying to achieve target accuracy without touching countMiss
                // So it's better to pretened that there were 0 misses in the 1st place
                var relevantAccuracy = accuracy * totalResultCount / relevantResultCount;

                // Clamp accuracy to account for user trying to break the algorithm by inputting impossible values
                relevantAccuracy = Math.Clamp(relevantAccuracy, 0, 1);

                // Main curve for accuracy > 25%, the closer accuracy is to 25% - the more 50s it adds
                if (relevantAccuracy >= 0.25)
                {
                    // Main curve. Zero 50s if accuracy is 100%, one 50 per 9 100s if accuracy is 75% (excluding misses), 4 50s per 9 100s if accuracy is 50%
                    var ratio50To100 = Math.Pow(1 - (relevantAccuracy - 0.25) / 0.75, 2);

                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c50 = c100 * ratio50to100
                    var count100Estimate =
                        6 * relevantResultCount * (1 - relevantAccuracy) / (5 * ratio50To100 + 4);

                    // Get count50 according to c50 = c100 * ratio50to100
                    var count50Estimate = count100Estimate * ratio50To100;

                    // Round it to get int number of 100s
                    countGood = (int?)Math.Round(count100Estimate);

                    // Get number of 50s as difference between total mistimed hits and count100
                    countMeh = (int?)(Math.Round(count100Estimate + count50Estimate) - countGood);
                }
                // If accuracy is between 16.67% and 25% - we assume that we have no 300s
                else if (relevantAccuracy >= 1.0 / 6)
                {
                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c300 = 0
                    var count100Estimate = 6 * relevantResultCount * relevantAccuracy - relevantResultCount;

                    // We only had 100s and 50s in that scenario so rest of the hits are 50s
                    var count50Estimate = relevantResultCount - count100Estimate;

                    // Round it to get int number of 100s
                    countGood = (int?)Math.Round(count100Estimate);

                    // Get number of 50s as difference between total mistimed hits and count100
                    countMeh = (int?)(Math.Round(count100Estimate + count50Estimate) - countGood);
                }
                // If accuracy is less than 16.67% - it means that we have only 50s or misses
                // Assuming that we removed misses in the 1st place - that means that we need to add additional misses to achieve target accuracy
                else
                {
                    // Derived from the formula: Accuracy = (6 * c300 + 2 * c100 + c50) / (6 * totalHits), assuming that c300 = c100 = 0
                    var count50Estimate = 6 * relevantResultCount * relevantAccuracy;

                    // We have 0 100s, because we can't start adding 100s again after reaching "only 50s" point
                    countGood = 0;

                    // Round it to get int number of 50s
                    countMeh = (int?)Math.Round(count50Estimate);

                    // Fill the rest results with misses overwriting initial countMiss
                    countMiss = (int)(totalResultCount - countMeh);
                }

                // Rest of the hits are 300s
                countGreat = (int)(totalResultCount - countGood - countMeh - countMiss);
            }

            var result = new Dictionary<HitResult, int>
            {
                { HitResult.Great, countGreat },
                { HitResult.Ok, countGood ?? 0 },
                { HitResult.Meh, countMeh ?? 0 },
                { HitResult.Miss, countMiss }
            };

            if (countLargeTickMisses != null)
                result[HitResult.LargeTickMiss] = countLargeTickMisses.Value;

            if (countSliderTailMisses != null)
                result[HitResult.SliderTailHit] =
                    beatmap.HitObjects.Count(x => x is Slider) - countSliderTailMisses.Value;

            return result;
        }

        public static double GetAccuracy(IBeatmap beatmap, Dictionary<HitResult, int> statistics)
        {
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];

            double total = 6 * countGreat + 2 * countGood + countMeh;
            double max = 6 * (countGreat + countGood + countMeh + countMiss);

            if (statistics.TryGetValue(HitResult.SliderTailHit, out var countSliderTailHit))
            {
                var countSliders = beatmap.HitObjects.Count(x => x is Slider);

                total += 3 * countSliderTailHit;
                max += 3 * countSliders;
            }

            if (statistics.TryGetValue(HitResult.LargeTickMiss, out var countLargeTickMiss))
            {
                var countLargeTicks = beatmap.HitObjects.Sum(obj =>
                    obj.NestedHitObjects.Count(x => x is SliderTick or SliderRepeat));
                var countLargeTickHit = countLargeTicks - countLargeTickMiss;

                total += 0.6 * countLargeTickHit;
                max += 0.6 * countLargeTicks;
            }

            return total / max;
        }
    }

    public static class Taiko
    {
        public static Dictionary<HitResult, int> GenerateHitResults(IBeatmap beatmap, Mod[] mods, double accuracy,
            int misses = 0, int? goods = null)
        {
            return generateHitResults(accuracy, beatmap, misses, goods);
        }

        private static Dictionary<HitResult, int> generateHitResults(double accuracy, IBeatmap beatmap, int countMiss,
            int? countGood)
        {
            var totalResultCount = beatmap.GetMaxCombo();

            int countGreat;

            if (countGood != null)
            {
                countGreat = (int)(totalResultCount - countGood - countMiss);
            }
            else
            {
                // Let Great=2, Good=1, Miss=0. The total should be this.
                var targetTotal = (int)Math.Round(accuracy * totalResultCount * 2);

                countGreat = targetTotal - (totalResultCount - countMiss);
                countGood = totalResultCount - countGreat - countMiss;
            }

            return new Dictionary<HitResult, int>
            {
                { HitResult.Great, countGreat },
                { HitResult.Ok, (int)countGood },
                { HitResult.Meh, 0 },
                { HitResult.Miss, countMiss }
            };
        }

        public static double GetAccuracy(IBeatmap beatmap, Dictionary<HitResult, int> statistics, Mod[] mods)
        {
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Ok];
            var countMiss = statistics[HitResult.Miss];
            var total = countGreat + countGood + countMiss;

            return (double)(2 * countGreat + countGood) / (2 * total);
        }
    }

    public static class Catch
    {
        public static Dictionary<HitResult, int> GenerateHitResults(IBeatmap beatmap, Mod[] mods, double accuracy,
            int misses = 0, int? mehs = null, int? goods = null)
        {
            return generateHitResults(beatmap, accuracy, misses, mehs, goods);
        }

        private static Dictionary<HitResult, int> generateHitResults(IBeatmap beatmap, double accuracy,
            int countMiss, int? countMeh, int? countGood)
        {
            var maxCombo = beatmap.GetMaxCombo();
            var maxTinyDroplets = beatmap.HitObjects.OfType<JuiceStream>()
                .Sum(s => s.NestedHitObjects.OfType<TinyDroplet>().Count());
            var maxDroplets =
                beatmap.HitObjects.OfType<JuiceStream>().Sum(s => s.NestedHitObjects.OfType<Droplet>().Count()) -
                maxTinyDroplets;
            var maxFruits = beatmap.HitObjects.Sum(h =>
                h is Fruit ? 1 : (h as JuiceStream)?.NestedHitObjects.Count(n => n is Fruit) ?? 0);

            // Either given or max value minus misses
            var countDroplets = countGood ?? Math.Max(0, maxDroplets - countMiss);

            // Max value minus whatever misses are left. Negative if impossible missCount
            var countFruits = maxFruits - (countMiss - (maxDroplets - countDroplets));

            // Either given or the max amount of hit objects with respect to accuracy minus the already calculated fruits and drops.
            // Negative if accuracy not feasable with missCount.
            var countTinyDroplets = countMeh ?? (int)Math.Round(accuracy * (maxCombo + maxTinyDroplets)) -
                countFruits - countDroplets;

            // Whatever droplets are left
            var countTinyMisses = maxTinyDroplets - countTinyDroplets;

            return new Dictionary<HitResult, int>
            {
                { HitResult.Great, countFruits },
                { HitResult.LargeTickHit, countDroplets },
                { HitResult.SmallTickHit, countTinyDroplets },
                { HitResult.SmallTickMiss, countTinyMisses },
                { HitResult.Miss, countMiss }
            };
        }

        public static double GetAccuracy(IBeatmap beatmap, Dictionary<HitResult, int> statistics, Mod[] mods)
        {
            double hits = statistics[HitResult.Great] + statistics[HitResult.LargeTickHit] +
                          statistics[HitResult.SmallTickHit];
            var total = hits + statistics[HitResult.Miss] + statistics[HitResult.SmallTickMiss];

            return hits / total;
        }
    }

    public static class Mania
    {
        public static Dictionary<HitResult, int> GenerateHitResults(IBeatmap beatmap, Mod[] mods, double accuracy,
            int? greats = null, int? oks = null, int? goods = null, int? mehs = null, int misses = 0)
        {
            return generateHitResults(beatmap, mods, accuracy, misses, mehs, oks, goods, greats);
        }

        private static Dictionary<HitResult, int> generateHitResults(IBeatmap beatmap, Mod[] mods, double accuracy,
            int countMiss, int? countMeh, int? countOk, int? countGood, int? countGreat)
        {
            // One judgement per normal note. Two judgements per hold note (head + tail).
            var totalHits = beatmap.HitObjects.Count;
            if (!mods.Any(m => m is ModClassic))
                totalHits += beatmap.HitObjects.Count(ho => ho is HoldNote);

            if (countMeh != null || countOk != null || countGood != null || countGreat != null)
            {
                var countPerfect = totalHits - (countMiss + (countMeh ?? 0) + (countOk ?? 0) + (countGood ?? 0) +
                                                (countGreat ?? 0));

                return new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = countPerfect,
                    [HitResult.Great] = countGreat ?? 0,
                    [HitResult.Good] = countGood ?? 0,
                    [HitResult.Ok] = countOk ?? 0,
                    [HitResult.Meh] = countMeh ?? 0,
                    [HitResult.Miss] = countMiss
                };
            }

            var perfectValue = mods.Any(m => m is ModClassic) ? 60 : 61;

            // Let Great = 60, Good = 40, Ok = 20, Meh = 10, Miss = 0, Perfect = 61 or 60 depending on CL. The total should be this.
            var targetTotal = (int)Math.Round(accuracy * totalHits * perfectValue);

            // Start by assuming every non miss is a meh
            // This is how much increase is needed by the rest
            var remainingHits = totalHits - countMiss;
            var delta = Math.Max(targetTotal - 10 * remainingHits, 0);

            // Each perfect increases total by 50 (CL) or 51 (no CL) (perfect - meh = 50 or 51)
            var perfects = Math.Min(delta / (perfectValue - 10), remainingHits);
            delta -= perfects * (perfectValue - 10);
            remainingHits -= perfects;

            // Each great increases total by 50 (great - meh = 50)
            var greats = Math.Min(delta / 50, remainingHits);
            delta -= greats * 50;
            remainingHits -= greats;

            // Each good increases total by 30 (good - meh = 30)
            countGood = Math.Min(delta / 30, remainingHits);
            delta -= countGood.Value * 30;
            remainingHits -= countGood.Value;

            // Each ok increases total by 10 (ok - meh = 10)
            var oks = Math.Min(delta / 10, remainingHits);
            remainingHits -= oks;

            // Everything else is a meh, as initially assumed
            countMeh = remainingHits;

            return new Dictionary<HitResult, int>
            {
                { HitResult.Perfect, perfects },
                { HitResult.Great, greats },
                { HitResult.Ok, oks },
                { HitResult.Good, countGood.Value },
                { HitResult.Meh, countMeh.Value },
                { HitResult.Miss, countMiss }
            };
        }

        public static double GetAccuracy(IBeatmap beatmap, Dictionary<HitResult, int> statistics, Mod[] mods)
        {
            var countPerfect = statistics[HitResult.Perfect];
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Good];
            var countOk = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];

            var perfectWeight = mods.Any(m => m is ModClassic) ? 300 : 305;

            double total = perfectWeight * countPerfect + 300 * countGreat + 200 * countGood +
                           100 * countOk + 50 * countMeh;
            double max = perfectWeight * (countPerfect + countGreat + countGood + countOk + countMeh + countMiss);

            return total / max;
        }
    }
}