using System;
using System.Collections.Generic;
using System.Globalization;

using Newtonsoft.Json;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
namespace Kapture
{
    /// <summary>
    /// Loot Event parsed from chat messages.
    /// </summary>
    public class LootEvent
    {
        /// <summary>
        /// Gets or sets unix timestamp of event.
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets loot message.
        /// </summary>
        public LootMessage LootMessage { get; set; } = null!;

        /// <summary>
        /// Gets or sets loot event type.
        /// </summary>
        public LootEventType LootEventType { get; set; }

        /// <summary>
        /// Gets or sets loot event type name.
        /// </summary>
        public string LootEventTypeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether event is for local player.
        /// </summary>
        public bool IsLocalPlayer { get; set; }

        /// <summary>
        /// Gets or sets player name.
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets player home world name.
        /// </summary>
        public string World { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets roll.
        /// </summary>
        public ushort Roll { get; set; }

        /// <summary>
        /// Gets or sets territory type id.
        /// </summary>
        public uint TerritoryTypeId { get; set; }

        /// <summary>
        /// Gets or sets content id.
        /// </summary>
        public uint ContentId { get; set; }

        /// <summary>
        /// Gets or sets the human-readable territory (place) name.
        /// </summary>
        public string TerritoryName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable content (duty) name; empty when not in content.
        /// </summary>
        public string ContentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets loot event id.
        /// </summary>
        public Guid LootEventId { get; set; }

        /// <summary>
        /// Gets or sets item name.
        /// </summary>
        public string ItemName { get; set; } = null!;

        /// <summary>
        /// Gets or sets item name abbreviated.
        /// </summary>
        [JsonIgnore]
        public string ItemNameAbbreviated { get; set; } = null!;

        /// <summary>
        /// Get CSV Headings.
        /// </summary>
        /// <returns>string of csv headings.</returns>
        public static string GetCsvHeadings()
        {
            return string.Join(",", new List<string>
            {
                "Timestamp",
                "Territory",
                "Content",
                "LootEventType",
                "ItemId",
                "ItemName",
                "PlayerName",
                "World",
                "Roll",
            });
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Convert object to csv as string.
        /// </summary>
        /// <returns>csv as string.</returns>
        public string ToCsv()
        {
            return string.Join(",", new List<string>
            {
                DateTimeOffset.FromUnixTimeMilliseconds(this.Timestamp)
                    .LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                this.TerritoryName,
                this.ContentName,
                this.LootEventTypeName,
                this.LootMessage.ItemId.ToString(),
                this.LootMessage.ItemName,
                this.PlayerName,
                this.World,
                this.Roll.ToString(),
            });
        }
    }
}
