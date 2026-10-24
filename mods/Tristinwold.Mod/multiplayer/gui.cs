using Tristinwold.Shared;

namespace Tristinwold.Mods.Multiplayer;

public unsafe sealed partial class MultiplayerModule {
    private bool   _open;
    private string _chat_input = "";
    private string _drop_input = "";

    /// <summary>F10: the shared world. Contract board, other companies, dead drops and chat.</summary>
    public override void render_imgui() {
        if (ImGui.IsKeyPressed(ImGuiKey.F10)) _open = !_open;
        if (!_open) return;

        ImGui.SetNextWindowSize(new Vector2(720, 520), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Tristinwold - Shared World###tristinwold.mp", ref _open)) {
            ImGui.End();
            return;
        }

        render_connection();
        ImGui.Separator();

        if (ImGui.BeginTabBar("##mp.tabs")) {
            if (ImGui.BeginTabItem("Contract Board")) { render_board();  ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Companies"))      { render_groups(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Dead Drops"))     { render_drops();  ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Chronicle"))      { render_log();    ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        ImGui.End();
    }

    private void render_connection() {
        ClientStatus status = _client?.Status ?? ClientStatus.Disconnected;
        string who = _nemesis?.player_name ?? "?";
        ImGui.Text($"{who} | {status}{(_client?.LastError is { } err && status != ClientStatus.Connected ? $" ({err})" : "")}");
        lock (_lock) {
            if (_campaign.Length > 0) { ImGui.SameLine(); ImGui.TextDisabled($"| {_campaign}"); }
            if (_keystone_note is not null) ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), _keystone_note);
        }
        ImGui.SameLine(ImGui.GetWindowWidth() - 120);
        if (_client is null) {
            if (ImGui.Button("Connect")) connect();
        }
        else if (ImGui.Button("Disconnect")) {
            disconnect();
        }
        if (_client is null) ImGui.TextDisabled("Set the server address and your player name in the Fahrenheit mod settings.");
    }

    private void render_board() {
        List<PublicContract> board;
        lock (_lock) board = [.. _board];
        string me = _nemesis?.player_name ?? "";

        if (!ImGui.BeginTable("##board", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("Contract");
        ImGui.TableSetupColumn("Posted by");
        ImGui.TableSetupColumn("Pay");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();
        foreach (PublicContract c in board) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (c.Status == ContractStatus.Done) ImGui.TextDisabled(c.Title); else ImGui.TextWrapped(c.Title);
            if (c.Where is not null) ImGui.TextDisabled(c.Where);
            ImGui.TableNextColumn(); ImGui.Text(c.PostedBy);
            ImGui.TableNextColumn(); ImGui.Text($"{c.Pay} gp");
            ImGui.TableNextColumn();
            ImGui.PushID(c.Id);
            switch (c.Status) {
                case ContractStatus.Open:
                    if (ImGui.Button("Take it") && connected) send(new ClaimContract(c.Id));
                    break;
                case ContractStatus.Claimed when c.ClaimedBy == me:
                    if (ImGui.Button("Abandon")) send(new AbandonContract(c.Id));
                    ImGui.SameLine();
                    if (ImGui.Button("Done")) send(new FinishContract(c.Id));
                    break;
                case ContractStatus.Claimed:
                    ImGui.TextDisabled($"{c.ClaimedBy}'s");
                    break;
                default:
                    ImGui.TextDisabled("Done");
                    break;
            }
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void render_groups() {
        List<GroupView> groups;
        lock (_lock) groups = [.. _groups];
        foreach (GroupView g in groups) {
            Vector4 colour = g.Online ? new Vector4(0.6f, 1f, 0.6f, 1f) : new Vector4(0.6f, 0.6f, 0.6f, 1f);
            ImGui.TextColored(colour, $"{g.Player}'s company");
            ImGui.SameLine();
            // Vann keeps the companies apart: you know roughly where they are, never exactly.
            ImGui.TextDisabled(g.Online ? $"- {(g.Area.Length > 0 ? g.Area : "somewhere in the city")}" : "- resting");
            ImGui.Indent();
            ImGui.Text(g.Party.Count > 0 ? string.Join(", ", g.Party) : "(party unknown)");
            ImGui.Unindent();
        }
    }

    private void render_drops() {
        string area = current_area ?? "?";
        ImGui.Text($"You are in: {area}");
        List<DeadDropView> drops;
        lock (_lock) drops = _drops_at == area ? [.. _drops] : [];
        if (drops.Count == 0) ImGui.TextDisabled("No notes hidden here that reached you.");
        foreach (DeadDropView d in drops) ImGui.TextWrapped($"Day {d.Day}, from {d.From}: {d.Text}");

        ImGui.SeparatorText("Leave a note here");
        ImGui.InputTextMultiline("##drop", ref _drop_input, 600, new Vector2(-1, 80));
        if (ImGui.Button("Hide the note") && connected && _drop_input.Trim().Length > 0 && current_area is not null) {
            send(new LeaveDrop(current_area, _drop_input));
            _drop_input = "";
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Some notes never arrive. Someone is reading them.");
    }

    private void render_log() {
        if (ImGui.BeginChild("##log", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - 4))) {
            lock (_lock) foreach (string line in _log) ImGui.TextWrapped(line);
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1f);
        }
        ImGui.EndChild();
        ImGui.SetNextItemWidth(-70);
        bool enter = ImGui.InputText("##chat", ref _chat_input, 400, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Say") || enter) && connected && _chat_input.Trim().Length > 0) {
            send(new Say(_chat_input));
            _chat_input = "";
        }
    }
}
