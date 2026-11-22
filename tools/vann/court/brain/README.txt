VANN'S BRAIN (local only)

A small language model on this laptop, run by Ollama (the same Ollama MoJo and Jarvis use for their embeddings),
sitting between Gemini and the simple brain:
  Gemini      plans: the standing orders, the to-do list, the decree (within the court's Gemini hours)
  this brain  is asked for the next move every 20 seconds or so, by those orders, when nothing urgent is going on;
              it thinks in the background while the simple brain keeps him moving, and its answer is carried out
              when it comes (if that move is still there to make). No answer in 5 minutes: Gemini takes the question.
  simple      everything else: fights, danger, healing, travel already under way, conversations and deals

It costs nothing and never leaves the laptop. If Ollama isn't running or the model isn't there, the bot just plays
on without it (the simple brain decides).

  config.json    the model (qwen2.5:1.5b), how often it's asked, how long to wait for it
  persona.txt    who it is and how it answers
  knowledge\     what it knows: MMORPG play in general, Tristinwold, and the court (add your own .md files here)

The launcher starts Ollama if it isn't running and pulls the model the first time (about 1 GB, once).
Lines starting [brain] in each character's last-run.log show what it picked.
