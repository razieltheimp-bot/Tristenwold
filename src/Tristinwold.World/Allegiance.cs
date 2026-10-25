namespace Tristinwold.World;

/// <summary>Which side something is on, for who fights whom without being told to.</summary>
public enum Side
{
    /// <summary>Beasts and monsters: they fight whoever comes near, but don't seek out other creatures.</summary>
    Wild,
    /// <summary>Chromatic dragons: cruel, they hunt people and those who guard them.</summary>
    Chromatic,
    /// <summary>Metallic dragons with no pact: proud, they keep to themselves but will not suffer the corrupted.</summary>
    Metallic,
    /// <summary>Dragons twisted by the Rift: they hunt every living thing.</summary>
    Corrupted,
    /// <summary>People and their protectors: pact dragons and the dragon knights who ride them.</summary>
    Alliance,
    /// <summary>A company of adventurers and the companions following them.</summary>
    Company,
}

public static class Allegiance
{
    /// <summary>
    /// Whether one side goes after another on sight. The corrupted attack everyone; the alliance (pact dragons and
    /// dragon knights) fends off chromatic and corrupted dragons; chromatic dragons hunt the alliance and the
    /// corrupted; unallied metallics drive off only the corrupted. Wild creatures and companies pick no fights
    /// with other creatures by themselves.
    /// </summary>
    public static bool Hostile(Side a, Side b) => a != b && (a, b) switch
    {
        (Side.Corrupted, not Side.Wild) => true,
        (Side.Alliance, Side.Chromatic or Side.Corrupted) => true,
        (Side.Chromatic, Side.Alliance or Side.Corrupted) => true,
        (Side.Metallic, Side.Corrupted) => true,
        _ => false,
    };
}
