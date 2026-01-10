using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BodyOutfitPresetDB.Models;
using BodyOutfitPresetDB.Utilities;
using StrawberryShake;

namespace BodyOutfitPresetDB.Programs
{
    public static class PresetsConsole
    {
        public static int Run(IConfiguration config)
        {
            // Open database (or create if doesn't exist)
            using var presetDB = new LiteDatabase(@"PresetDatabase.db");

            // Get a collection (or create, if doesn't exist)
            var presets = presetDB.GetCollection<Preset>("presets");

            int presetCount = presets.Count();
            Console.WriteLine($"Presets in database: {presetCount}");

            if (ConsoleUtil.ReadYesNoConfirmation("Delete all presets in database? Y/n"))
            {
                presets.DeleteAll();
                Console.WriteLine($"{presetCount} presets deleted.");
                presetCount = presets.Count();
            }

            if (ConsoleUtil.ReadYesNoConfirmation("Fetch new mods data from Nexus Mods? Y/n"))
            {
                Console.WriteLine("Creating GraphQL client for Nexus Mods...");

                var serviceCollection = new ServiceCollection();

                serviceCollection
                    .AddNexusGraphQLClient()
                    .ConfigureHttpClient(client =>
                    {
                        client.BaseAddress = new Uri("https://api.nexusmods.com/v2/graphql");
                        //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    });

                var services = serviceCollection.BuildServiceProvider();

                var client = services.GetRequiredService<INexusGraphQLClient>();

                var date = new DateOnly(2024, 5, 7);

                // Convert to DateTime at midnight UTC
                var dateTimeUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

                // Unix timestamp (seconds)
                long unixTimestamp = new DateTimeOffset(dateTimeUtc).ToUnixTimeSeconds();

                var filter = new ModsFilter()
                {
                    Status = [new() { Op = FilterComparisonOperator.Equals, Value = "published" }],
                    Endorsements = [new() { Op = FilterComparisonOperator.Gt, Value = 25 }],
                    //CreatedAt = [new() { Op = FilterComparisonOperator.Gte, Value = unixTimestamp.ToString() }],
                    FileSize = [new() { Op = FilterComparisonOperator.Lte, Value = 100 }], // 100 KB?
                    Op = FilterLogicalOperator.And,
                    Filter = [
                        new()
                {
                    Op = FilterLogicalOperator.Or,
                    GameDomainName = [
                        new() { Op = FilterComparisonOperator.Equals, Value = "skyrimspecialedition" },
                    ]
                },
                new()
                {
                    Op = FilterLogicalOperator.Or,
                    Name = [
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "CBBE" },
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "3BA" },
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "Preset" },
                        //new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "Body Preset" },
                        //new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "BodySlide Preset" },
                    ]
                },
                new()
                {
                    Op = FilterLogicalOperator.Or,
                    CategoryName = [
                        new() { Op = FilterComparisonOperator.Equals, Value = "Body, Face, and Hair" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Models and Textures" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Visuals and Graphics" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Character Presets" }
                    ],
                    Tag = [
                        new() { Op = FilterComparisonOperator.Equals, Value = "Bodyslide, OutfitStudio, and TexBlend Presets"},
                    ]
                }
                    ]
                };

                //var sortList = new List<ModsSort>()
                //{
                //    new() { Endorsements = new BaseSortValue() { Direction = SortDirection.Desc } }
                //};

                int offset = 0;
                int count = 100;

                int newPresetCount = 0;

                while (true)
                {
                    var result = client.GetMods.ExecuteAsync(filter, [], offset, count).Result;
                    result.EnsureNoErrors();

                    if (result.Data == null)
                        break;

                    var presetsData = result.Data.Mods;

                    int totalCount = presetsData.TotalCount;
                    int pageCount = presetsData.Nodes.Count;
                    if (pageCount <= 0)
                        break;

                    foreach (var mod in presetsData.Nodes)
                    {
                        if (mod.Game == null)
                        {
                            Console.WriteLine($"Mod {mod.ModId} has no game domain data. Skipping.");
                            continue;
                        }

                        /*
                        var modFiles = await client.QueryModFiles.ExecuteAsync(mod.ModId.ToString(), mod.Game.Id.ToString());
                        modFiles.EnsureNoErrors();

                        foreach (var modFile in modFiles.Data.ModFiles)
                        {
                        }
                        */

                        if (!mod.Name.Contains("CBBE") && !mod.Name.Contains("3BA") &&
                            (mod.Name.Contains("UBE") || mod.Name.Contains("BHUNP") || mod.Name.Contains("UUNP") || mod.Name.Contains("HIMBO") || mod.Name.Contains("TBD")))
                        {
                            Console.WriteLine($"Skipping preset {mod.ModId} '{mod.Name}' (game '{mod.Game.Name}') due to different body mod in title...");
                            continue;
                        }

                        Console.WriteLine($"Adding preset {mod.ModId} '{mod.Name}' (game '{mod.Game.Name}') to database...");

                        string? gameDomainName = mod.Game?.DomainName ?? null;

                        // Create new preset instance
                        var newPreset = new Preset
                        {
                            Id = mod.Uid,
                            GameId = mod.Game!.Id,
                            ModId = mod.ModId,
                            Source = "Nexus Mods",
                            Name = mod.Name,
                            GameDomainName = gameDomainName,
                            Url = $"https://www.nexusmods.com/{gameDomainName}/mods/{mod.ModId}",
                            CreatedAt = mod.CreatedAt.DateTime,
                            UpdatedAt = mod.UpdatedAt.DateTime,
                            Category = mod.ModCategory?.Name ?? null,
                            Author = mod.Author,
                            Summary = mod.Summary,
                            Downloads = mod.Downloads,
                            Endorsements = mod.Endorsements,
                            UploaderName = mod.Uploader?.Name ?? null,
                            Version = mod.Version,
                            FileSize = mod.FileSize,
                            Status = mod.Status,
                            AdultContent = mod.AdultContent,
                            TaggedAsPreset = true, // FIXME false
                            Description = mod.Description
                        };

                        if (newPreset.Status == "published")
                            newPreset.Status = "Published";

                        // Insert or update mod (Id will be auto-incremented)
                        if (presets.FindById(mod.Id) == null)
                            newPresetCount++;

                        presets.Upsert(newPreset);
                    }

                    offset += pageCount;

                    if (offset >= totalCount)
                        break;
                }

                // Index document
                presets.EnsureIndex(x => x.GameId);
                presets.EnsureIndex(x => x.ModId);
                presets.EnsureIndex(x => x.Name);

                Console.WriteLine("Done fetching presets.");

                presetCount = presets.Count();
                Console.WriteLine($"{newPresetCount} new presets in database. Total: {presetCount}");
            }

            if (ConsoleUtil.ReadYesNoConfirmation("Export CSV preset list? Y/n"))
            {
                Console.WriteLine("Which game?");
                Console.WriteLine("1 = Skyrim LE");
                Console.WriteLine("2 = Skyrim SE");
                Console.WriteLine("3 = Fallout 4");
                Console.WriteLine("any = all games");

                GameId? gameId = null;
                string exportPath = "presetlist.csv";

                switch (ConsoleUtil.ReadNumber())
                {
                    case 1:
                        gameId = GameId.SkyrimLE;
                        exportPath = "presetlist_LE.csv";
                        break;
                    case 2:
                        gameId = GameId.SkyrimSE;
                        exportPath = "presetlist_SE.csv";
                        break;
                    case 3:
                        gameId = GameId.Fallout4;
                        exportPath = "presetlist_FO4.csv";
                        break;
                }

                Export.ExportPresetListCSV(exportPath, presets, gameId);

                Console.WriteLine($"Preset list exported to '{exportPath}'.");
            }

            if (ConsoleUtil.ReadYesNoConfirmation("Download and extract all BodySlide presets? Y/n"))
            {
                Console.WriteLine("Which game?");
                Console.WriteLine("1 = Skyrim LE");
                Console.WriteLine("2 = Skyrim SE");
                Console.WriteLine("3 = Fallout 4");
                Console.WriteLine("any = all games");

                GameId? gameId = null;
                string downloadPath = "preset_archives";
                string extractPath = "presets";

                switch (ConsoleUtil.ReadNumber())
                {
                    case 1:
                        gameId = GameId.SkyrimLE;
                        downloadPath = "preset_archives_LE";
                        extractPath = "presets_LE";
                        break;
                    case 2:
                        gameId = GameId.SkyrimSE;
                        downloadPath = "preset_archives_SE";
                        extractPath = "presets_SE";
                        break;
                    case 3:
                        gameId = GameId.Fallout4;
                        downloadPath = "preset_archives_FO4";
                        extractPath = "presets_FO4";
                        break;
                }

                string nexusModsApiKey = config.GetSection("nexusMods")["apiKey"] ?? "";
                if (string.IsNullOrWhiteSpace(nexusModsApiKey))
                {
                    Console.WriteLine("No Nexus Mods api key provided!");
                    return -1;
                }

                Nexus.DownloadBodySlidePresets(nexusModsApiKey, downloadPath, presets, gameId);
                Console.WriteLine($"BodySlide preset archives from Nexus downloaded to '{downloadPath}'.");

                Archives.ExtractFilesFromArchives(downloadPath, extractPath, ".xml", false);
                Console.WriteLine($"BodySlide presets extracted to '{extractPath}'.");
            }

            if (ConsoleUtil.ReadYesNoConfirmation("Generate additional presets from all BodySlide presets? Y/n"))
            {
                Console.WriteLine("Which game?");
                Console.WriteLine("1 = Skyrim LE");
                Console.WriteLine("2 = Skyrim SE");
                Console.WriteLine("3 = Fallout 4");
                Console.WriteLine("any = all games");

                string extractPath = "presets";

                switch (ConsoleUtil.ReadNumber())
                {
                    case 1:
                        extractPath = "presets_LE";
                        break;
                    case 2:
                        extractPath = "presets_SE";
                        break;
                    case 3:
                        extractPath = "presets_FO4";
                        break;
                }

                BodySlide.CalculatePresetsAverage(extractPath);
                Console.WriteLine($"Average presets calculated from '{extractPath}'.");

                BodySlide.GenerateRandomPresets(extractPath, 1000);
                Console.WriteLine($"Random preset generated from '{extractPath}'.");
            }

            return 0;
        }
    }
}
