VANN'S COURT (local only: git ignores local-claude, so none of this reaches GitHub or a release)

What it is
  Vann, the Rogue Magus, playing the shared world as a real player character, run by Gemini the way Ashgrip is. He
  wants out of Tristinwold: he levels up, gets to level 5 with 800 crowns, marches out along his "frontier" past the
  known cities and founds a colony there, then keeps reaching further (each new colony further out), takes towns through
  their councils and by raids for the place itself, buys ships and puts apprentices in command of them, and makes
  trouble for every other company on the server. The server's own Vann (the Saved Man who whispers) carries on as before.

His court
  He has permission to raise more player characters, each its own account, named Vann and the next number in Spanish:
  Vannuno, Vanndos, Vanntres, Vanncuatro, Vanncinco...
  - The first three are his lieutenants, always Vannuno the Paladin, Vanndos the Martial Artist and Vanntres the
    Wizard: the classes with no limit on apprentices. They recruit every youth they can afford, ride to Vann's colony
    once he has one, found their chapel, monastery and spire beside it, and run the colony and the towns with him.
  - Past the three, Gemini picks when to raise an agent and its class. Agents start weak in Tristinwold, pass for
    ordinary new players, and work on the other players in the shared chat and with hidden notes: steering them apart,
    spreading lies, luring them into trouble. They never fight players, and none of the court fights each other.
  Every one of them wears the same look (the portrait here): older, wild white hair, pale shirt, olive trousers.

Running it
  Double-click "Start Vann" on the desktop (or: powershell -ExecutionPolicy Bypass -File local-claude\vann\run-vann.ps1).
  Vann plays in a window; the rest play with no window, to spare the laptop. Close Vann's window and the whole court
  stops. Add -Offline to try Vann alone on this PC. F8 in Vann's window takes the controls, as with Ashgrip.
  Settings (how many play at once, how many he may raise, the look, the key): vann-settings.txt.

Files
  court.json          the roster, Vann's decree, his frontier and the court's seat (Vann writes it)
  status-<name>.json  where each one is, their level, crowns, apprentices, colonies and towns ruled
  <name>\             each one's orders.json, notebook, Gemini count, and last-run.log (lines starting [vann] and [claude])
