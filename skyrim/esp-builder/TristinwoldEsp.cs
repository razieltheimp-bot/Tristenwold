using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Tristinwold.Shared;

namespace Tristinwold.EspBuilder;

/// <summary>
/// The Tristinwold plugin: overrides of Skyrim's own records that give Riften's places their
/// Tristinwold names. Matching is by the in-game name, so it doesn't depend on editor IDs.
/// </summary>
public static class TristinwoldEsp
{
    public const string FileName = "Tristinwold.esp";

    /// <summary>In-game names, capitalised for signs and the map ("The Gallows Perch").</summary>
    public static IReadOnlyDictionary<string, string> Renames { get; } =
        CampaignState.RiftenAsTristinwold()
            .ToDictionary(kv => kv.Key, kv => char.ToUpperInvariant(kv.Value[0]) + kv.Value[1..], StringComparer.OrdinalIgnoreCase);

    public static (SkyrimMod Mod, List<string> Report) Build(ISkyrimModGetter skyrim)
    {
        var mod = new SkyrimMod(FileName, SkyrimRelease.SkyrimSE);
        // Light (ESL-flagged) so it doesn't take a full load-order slot.
        mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
        var report = new List<string>();
        var cache = skyrim.ToImmutableLinkCache();

        string? Rename(string? name) => name is not null && Renames.TryGetValue(name, out var to) ? to : null;

        foreach (var location in skyrim.Locations)
        {
            if (Rename(location.Name?.String) is not { } to) continue;
            mod.Locations.GetOrAddAsOverride(location).Name = to;
            report.Add($"Location {location.EditorID}: {location.Name?.String} -> {to}");
        }

        foreach (var ctx in skyrim.EnumerateMajorRecordContexts<ICell, ICellGetter>(cache))
        {
            if (Rename(ctx.Record.Name?.String) is not { } to) continue;
            ctx.GetOrAddAsOverride(mod).Name = to;
            report.Add($"Cell {ctx.Record.EditorID}: {ctx.Record.Name?.String} -> {to}");
        }

        foreach (var ctx in skyrim.EnumerateMajorRecordContexts<IPlacedObject, IPlacedObjectGetter>(cache))
        {
            if (ctx.Record.MapMarker is not { } marker || Rename(marker.Name?.String) is not { } to) continue;
            ctx.GetOrAddAsOverride(mod).MapMarker!.Name = to;
            report.Add($"Map marker {ctx.Record.FormKey}: {marker.Name?.String} -> {to}");
        }

        return (mod, report);
    }
}
