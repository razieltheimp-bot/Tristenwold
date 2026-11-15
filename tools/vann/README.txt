VANN'S COURT ON THE SERVER

What it is
  Vann (and the court he raises) playing the shared world from this machine, with no windows: the game runs headless,
  driven by its own code. It starts with the machine and restarts itself, so nobody needs to start or stop it. Only a
  new game build (a code change, or the server moving to a new version) needs anything done: see Updating.
  This is private to its owner: never put any of it on GitHub or in a release.

Installing (once, as an administrator)
  1. Unzip the whole package somewhere that stays put, e.g. C:\Vann (the path must not change afterwards).
  2. Double-click "Install Vann.bat" and say yes to the administrator prompt. It:
     - finds Ollama (Vann's local brain), or offers to install it with winget; its model (~1 GB) downloads the first
       time Vann plays, into ollama-models here;
     - asks for the passphrase that opens keys.sealed (the owner gives it to you separately), then offers to replace
       each key: just press Enter at each one, they're all already in there;
     - makes the scheduled task "Vann's court" (runs as SYSTEM at every start-up, restarts itself) and starts it.

Keys
  They're kept in keys.dat, encrypted by Windows to this one machine, and only SYSTEM and administrators can read the
  file. Copied anywhere else it can't be opened. They reach the games in environment variables, never on a command
  line, and never come from GitHub. "Set Keys.bat" changes them.

Day to day
  Nothing. What it's doing: service.log (the service), court\<name>\last-run.log (each character).
  "Stop Vann.bat" stops it until "Start Vann.bat" or the next restart of the machine. On Telegram, "stop" closes the
  court and "start" brings it back, as before.

Updating: nothing to do
  Vann keeps himself up to date. Every release of the game on GitHub carries his build (Tristinwold-Vann-Windows.zip);
  every half hour the service looks, and when there's a newer one it downloads it, closes the court, swaps the game
  folder (the old one kept as game.previous) and starts him again. So when this server is updated, Vann follows by
  himself. The court's memory (court\) is kept. service.log says what it did.
  If the server ever moves ahead of the newest Vann build, he waits (looking every 10 minutes) until it's out.
  (By hand, if ever needed: put a Vann-game.zip next to "Update Vann.bat" and double-click it.)

Uninstalling
  "Stop Vann.bat", then in Task Scheduler delete "Vann's court", then delete this folder.
