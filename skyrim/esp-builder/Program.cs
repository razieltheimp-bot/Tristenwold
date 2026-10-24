// Builds Tristinwold.esp: renames Riften's places inside Skyrim (locations, interior cells and
// the world-map marker) so the city reads as Tristinwold everywhere in game. Run it on your PC
// against your own Skyrim.esm:
//
//   dotnet run --project skyrim/esp-builder -- "C:\Steam\steamapps\common\Skyrim Special Edition\Data"
//
// It writes Tristinwold.esp into that Data folder (enable it in your mod manager).
using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Tristinwold.EspBuilder;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Tristinwold.EspBuilder <Skyrim Data folder> [output .esp]");
    return 1;
}
var data = args[0];
var output = args.Length > 1 ? args[1] : Path.Combine(data, TristinwoldEsp.FileName);
var master = Path.Combine(data, "Skyrim.esm");
if (!File.Exists(master))
{
    Console.Error.WriteLine($"No Skyrim.esm in {data}");
    return 1;
}

using var skyrim = SkyrimMod.CreateFromBinaryOverlay(master, SkyrimRelease.SkyrimSE);
var (mod, report) = TristinwoldEsp.Build(skyrim);
mod.WriteToBinary(output);
foreach (var line in report) Console.WriteLine(line);
Console.WriteLine($"Wrote {output}.");
return 0;
