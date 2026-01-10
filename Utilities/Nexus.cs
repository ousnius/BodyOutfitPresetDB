using LiteDB;
using BodyOutfitPresetDB.Models;
using Pathoschild.FluentNexus;
using Pathoschild.FluentNexus.Models;
using Pathoschild.Http.Client;

namespace BodyOutfitPresetDB.Utilities
{
    public static class Nexus
    {
        public static void DownloadBodySlidePresets(string nexusModsApiKey, string downloadFolder, ILiteCollection<Preset> presets, GameId? gameId = null)
        {
            Directory.CreateDirectory(downloadFolder);

            var nexusClient = new NexusClient(nexusModsApiKey, "ousnius CLI", "1.0.0");

            IEnumerable<Preset> presetsFilter;
            if (gameId != null)
            {
                int gameIdInt = (int)gameId;
                presetsFilter = presets.Find(m => m.GameId == gameIdInt && m.NoExport != true);
            }
            else
                presetsFilter = presets.Find(m => m.NoExport != true);

            presetsFilter = presetsFilter.OrderBy(m => (m.GameId, m.ModId));

            foreach (var mod in presetsFilter)
            {
                try
                {
                    var modFiles = nexusClient.ModFiles.GetModFiles(mod.GameDomainName, mod.ModId, FileCategory.Main).Result;

                    ModFile? file;
                    if (modFiles.Files.Length > 1)
                        file = modFiles.Files.FirstOrDefault(f => f.IsPrimary);
                    else
                        file = modFiles.Files.FirstOrDefault();

                    if (file == null)
                    {
                        Console.WriteLine($"No main file found: {mod.ModId};{mod.Name};{mod.Author}");

                        modFiles = nexusClient.ModFiles.GetModFiles(mod.GameDomainName, mod.ModId, FileCategory.Main, FileCategory.Update, FileCategory.Optional, FileCategory.Miscellaneous).Result;
                        file = modFiles.Files.FirstOrDefault();

                        if (file == null)
                        {
                            Console.WriteLine($"No file found: {mod.ModId};{mod.Name};{mod.Author}");
                            continue;
                        }
                    }

                    bool smallMod = file.SizeInKilobytes < 100;
                    if (!smallMod)
                    {
                        Console.WriteLine($"Mod too large: {mod.ModId};{mod.Name};{mod.Author};{file.SizeInKilobytes} KB");
                        continue;
                    }

                    var downloadLinks = nexusClient.ModFiles.GetDownloadLinks(mod.GameDomainName, mod.ModId, file.FileID).Result;

                    var s = nexusClient.HttpClient.GetAsync(downloadLinks[0].Uri.ToString()).AsStream();
                    using var fs = new FileStream(Path.Combine(downloadFolder, $"{mod.ModId}-{file.FileID}-{file.FileName}"), FileMode.OpenOrCreate);
                    s.Result.CopyTo(fs);

                    var line = $"{mod.ModId};{mod.Name};{mod.Status};{mod.Author}";
                    Console.WriteLine(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
        }
    }
}
