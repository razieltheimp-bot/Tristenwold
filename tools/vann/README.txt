VANN'S COURT ON THE SERVER

What it is
  Vann (and the court he raises) playing the shared world from this machine, with no windows: the game runs headless,
  driven by its own code. It starts with the machine and restarts itself, so nobody needs to start or stop it.

What's public and what isn't
  - Vann's game build (Tristinwold-Vann-Windows.zip on the GitHub release) and these scripts (tools/vann in the repo)
    are public: they hold no keys and no court memory.
  - This package (Vann-Server.zip: keys.sealed and the court's memory in court\), the passphrase, keys.dat and any key
    list are private: never put them on GitHub, in a release, or in a chat or channel others can read.

Before installing
  - Only one copy of the court may run at a time: two copies on the same Telegram bot fight over its messages. The
    owner's own copy must be stopped first.
  - Make a standard (non-administrator) Windows account for Vann, e.g. in an administrator prompt:
        net user vann * /add
    (it asks for a password; leave the account out of Administrators). The court then can only change its own folder,
    not the machine. SYSTEM still works if you'd rather (just press Enter at the account question).

Installing (once, as an administrator)
  1. Unzip the whole package somewhere that stays put, e.g. C:\Vann (the path must not change afterwards).
  2. Double-click "Install Vann.bat" and say yes to the administrator prompt. It:
     - asks which account Vann runs as (e.g. vann), and that account's Windows password (the task keeps it so it can
       start Vann at boot with nobody logged on). It then locks the folder: administrators and SYSTEM can do anything,
       Vann's account can change the court, the game and its logs, but only read the scripts and keys.dat;
     - finds Ollama (Vann's local brain), or offers to install it with winget into Program Files; its model (~1 GB)
       downloads the first time Vann plays, into ollama-models here;
     - asks for the passphrase that opens keys.sealed (the owner gives it to you separately), then offers to replace
       each key: just press Enter at each one, they're all already in there. (Or, with no keys.sealed, paste each key
       from the owner's key list when asked.)
     - makes the scheduled task "Vann's court" (runs as that account at every start-up, restarts itself) and starts it.
       If it says the account isn't allowed to log on as a batch job, a policy on the machine forbids it: add the
       account under secpol.msc > Local Policies > User Rights Assignment > Log on as a batch job.

Keys
  Gemini key, Telegram bot token, the court's account password, the world's server password, and a
  top-brain key that's normally blank. They're kept in keys.dat, encrypted by Windows to this one machine; only
  Vann's account (read-only), SYSTEM and administrators can read the file. Copied anywhere else it can't be opened.
  They reach the games in environment variables, never on a command line. "Set Keys.bat" changes them.

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

Changing the account
  Run "Install Vann.bat" again and give the new account (or SYSTEM). The keys stay as they are.

Uninstalling
  "Stop Vann.bat", then in Task Scheduler delete "Vann's court", then delete this folder.
