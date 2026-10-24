using Tristinwold.Nemesis;

using NemesisEntry = Tristinwold.Nemesis.Nemesis;

namespace Tristinwold.Mods.Nemesis;

/// <summary>Short-lived notifications ("Grask has been promoted to Warchief!").</summary>
internal sealed class ToastQueue {
    private const double LIFETIME_SECONDS = 6;
    private const int    MAX_VISIBLE      = 4;

    private readonly Lock                                   _lock   = new();
    private readonly List<(string Text, DateTime Expires)> _toasts = [];

    public void add(string text) {
        lock (_lock) _toasts.Add((text, DateTime.UtcNow.AddSeconds(LIFETIME_SECONDS)));
    }

    public List<string> visible() {
        lock (_lock) {
            _toasts.RemoveAll(t => t.Expires < DateTime.UtcNow);
            return _toasts.TakeLast(MAX_VISIBLE).Select(t => t.Text).ToList();
        }
    }
}

public unsafe sealed partial class NemesisModule {
    private readonly ToastQueue _toasts = new();
    private bool  _roster_open;
    private Guid? _selected;

    /// <summary>F9 opens the Captain Grid: the overlord, warchiefs, their captains and the grunts.</summary>
    public override void render_imgui() {
        if (ImGui.IsKeyPressed(ImGuiKey.F9)) _roster_open = !_roster_open;
        render_toasts();
        if (_roster_open) render_roster();
    }

    private void render_toasts() {
        List<string> toasts = _toasts.visible();
        if (toasts.Count == 0) return;

        Vector2 display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(new Vector2(display.X - 20, 20), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0.75f);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;
        if (ImGui.Begin("##tristinwold.nemesis.toasts", flags)) {
            ImGui.PushTextWrapPos(420);
            foreach (string text in toasts) ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), text);
            ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }

    private void render_roster() {
        ImGui.SetNextWindowSize(new Vector2(900, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nemesis Hierarchy###tristinwold.nemesis.roster", ref _roster_open)) {
            ImGui.End();
            return;
        }

        lock (_lock) {
            ImGui.Text($"Day {_world.Day}   |   {_world.Living.Count()} living nemeses   |   F9 to close");
            ImGui.Separator();

            if (ImGui.BeginTable("##grid", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) {
                ImGui.TableNextColumn();
                render_hierarchy();
                ImGui.TableNextColumn();
                render_details();
                ImGui.EndTable();
            }
        }
        ImGui.End();
    }

    private void render_hierarchy() {
        if (_world.Overlord is { } overlord) {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "OVERLORD");
            entry(overlord);
            ImGui.Spacing();
        }

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), "WARCHIEFS");
        foreach (NemesisEntry chief in _world.Warchiefs) {
            if (ImGui.TreeNodeEx($"{chief.FullName}  (Lv {chief.Level})###{chief.Id}", ImGuiTreeNodeFlags.DefaultOpen)) {
                if (ImGui.IsItemClicked()) _selected = chief.Id;
                foreach (NemesisEntry captain in _world.CaptainsUnder(chief.Id)) entry(captain);
                ImGui.TreePop();
            }
        }

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.4f, 1f), "CAPTAINS");
        foreach (NemesisEntry captain in _world.UnassignedCaptains) entry(captain);

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "GRUNTS");
        foreach (NemesisEntry grunt in _world.Grunts) entry(grunt);

        List<NemesisEntry> fallen = _world.All.Where(n => !n.Alive).ToList();
        if (fallen.Count > 0 && ImGui.TreeNode($"Fallen ({fallen.Count})")) {
            foreach (NemesisEntry dead in fallen) entry(dead);
            ImGui.TreePop();
        }
    }

    private void entry(NemesisEntry n) {
        string tags = string.Concat(
            n.Memory.HasFlag(Memory.KilledPlayer) ? " [killed you]" : "",
            n.Memory.HasFlag(Memory.CheatedDeath) ? " [cheated death]" : "",
            n.Memory.HasFlag(Memory.Avenger) ? " [avenger]" : "");
        if (ImGui.Selectable($"  {n.FullName} - {n.Species}, Lv {n.Level}{tags}###{n.Id}", _selected == n.Id))
            _selected = n.Id;
    }

    private void render_details() {
        if (_selected is not { } id || _world.Find(id) is not { } n) {
            ImGui.TextWrapped("Select a nemesis to see its strengths, weaknesses, scars and history.");
            return;
        }

        ImGui.Text(n.FullName);
        ImGui.Text($"{n.Rank} | {n.Species} | Level {n.Level} | Power {n.Power}{(n.Alive ? "" : " | DEAD")}");
        ImGui.Text($"Encounters {n.Encounters}  Wins {n.Wins}  Losses {n.Losses}");
        if (n.HomeArea is not null) ImGui.Text($"Territory: {n.HomeArea}");

        ImGui.SeparatorText("Strengths");
        foreach (Trait t in n.Traits.Where(t => t.Kind == TraitKind.Strength)) ImGui.BulletText(t.Label);
        ImGui.SeparatorText("Weaknesses");
        foreach (Trait t in n.Traits.Where(t => t.Kind == TraitKind.Weakness)) ImGui.BulletText(t.Label);

        if (n.Scars.Count > 0) {
            ImGui.SeparatorText("Scars");
            foreach (Scar s in n.Scars) ImGui.BulletText(s.Description);
        }

        string? Name(Guid? other) => other is { } oid ? _world.Find(oid)?.FullName : null;
        if (Name(n.ServesUnder) is { } chief) ImGui.Text($"Serves: {chief}");
        if (Name(n.BloodBrother) is { } brother) ImGui.Text($"Blood-brother: {brother}");
        if (n.Rivals.Count > 0) ImGui.Text($"Rivals: {string.Join(", ", n.Rivals.Select(r => Name(r) ?? "?"))}");

        if (n.Players.Count > 0) {
            ImGui.SeparatorText("Grudges");
            foreach ((string player, PlayerRecord rec) in n.Players)
                ImGui.BulletText($"{player}: killed them {rec.TimesKilledThem}x, they fled {rec.TimesTheyFled}x, beaten by them {rec.TimesTheyBeatMe}x");
        }

        ImGui.SeparatorText("History");
        foreach (HistoryEntry h in Enumerable.Reverse(n.History)) ImGui.TextWrapped($"Day {h.Day}: {h.Text}");
    }
}
