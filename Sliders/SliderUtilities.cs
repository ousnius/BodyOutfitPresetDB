using BodyOutfitPresetDB.Sliders.Enums;
using BodyOutfitPresetDB.Sliders.Structs;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace BodyOutfitPresetDB.Sliders
{
    public static class SliderUtilities
    {
        public static BodyRegion MapSliderCategoryToRegion(string categoryName)
        {
            return categoryName switch
            {
                "Vagina and Anal" => BodyRegion.VaginaAndAnal,
                "Full Body" => BodyRegion.FullBody,
                "Seams" => BodyRegion.Seams,
                "Breasts" => BodyRegion.Breasts,
                "Nipples" => BodyRegion.Nipples,
                "Torso" => BodyRegion.Torso,
                "Butt" => BodyRegion.Butt,
                "Legs and Feet" => BodyRegion.LegsAndFeet,
                "Muscle Definition" => BodyRegion.MuscleDefinition,
                "Hips" => BodyRegion.Hips,
                "Arms" => BodyRegion.Arms,
                "Belly" => BodyRegion.Belly,
                _ => BodyRegion.Unknown
            };
        }

        public static (List<SliderPreset> Presets, List<XMLSliderValue> SliderValues) LoadPresetFiles(string inputDirectory, Dictionary<string, BodyRegion>? bodyRegions = null)
        {
            var presets = new List<SliderPreset>();
            var sliderValues = new List<XMLSliderValue>();

            foreach (var file in Directory.GetFiles(inputDirectory, "*.xml"))
            {
                var doc = XDocument.Load(file);

                // Loop over all <Preset> elements in the file
                foreach (var presetElement in doc.Descendants("Preset"))
                {
                    string presetName = (string?)presetElement.Attribute("name") ?? "Unnamed";

                    // Load sliders inside this <Preset>
                    var sliders = presetElement.Descendants("SetSlider")
                        .Select(x => new SliderInfo
                        {
                            Name = (string?)x.Attribute("name"),
                            Region = BodyRegion.Unknown,    // Map later using slider to region dictionary
                            BigValue = float.Parse(x.Attribute("value")?.Value ?? "0", CultureInfo.InvariantCulture),
                            SmallValue = 0f                 // Will be computed later
                        })
                        .Where(x => Constants.SliderNames3BA.Contains(x.Name)) // Only CBBE/3BA sliders
                        .ToList();

                    foreach (ref var slider in CollectionsMarshal.AsSpan(sliders))
                    {
                        if (bodyRegions != null)
                        {
                            if (bodyRegions.TryGetValue(slider.Name ?? "", out var bodyRegion))
                                slider.Region = bodyRegion;
                        }
                    }

                    if (sliders.Count > 0)
                    {
                        presets.Add(new SliderPreset
                        {
                            Name = presetName,
                            Sliders = sliders
                        });
                    }

                    sliderValues.AddRange(
                        doc.Descendants("SetSlider")
                           .Select(x => new XMLSliderValue
                           {
                               Name = (string?)x.Attribute("name"),
                               Size = (string?)x.Attribute("size"),
                               Value = int.Parse(x.Attribute("value")?.Value ?? "0", CultureInfo.InvariantCulture)
                           })
                           .Where(x => Constants.SliderNames3BA.Contains(x.Name)) // CBBE/3BA sliders only
                    );
                }
            }

            return (presets, sliderValues);
        }

        public static Dictionary<string, BodyRegion> LoadSliderRegionsFile(string xmlPath)
        {
            var sliderToRegion = new Dictionary<string, BodyRegion>(
                StringComparer.OrdinalIgnoreCase);

            var doc = XDocument.Load(xmlPath);

            if (doc.Root == null || doc.Root.Name != "SliderCategories")
                throw new InvalidOperationException("Invalid slider category XML.");

            foreach (var categoryElement in doc.Root.Elements("Category"))
            {
                var categoryName = (string?)categoryElement.Attribute("name");
                if (string.IsNullOrWhiteSpace(categoryName))
                    continue;

                BodyRegion region = MapSliderCategoryToRegion(categoryName);

                foreach (var sliderElement in categoryElement.Elements("Slider"))
                {
                    var sliderName = (string?)sliderElement.Attribute("name");
                    if (string.IsNullOrWhiteSpace(sliderName))
                        continue;

                    if (sliderToRegion.ContainsKey(sliderName))
                    {
                        // Duplicate slider names happen in real XMLs — log but allow override
                        Console.WriteLine(
                            $"Warning: Slider '{sliderName}' already mapped, overriding.");
                    }

                    sliderToRegion[sliderName] = region;
                }
            }

            sliderToRegion["PregnancyBelly"] = BodyRegion.PregnancyBelly;

            return sliderToRegion;
        }
    }
}
