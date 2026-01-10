using LiteDB;

namespace BodyOutfitPresetDB.Models
{
    public class Preset
    {
        [BsonId(false)]
        public string? Id { get; set; }
        public int GameId { get; set; }
        public int ModId { get; set; }
        public int? SortId { get; set; }

        public string? Source { get; set; }

        public string? Name { get; set; }
        public string? GameDomainName { get; set; }
        public string? Url { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Summary { get; set; }
        public string? Author { get; set; }
        public string? UploaderName { get; set; }
        public string? Category { get; set; }
        public string? Version { get; set; }
        public int? Downloads { get; set; }
        public int? Endorsements { get; set; }
        public int? FileSize { get; set; }
        public string? Status { get; set; }
        public bool? AdultContent { get; set; }

        public bool? NoExport { get; set; }

        public bool? TaggedAsPreset { get; set; }
        public string? Description { get; set; }
    }
}
