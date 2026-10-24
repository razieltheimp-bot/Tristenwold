using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Tristinwold.EspBuilder;
using Tristinwold.Shared;

namespace Tristinwold.EspBuilder.Tests;

public class TristinwoldEspTests
{
    sealed record FakeSkyrim(string Path, FormKey Nazeem, FormKey NazeemAtTheMare, FormKey Sandbox);

    /// <summary>A tiny stand-in for Skyrim.esm with a few of the real records' names, written to disk.</summary>
    static FakeSkyrim MakeFakeSkyrim()
    {
        var skyrim = new SkyrimMod(ModKey.FromFileName("Skyrim.esm"), SkyrimRelease.SkyrimSE);
        foreach (var (id, name) in new[] { ("RiftenLocation", "Riften"), ("WhiterunLocation", "Whiterun"), ("BleakFallsBarrowLocation", "Bleak Falls Barrow") })
            skyrim.Locations.AddNew(id).Name = name;

        var sandbox = skyrim.Packages.AddNew("DefaultSandboxEditorLocation512");
        var nazeem = skyrim.Npcs.AddNew("Nazeem");
        nazeem.Name = "Nazeem";
        nazeem.Packages.Add(skyrim.Packages.AddNew("NazeemWhiterunSchedule").ToLink());

        Cell Interior(string editorId, string name) => new(skyrim) { EditorID = editorId, Name = name, Flags = Cell.Flag.IsInteriorCell };
        var flagon = Interior("RiftenRaggedFlagon", "The Ragged Flagon");
        flagon.Temporary.Add(new PlacedObject(skyrim)
        {
            Base = new FormLinkNullable<IPlaceableObjectGetter>(new FormKey(skyrim.ModKey, 0x10)),
            MapMarker = new MapMarker { Name = "Riften", Flags = MapMarker.Flag.Visible },
        });
        var mare = Interior("WhiterunBanneredMare", "The Bannered Mare");
        var nazeemAtTheMare = new PlacedNpc(skyrim) { Base = nazeem.ToNullableLink(), Placement = new Placement { Position = new P3Float(10, 20, 30) } };
        mare.Persistent.Add(nazeemAtTheMare);
        var drink = Interior("FalkreathDeadMansDrink", "Dead Man's Drink");
        drink.Temporary.Add(new PlacedObject(skyrim)
        {
            Base = new FormLinkNullable<IPlaceableObjectGetter>(new FormKey(skyrim.ModKey, 0x11)),
            Placement = new Placement { Position = new P3Float(100, 200, 50) },
        });

        skyrim.Cells.Records.Add(new CellBlock
        {
            BlockNumber = 0,
            GroupType = GroupTypeEnum.InteriorCellBlock,
            SubBlocks = { new CellSubBlock { BlockNumber = 0, GroupType = GroupTypeEnum.InteriorCellSubBlock, Cells = { flagon, mare, drink } } },
        });

        var path = System.IO.Path.Combine(Directory.CreateTempSubdirectory("skyrim-data-").FullName, "Skyrim.esm");
        skyrim.WriteToBinary(path);
        return new FakeSkyrim(path, nazeem.FormKey, nazeemAtTheMare.FormKey, sandbox.FormKey);
    }

    [Fact]
    public void Each_city_is_renamed_and_characters_move_between_them()
    {
        var fake = MakeFakeSkyrim();
        using var skyrim = SkyrimMod.CreateFromBinaryOverlay(fake.Path, SkyrimRelease.SkyrimSE);
        var atlas = Atlas.Default with { Moves = [new NpcMove("Nazeem", "Dead Man's Drink")] };
        var (mod, report) = TristinwoldEsp.Build(skyrim, atlas);

        var output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fake.Path)!, TristinwoldEsp.FileName);
        mod.WriteToBinary(output);
        using var esp = SkyrimMod.CreateFromBinaryOverlay(output, SkyrimRelease.SkyrimSE);

        // Cities and places are renamed; places in no city are left alone.
        Assert.Equal(["Galeholt", "Tristinwold"], esp.Locations.Select(l => l.Name!.String!).Order());
        var cells = esp.EnumerateMajorRecords<ICellGetter>().ToDictionary(c => c.EditorID!);
        Assert.Equal("The Gallows Perch", cells["RiftenRaggedFlagon"].Name?.String);
        Assert.Equal("The Spur and Saddle", cells["WhiterunBanneredMare"].Name?.String);
        Assert.Equal("The Last Toast", cells["FalkreathDeadMansDrink"].Name?.String);

        // Nazeem leaves Galeholt for Gravenmoor's inn.
        var placed = esp.EnumerateMajorRecords<IPlacedNpcGetter>().ToList();
        var old = placed.Single(p => p.FormKey == fake.NazeemAtTheMare);
        Assert.True(((SkyrimMajorRecord.SkyrimMajorRecordFlag)old.MajorRecordFlagsRaw).HasFlag(SkyrimMajorRecord.SkyrimMajorRecordFlag.InitiallyDisabled));
        var arrived = Assert.Single(cells["FalkreathDeadMansDrink"].Persistent.OfType<IPlacedNpcGetter>());
        Assert.Equal(fake.Nazeem, arrived.Base.FormKey);
        Assert.Equal(164, arrived.Placement!.Position.X); // beside the thing already standing there

        var npc = Assert.Single(esp.Npcs);
        Assert.Equal([fake.Sandbox], npc.Packages.Select(p => p.FormKey));
        Assert.Contains(report, r => r.StartsWith("Moved Nazeem to Dead Man's Drink"));

        Assert.True(esp.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Small), "should be ESL-flagged");
    }

    [Fact]
    public void A_move_that_cant_be_done_is_reported_not_guessed()
    {
        var fake = MakeFakeSkyrim();
        using var skyrim = SkyrimMod.CreateFromBinaryOverlay(fake.Path, SkyrimRelease.SkyrimSE);
        var atlas = Atlas.Default with { Moves = [new NpcMove("Nobody", "The Ragged Flagon"), new NpcMove("Nazeem", "Nowhere")] };
        var (mod, report) = TristinwoldEsp.Build(skyrim, atlas);
        Assert.Contains(report, r => r.Contains("no character named Nobody"));
        Assert.Contains(report, r => r.Contains("no interior named Nowhere"));
        Assert.Empty(mod.Npcs);
    }
}
