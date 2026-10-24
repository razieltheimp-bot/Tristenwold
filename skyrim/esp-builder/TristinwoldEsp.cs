using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;
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

    /// <summary>Every place in world/world.json: Skyrim name -> its name in this world.</summary>
    public static IReadOnlyDictionary<string, string> Renames { get; } = Atlas.Default.AllPlaces();

    public static (SkyrimMod Mod, List<string> Report) Build(ISkyrimModGetter skyrim, Atlas? atlas = null)
    {
        atlas ??= Atlas.Default;
        var renames = atlas.AllPlaces();
        var mod = new SkyrimMod(FileName, SkyrimRelease.SkyrimSE);
        // Light (ESL-flagged) so it doesn't take a full load-order slot.
        mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
        var report = new List<string>();
        var cache = skyrim.ToImmutableLinkCache();

        string? Rename(string? name) => name is not null && renames.TryGetValue(name, out var to) ? to : null;

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

        foreach (var move in atlas.Moves) MoveNpc(skyrim, cache, mod, move, report);
        return (mod, report);
    }

    /// <summary>
    /// Takes a character out of their city and puts them in another: the old placement is
    /// disabled, a new one is made in the destination interior, and their daily routine is
    /// replaced with "wander around where you stand" so they don't walk home.
    /// </summary>
    static void MoveNpc(ISkyrimModGetter skyrim, ILinkCache cache, SkyrimMod mod, NpcMove move, List<string> report)
    {
        var npc = skyrim.Npcs.FirstOrDefault(n => n.Name?.String == move.Npc);
        if (npc is null) { report.Add($"SKIPPED move: no character named {move.Npc}."); return; }

        var destination = skyrim.EnumerateMajorRecordContexts<ICell, ICellGetter>(cache)
            .FirstOrDefault(c => c.Record.Name?.String?.Equals(move.To, StringComparison.OrdinalIgnoreCase) == true);
        if (destination is null) { report.Add($"SKIPPED move: no interior named {move.To} for {move.Npc}."); return; }

        // Stand them where someone or something already stands, so they're on the floor.
        var anchor = destination.Record.Persistent.Concat(destination.Record.Temporary)
            .Select(r => r switch
            {
                IPlacedNpcGetter n => n.Placement,
                IPlacedObjectGetter o => o.Placement,
                _ => null,
            })
            .FirstOrDefault(p => p is not null);
        if (anchor is null) { report.Add($"SKIPPED move: {move.To} has nothing to stand {move.Npc} next to."); return; }

        var originals = skyrim.EnumerateMajorRecordContexts<IPlacedNpc, IPlacedNpcGetter>(cache)
            .Where(c => c.Record.Base.FormKey == npc.FormKey)
            .ToList();
        foreach (var original in originals)
        {
            var hidden = original.GetOrAddAsOverride(mod);
            hidden.MajorRecordFlagsRaw |= (int)SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled;
            if (hidden.Placement is { } p) hidden.Placement = new Placement { Position = p.Position with { Z = -30000 }, Rotation = p.Rotation };
        }

        var cell = destination.GetOrAddAsOverride(mod);
        cell.Persistent.Add(new PlacedNpc(mod)
        {
            Base = npc.ToNullableLink(),
            Placement = new Placement { Position = anchor.Position with { X = anchor.Position.X + 64 }, Rotation = anchor.Rotation },
        });

        var moved = mod.Npcs.GetOrAddAsOverride(npc);
        if (move.Rename is { } name) moved.Name = name;
        var sandbox = skyrim.Packages.FirstOrDefault(p => p.EditorID == "DefaultSandboxEditorLocation512")
            ?? skyrim.Packages.FirstOrDefault(p => p.EditorID?.StartsWith("DefaultSandboxEditorLocation", StringComparison.Ordinal) == true);
        if (sandbox is not null)
        {
            moved.Packages.Clear();
            moved.Packages.Add(sandbox.ToLink());
        }

        var essential = npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Essential);
        report.Add($"Moved {move.Npc}{(move.Rename is null ? "" : $" (now {move.Rename})")} to {move.To}"
            + $"{(originals.Count == 0 ? ", no original placement found" : "")}"
            + $"{(sandbox is null ? ", WARNING: kept old routine (no sandbox package found)" : "")}"
            + $"{(essential ? ", WARNING: essential to a quest, which may break" : "")}.");
    }
}
