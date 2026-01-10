using LiteDB;
using BodyOutfitPresetDB.Models;
using SharpCompress.Archives;
using SharpCompress.Readers;
using Mod = BodyOutfitPresetDB.Models.Mod;

namespace BodyOutfitPresetDB.Utilities
{
    public enum GameId
    {
        SkyrimLE = 110,
        SkyrimSE = 1704,
        Fallout4 = 1151
    }

    public static class Export
    {
        public static void ExportModListCSV(string outputFilePath, ILiteCollection<Mod> mods, GameId? gameId = null)
        {
            using var file = new StreamWriter(outputFilePath);

            // Write header
            file.WriteLine("Type\tName\tSource\tStatus\tLink\tAuthors\tUpload Date\tLast Updated\tAdult Mod\tSkimpy\tNon-Skimpy\tRevealing\tPhysics req.\tPhysics sup.\tHigh Heels\tHeavy Armor\tLight Armor\tClothing\tVanilla/CC");

            void writeMod(Mod mod)
            {
                // Write mod line
                var line =
                    $"""
                    {mod.Type}{'\t'}
                    {mod.Name}{'\t'}
                    {mod.Source}{'\t'}
                    {mod.Status}{'\t'}
                    {mod.Url}{'\t'}
                    {mod.Author}{'\t'}
                    {mod.CreatedAt.GetValueOrDefault().Date:dd/MM/yyyy}{'\t'}
                    {mod.UpdatedAt.GetValueOrDefault().Date:dd/MM/yyyy}{'\t'}
                    {((mod.AdultContent ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagSkimpy ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagNonSkimpy ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagRevealing ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagPhysicsRequired ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagPhysicsSupported ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagHighHeels ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagHeavyArmor ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagLightArmor ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagClothing ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((mod.TagVanillaCC ?? false) ? "TRUE" : "FALSE")}
                    """;
                line = line.ReplaceLineEndings("");
                file.WriteLine(line);
            }

            IEnumerable<Mod> modsFilter;
            if (gameId != null)
            {
                int gameIdInt = (int)gameId;
                modsFilter = mods.Find(m => m.GameId == gameIdInt && m.NoExport != true);
            }
            else
                modsFilter = mods.Find(m => m.NoExport != true);

            modsFilter = modsFilter.OrderBy(m => (m.GameId, m.ModId));

            foreach (var mod in modsFilter)
            {
                writeMod(mod);
            }
        }

        public static void ExportPresetListCSV(string outputFilePath, ILiteCollection<Preset> presets, GameId? gameId = null)
        {
            using var file = new StreamWriter(outputFilePath);

            // Write header
            file.WriteLine("Name\tSource\tStatus\tLink\tAuthors\tUpload Date\tLast Updated\tAdult Mod\tPreset Tag\tEndorsements");

            void writePreset(Preset preset)
            {
                // Write preset line
                var line =
                    $"""
                    {preset.Name}{'\t'}
                    {preset.Source}{'\t'}
                    {preset.Status}{'\t'}
                    {preset.Url}{'\t'}
                    {preset.Author}{'\t'}
                    {preset.CreatedAt.GetValueOrDefault().Date:dd/MM/yyyy}{'\t'}
                    {preset.UpdatedAt.GetValueOrDefault().Date:dd/MM/yyyy}{'\t'}
                    {((preset.AdultContent ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {((preset.TaggedAsPreset ?? false) ? "TRUE" : "FALSE")}{'\t'}
                    {preset.Endorsements}
                    """;
                line = line.ReplaceLineEndings("");
                file.WriteLine(line);
            }

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
                writePreset(mod);
            }
        }
    }
}
