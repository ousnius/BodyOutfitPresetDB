using BodyOutfitPresetDB.Sliders;
using BodyOutfitPresetDB.Sliders.Enums;
using BodyOutfitPresetDB.Sliders.Structs;
using LiteDB;
using SharpCompress.Archives;
using SharpCompress.Readers;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace BodyOutfitPresetDB.Utilities
{
    public static partial class BodySlide
    {
        private static bool IsStructuralRegion(BodyRegion region) =>
            region == BodyRegion.FullBody ||
            region == BodyRegion.Torso ||
            region == BodyRegion.Hips ||
            region == BodyRegion.Belly;

        private static float SmallScaleForRegion(BodyRegion region)
        {
            return IsStructuralRegion(region) ? 0.3f : 0.6f;
        }

        public static readonly Dictionary<BodyRegion, float> RegionBudgets = new()
        {
            { BodyRegion.FullBody, 120f },
            { BodyRegion.Torso, 100f },
            { BodyRegion.Hips, 130f },
            { BodyRegion.Belly, 140f },

            { BodyRegion.Breasts, 400f },
            { BodyRegion.Butt, 250f },
            { BodyRegion.LegsAndFeet, 200f },
            { BodyRegion.Arms, 90f },

            // Detail regions with looser limits
            { BodyRegion.Nipples, 500f },
            { BodyRegion.Seams, 1000f },
            { BodyRegion.MuscleDefinition, 300f },
            { BodyRegion.VaginaAndAnal, 400f },

            { BodyRegion.Unknown, 100f }
        };

        public static float GetSliderWeight(string sliderName)
        {
            return Constants.SliderWeights.TryGetValue(sliderName, out var w) ? w : 1f;
        }

        public static void GenerateRandomPresets(string inputDirectory, int count)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string outputFile = $"RandomPresets_{timestamp}.xml";

            var bodyRegions = SliderUtilities.LoadSliderRegionsFile("CBBE 3BA.xml");
            var (Presets, SliderValues) = SliderUtilities.LoadPresetFiles(inputDirectory, bodyRegions);

            var rng = new Random();
            var generatedPresets = new Dictionary<string, List<SliderInfo>>();

            for (int i = 0; i < count; i++)
            {
                var generated =
                    SliderValues
                        .GroupBy(s => s.Name)
                        .Select(g =>
                        {
                            var list = g.ToList();
                            var value = list[rng.Next(list.Count)].Value;

                            var region = BodyRegion.Unknown;

                            if (g.Key != null)
                                bodyRegions.TryGetValue(g.Key, out region);

                            if (region == BodyRegion.Breasts)
                            {
                                // Add small random jitter
                                //value -= (int)((rng.NextDouble() * 0.15 - 0.0025) * 300);
                            }

                            return new SliderInfo
                            {
                                Name = g.Key,
                                Region = region,
                                BigValue = value
                            };
                        })
                        .ToList();

                var regionGroups = generated.GroupBy(x => x.Region);

                foreach (var group in regionGroups)
                {
                    float budget =
                        RegionBudgets.TryGetValue(group.Key, out var b)
                            ? b
                            : RegionBudgets[BodyRegion.Unknown];

                    // Add small random jitter
                    if (group.Key == BodyRegion.Breasts || group.Key == BodyRegion.Nipples)
                        budget -= (int)((rng.NextDouble() * 0.15 - 0.0025) * 2500);
                    else
                        budget -= (int)((rng.NextDouble() * 0.15 - 0.0025) * 1000);

                    if (IsStructuralRegion(group.Key))
                        budget *= 0.85f;

                    float usage = group.Sum(s => s.BigValue * GetSliderWeight(s.Name ?? string.Empty));

                    if (Math.Abs(usage) <= budget || Math.Abs(usage) <= 0.01f)
                        continue;

                    float scale = budget / usage;

                    foreach (ref var item in CollectionsMarshal.AsSpan(group.ToList()))
                    {
                        item.BigValue *= scale;
                    }
                }

                foreach (ref var slider in CollectionsMarshal.AsSpan(generated))
                {
                    slider.SmallValue = slider.BigValue * SmallScaleForRegion(slider.Region);
                }

                string presetName = $"ousnius - Random Preset {i + 1:D4}";

                generatedPresets.Add(presetName, generated);
            }

            var doc = new XDocument(
                new XElement("SliderPresets",
                    generatedPresets.Select(p =>
                        new XElement("Preset",
                            new XAttribute("name", p.Key),
                            new XAttribute("set", "CBBE Body"),
                            new XElement("Group", new XAttribute("name", "3BA")),
                            new XElement("Group", new XAttribute("name", "CBBE")),
                            new XElement("Group", new XAttribute("name", "CBBE Bodies")),
                            p.Value.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "big"),
                                    new XAttribute("value", g.BigValue)
                                )
                            ),
                            p.Value.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "small"),
                                    new XAttribute("value", g.SmallValue)
                                )
                            )
                        )
                    )
                )
            );

            doc.Save(outputFile);
        }

        public static void CalculatePresetsAverage(string inputDirectory)
        {
            string outputSmall = "AveragePreset_Small.xml";
            string outputBig = "AveragePreset_Big.xml";

            var (_, SliderValues) = SliderUtilities.LoadPresetFiles(inputDirectory);

            WriteStatisticsPresets(SliderValues, "small", outputSmall);
            WriteStatisticsPresets(SliderValues, "big", outputBig);
        }

        public static void WriteStatisticsPresets(List<XMLSliderValue> presets, string size, string outputFile)
        {
            var grouped = presets
                .Where(s => s.Size == size)
                .GroupBy(s => s.Name)
                .Select(g =>
                {
                    var values = g.Select(v => v.Value).OrderBy(v => v).ToList();
                    return new
                    {
                        Name = g.Key,
                        Average = values.Average(),
                        Median = CalculateMedian(values)
                    };
                });

            if (size == "big")
            {
                var doc = new XDocument(
                    new XElement("SliderPresets",
                        new XElement("Preset",
                            new XAttribute("name", $"Median {size}"),
                            new XAttribute("set", "CBBE Body"),
                            new XElement("Group", new XAttribute("name", "3BA")),
                            new XElement("Group", new XAttribute("name", "CBBE")),
                            new XElement("Group", new XAttribute("name", "CBBE Bodies")),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "big"),
                                    new XAttribute("value", Math.Round(g.Median, 0))
                                )
                            ),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "small"),
                                    new XAttribute("value", Math.Round(g.Median / 2.0, 0))
                                )
                            )
                        ),
                        new XElement("Preset",
                            new XAttribute("name", $"Median {size} div2"),
                            new XAttribute("set", "CBBE Body"),
                            new XElement("Group", new XAttribute("name", "3BA")),
                            new XElement("Group", new XAttribute("name", "CBBE")),
                            new XElement("Group", new XAttribute("name", "CBBE Bodies")),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "big"),
                                    new XAttribute("value", Math.Round(g.Median / 2.0, 0))
                                )
                            ),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "small"),
                                    new XAttribute("value", Math.Round(g.Median / 3.0, 0))
                                )
                            )
                        )
                    )
                );

                doc.Save(outputFile);
            }
            else
            {
                var doc = new XDocument(
                    new XElement("SliderPresets",
                        new XElement("Preset",
                            new XAttribute("name", $"Median {size}"),
                            new XAttribute("set", "CBBE Body"),
                            new XElement("Group", new XAttribute("name", "3BA")),
                            new XElement("Group", new XAttribute("name", "CBBE")),
                            new XElement("Group", new XAttribute("name", "CBBE Bodies")),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "small"),
                                    new XAttribute("value", Math.Round(g.Median, 0))
                                )
                            ),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "big"),
                                    new XAttribute("value", Math.Round(g.Median * 2.0, 0))
                                )
                            )
                        ),
                        new XElement("Preset",
                            new XAttribute("name", $"Median {size} div2"),
                            new XAttribute("set", "CBBE Body"),
                            new XElement("Group", new XAttribute("name", "3BA")),
                            new XElement("Group", new XAttribute("name", "CBBE")),
                            new XElement("Group", new XAttribute("name", "CBBE Bodies")),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "small"),
                                    new XAttribute("value", Math.Round(g.Median / 2.0, 0))
                                )
                            ),
                            grouped.Select(g =>
                                new XElement("SetSlider",
                                    new XAttribute("name", g.Name ?? string.Empty),
                                    new XAttribute("size", "big"),
                                    new XAttribute("value", Math.Round(g.Median, 0))
                                )
                            )
                        )
                    )
                );

                doc.Save(outputFile);
            }
        }

        public static double CalculateMedian(List<int> values)
        {
            int count = values.Count;

            if (count == 0)
                return 0;

            if (count % 2 == 1)
                return values[count / 2];

            return (values[count / 2 - 1] + values[count / 2]) / 2.0;
        }
    }
}
