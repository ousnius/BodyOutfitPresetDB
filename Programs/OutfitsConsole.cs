using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using BodyOutfitPresetDB.Models;
using BodyOutfitPresetDB.Utilities;
using StrawberryShake;

namespace BodyOutfitPresetDB.Programs
{
    public static class OutfitsConsole
    {
        public static int Run()
        {
            // Open database (or create if doesn't exist)
            using var outfitDB = new LiteDatabase(@"BodyOutfitPresetDB.db");

            // Get a collection (or create, if doesn't exist)
            var mods = outfitDB.GetCollection<Mod>("mods");

            int modCount = mods.Count();
            Console.WriteLine($"Mods in database: {modCount}");

            if (ConsoleUtil.ReadYesNoConfirmation("Delete all mods in database? Y/n"))
            {
                mods.DeleteAll();
                Console.WriteLine($"{modCount} mods deleted.");
                modCount = mods.Count();
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
                    Endorsements = [new() { Op = FilterComparisonOperator.Gt, Value = 100 }],
                    //CreatedAt = [new() { Op = FilterComparisonOperator.Gte, Value = unixTimestamp.ToString() }],
                    FileSize = [new() { Op = FilterComparisonOperator.Gte, Value = 100 }], // 100 KB?
                    Op = FilterLogicalOperator.And,
                    Filter = [
                        new()
                {
                    Op = FilterLogicalOperator.Or,
                    GameDomainName = [
                        new() { Op = FilterComparisonOperator.Equals, Value = "skyrim" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "skyrimspecialedition" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "fallout4" },
                    ]
                },
                new()
                {
                    Op = FilterLogicalOperator.Or,
                    Name = [
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "CBBE" },
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "3BA" },
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "3BBB" },
                        new() { Op = FilterComparisonOperatorEqualsWildcard.Wildcard, Value = "ELLE - " },
                    ]
                },
                new()
                {
                    Op = FilterLogicalOperator.Or,
                    CategoryName = [
                        new() { Op = FilterComparisonOperator.Equals, Value = "Armour" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Clothing" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Clothing and Accessories" },
                        new() { Op = FilterComparisonOperator.Equals, Value = "Body, Face, and Hair" }
                    ],
                    //Tag = [
                    //    new() { Op = FilterComparisonOperator.Equals, Value = "CBBE Bodybase"},
                    //    new() { Op = FilterComparisonOperator.Equals, Value = "UNPB Bodybase"}
                    //]
                }
                    ]
                };

                //var sortList = new List<ModsSort>()
                //{
                //    new() { Endorsements = new BaseSortValue() { Direction = SortDirection.Desc } }
                //};

                int offset = 0;
                int count = 100;

                int newModCount = 0;

                while (true)
                {
                    var result = client.GetMods.ExecuteAsync(filter, [], offset, count).Result;
                    result.EnsureNoErrors();

                    if (result.Data == null)
                        break;

                    var modsData = result.Data.Mods;

                    int totalCount = modsData.TotalCount;
                    int pageCount = modsData.Nodes.Count;
                    if (pageCount <= 0)
                        break;

                    foreach (var mod in modsData.Nodes)
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

                        Console.WriteLine($"Adding mod {mod.ModId} '{mod.Name}' (game '{mod.Game.Name}') to database...");

                        string? gameDomainName = mod.Game?.DomainName ?? null;

                        bool descContainsHeels = false;

                        if (mod.Description?.Contains("heels", StringComparison.OrdinalIgnoreCase) ?? false)
                            descContainsHeels = true;

                        // Create new mod instance
                        var newMod = new Mod
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
                            Description = mod.Description,
                            TagHighHeels = descContainsHeels
                        };

                        if (newMod.Status == "published")
                            newMod.Status = "Published";

                        // Insert or update mod (Id will be auto-incremented)
                        if (mods.FindById(mod.Id) == null)
                            newModCount++;

                        mods.Upsert(newMod);
                    }

                    offset += pageCount;

                    if (offset >= totalCount)
                        break;
                }

                // Index document
                mods.EnsureIndex(x => x.GameId);
                mods.EnsureIndex(x => x.ModId);
                mods.EnsureIndex(x => x.Name);

                Console.WriteLine("Done fetching mods.");

                modCount = mods.Count();
                Console.WriteLine($"{newModCount} new mods in database. Total: {modCount}");
            }

            if (ConsoleUtil.ReadYesNoConfirmation("Export CSV mod list? Y/n"))
            {
                Console.WriteLine("Which game?");
                Console.WriteLine("1 = Skyrim LE");
                Console.WriteLine("2 = Skyrim SE");
                Console.WriteLine("3 = Fallout 4");
                Console.WriteLine("any = all games");

                GameId? gameId = null;
                string exportPath = "modlist.csv";

                switch (ConsoleUtil.ReadNumber())
                {
                    case 1:
                        gameId = GameId.SkyrimLE;
                        exportPath = "modlist_LE.csv";
                        break;
                    case 2:
                        gameId = GameId.SkyrimSE;
                        exportPath = "modlist_SE.csv";
                        break;
                    case 3:
                        gameId = GameId.Fallout4;
                        exportPath = "modlist_FO4.csv";
                        break;
                }

                Export.ExportModListCSV(exportPath, mods, gameId);

                Console.WriteLine($"Mod list exported to '{exportPath}'.");
            }

            return 0;
        }
    }
}
