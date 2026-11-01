using Godot;

namespace Tristinwold.Game;

/// <summary>A chamber or room on a structure's plan: where it is in the structure's own metres, how wide, and whether the hero has been in it.</summary>
public sealed record ChartRoom(string Id, string Name, Vector2 At, float Radius, bool Seen);

/// <summary>
/// The plan of the structure the hero is inside (the mine's chambers and drifts, a ship's deck), as much of it as they've
/// seen, for the map in place of the world: rooms and the ways between them, an outline where the structure has one, a few
/// marked spots, and where the hero stands and faces on it. Distances are the structure's own metres.
/// </summary>
public sealed record StructureChart(
    string Title,
    IReadOnlyList<ChartRoom> Rooms,
    IReadOnlyList<(string From, string To)> Links,
    IReadOnlyList<Vector2>? Outline,
    IReadOnlyList<(Vector2 At, string Name)> Marks,
    Vector2 Player,
    Vector2 Facing,
    float MetresPerPixel);
