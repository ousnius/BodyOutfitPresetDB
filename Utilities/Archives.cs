using SharpCompress.Archives;
using SharpCompress.Common;

namespace BodyOutfitPresetDB.Utilities
{
    public static class Archives
    {
        public static void ExtractFilesFromArchives(string archivesSrcDir, string extractDir, string fileExtension, bool fullPath = true)
        {
            Directory.CreateDirectory(extractDir);

            foreach (var archivePath in Directory.EnumerateFiles(archivesSrcDir))
            {
                try
                {
                    using var archive = ArchiveFactory.Open(archivePath);

                    var filePrefix = string.Empty;

                    var modId = Path.GetFileName(archivePath).Split("-").FirstOrDefault();
                    if (modId != null)
                        filePrefix = modId + "-";

                    foreach (var entry in archive.Entries.Where(
                        entry =>
                        !entry.IsDirectory &&
                        !string.IsNullOrEmpty(entry.Key) &&
                        (entry.Key?.ToLower().EndsWith(fileExtension) ?? false)))
                    {
                        if (!fullPath)
                        {
                            string destFileName = Path.Combine(extractDir, filePrefix + Path.GetFileName(entry.Key));
                            entry.WriteToFile(destFileName, new ExtractionOptions()
                            {
                                Overwrite = true
                            });
                        }
                        else
                        {
                            entry.WriteToDirectory(extractDir, new ExtractionOptions()
                            {
                                ExtractFullPath = fullPath,
                                Overwrite = true
                            });
                        }
                    }

                    Console.WriteLine($"Extracted '{fileExtension}' files from archive: {archivePath}");
                }
                catch (InvalidOperationException)
                {
                    // Ignore unsupported archive formats
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to extract '{archivePath}':");
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
