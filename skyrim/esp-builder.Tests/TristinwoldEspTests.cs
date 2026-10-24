using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Tristinwold.EspBuilder;

namespace Tristinwold.EspBuilder.Tests;

public class TristinwoldEspTests
{
    /// <summary>A tiny stand-in for Skyrim.esm with a few of Riften's records, written to disk.</summary>
    static string FakeSkyrim()
    {
        var skyrim = new SkyrimMod(ModKey.FromFileName("Skyrim.esm"), SkyrimRelease.SkyrimSE);
        var riften = skyrim.Locations.AddNew("RiftenLocation");
        riften.Name = "Riften";
        var whiterun = skyrim.Locations.AddNew("WhiterunLocation");
        whiterun.Name = "Whiterun";

        var flagon = new Cell(skyrim) { EditorID = "RiftenRaggedFlagon", Name = "The Ragged Flagon", Flags = Cell.Flag.IsInteriorCell };
        var marker = new PlacedObject(skyrim)
        {
            Base = new FormLinkNullable<IPlaceableObjectGetter>(new FormKey(skyrim.ModKey, 0x10)),
            MapMarker = new MapMarker { Name = "Riften", Flags = MapMarker.Flag.Visible },
        };
        flagon.Temporary.Add(marker);
        skyrim.Cells.Records.Add(new CellBlock
        {
            BlockNumber = 0,
            GroupType = GroupTypeEnum.InteriorCellBlock,
            SubBlocks = { new CellSubBlock { BlockNumber = 0, GroupType = GroupTypeEnum.InteriorCellSubBlock, Cells = { flagon } } },
        });

        var dir = Directory.CreateTempSubdirectory("skyrim-data-").FullName;
        var path = Path.Combine(dir, "Skyrim.esm");
        skyrim.WriteToBinary(path);
        return path;
    }

    [Fact]
    public void Riftens_places_come_out_with_tristinwold_names()
    {
        var master = FakeSkyrim();
        using var skyrim = SkyrimMod.CreateFromBinaryOverlay(master, SkyrimRelease.SkyrimSE);
        var (mod, report) = TristinwoldEsp.Build(skyrim);

        var output = Path.Combine(Path.GetDirectoryName(master)!, TristinwoldEsp.FileName);
        mod.WriteToBinary(output);
        using var esp = SkyrimMod.CreateFromBinaryOverlay(output, SkyrimRelease.SkyrimSE);

        Assert.Equal("Tristinwold", Assert.Single(esp.Locations).Name?.String); // Whiterun is left alone
        var cell = Assert.Single(esp.EnumerateMajorRecords<ICellGetter>());
        Assert.Equal("The Gallows Perch", cell.Name?.String);
        var placed = Assert.Single(esp.EnumerateMajorRecords<IPlacedObjectGetter>());
        Assert.Equal("Tristinwold", placed.MapMarker?.Name?.String);

        Assert.True(esp.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Small), "should be ESL-flagged");
        Assert.Contains(esp.ModHeader.MasterReferences, m => m.Master == ModKey.FromFileName("Skyrim.esm"));
        Assert.Equal(3, report.Count);
    }
}
