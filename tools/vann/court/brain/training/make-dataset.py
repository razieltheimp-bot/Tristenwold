"""
Builds a fine-tuning dataset for Vann's local brain from his own play.

Every decision the local brain or Gemini makes is written down with what it earned
(local-claude/vann/<character>/train/YYYY-MM-DD.jsonl). This keeps the good ones:

  * every answer Gemini gave (the teacher), unless it plainly cost him (reward below -0.5)
  * the local brain's own answers that paid off (reward above 0)

and writes them in the chat format most fine-tuning tools take (one JSON object per line):

  {"messages": [{"role": "system", ...}, {"role": "user", ...}, {"role": "assistant", "content": "3"}]}

Run it from this folder:  python make-dataset.py
Output: dataset.jsonl next to this script, plus a short count of what went in.
"""

import glob
import json
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))
VANN = os.path.abspath(os.path.join(HERE, "..", ".."))  # local-claude/vann

SYSTEM = (
    "You are the quick mind of Vann, a villain playing the MMO Tristinwold. "
    "Your deeper mind (Gemini) gives you standing orders; you pick the next move from a numbered list. "
    "Answer with the number of your choice."
)


def main():
    rows, kept, by_who = 0, [], {"gemini": 0, "brain": 0}
    for path in sorted(glob.glob(os.path.join(VANN, "Vann-*", "train", "*.jsonl"))):
        with open(path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    r = json.loads(line)
                except json.JSONDecodeError:
                    continue
                rows += 1
                who, reward = r.get("by", ""), float(r.get("reward", 0))
                good = (who == "gemini" and reward > -0.5) or (who == "brain" and reward > 0)
                if not good or not r.get("prompt") or not r.get("answer"):
                    continue
                by_who[who] = by_who.get(who, 0) + 1
                kept.append({"messages": [
                    {"role": "system", "content": SYSTEM},
                    {"role": "user", "content": r["prompt"]},
                    {"role": "assistant", "content": str(r["answer"])},
                ]})
    random.shuffle(kept)
    out = os.path.join(HERE, "dataset.jsonl")
    with open(out, "w", encoding="utf-8") as f:
        for k in kept:
            f.write(json.dumps(k, ensure_ascii=False) + "\n")
    print(f"Read {rows} decisions; kept {len(kept)} ({by_who.get('gemini', 0)} from Gemini, {by_who.get('brain', 0)} from the local brain).")
    print(f"Wrote {out}")
    if len(kept) < 500:
        print("That's still a small set: a few hundred to a few thousand good examples is where a fine-tune starts to help.")


if __name__ == "__main__":
    main()
