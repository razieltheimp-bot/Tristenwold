# Teaching Vann's local brain from his own play

Vann now learns in three ways. The first two happen by themselves while he plays; the third is a step you run now and then.

## 1. Lessons checked by his score (automatic)
- Every 10 minutes he "dreams" and writes lessons (`Vann-<name>\LESSONS.md`), such as "in Tristinwold at high health, X has paid better than Y".
- Every move after that is a test: was it the lesson's move, and what did it earn compared with everything else in that moment?
- Once a lesson has been followed and not followed at least 6 times each, it is either **kept** (the more it pays, the stronger it gets) or **dropped as disproved**.
- `LESSONS.md` shows each lesson's record, for example `(followed 9x: +0.40 a move; otherwise -0.10)`.
- Habits are now grouped in level bands (1-4, 5-9, 10-14...), so a level-up no longer wipes what he's learned.

## 2. Teacher and pupil (automatic)
- A question too much for the small local brain goes **straight up to Gemini** (at most one a minute, only while that character has Gemini time). That covers:
  - a deviant in the choices,
  - another player nearby,
  - him being hurt with foes about,
  - the small brain failing to answer clearly twice running,
  - a kind of moment where the small brain's own picks have kept costing him.
- Gemini answers with its choice and a short reason. The choice is **carried out** (the simple brain lets it play out), and it's **kept** in `Vann-<name>\taught.jsonl`.
- With every question after that, the local brain is shown the three kept Gemini answers nearest its own moment, so it learns to answer like its teacher.

## 3. Fine-tuning the local brain (you run this now and then)
Every decision either brain makes is saved with what it earned in `Vann-<name>\train\`. When there are a few thousand:

1. **Build the dataset.** Run `python make-dataset.py` in this folder. It keeps Gemini's answers and the local brain's answers that paid off, and writes `dataset.jsonl`.
2. **Fine-tune `Qwen2.5-1.5B-Instruct` on it** with a LoRA. This laptop has no graphics card for training, so use a free GPU notebook such as Google Colab with the Unsloth or Hugging Face TRL fine-tuning notebook.
   - Upload `dataset.jsonl` (the chat "messages" format).
   - Train for 1–2 epochs.
   - Export the result as a **GGUF** file (Unsloth can save GGUF directly).
3. **Load it into Ollama on the laptop.** Make a file called `Modelfile` containing:
   ```
   FROM ./vann-brain.gguf
   ```
   then run:
   ```
   ollama create vann-brain -f Modelfile
   ```
4. **Point Vann at it.** In `brain\config.json` set `"model": "vann-brain"` and restart Vann.
5. **Check it's better.** Watch his `LESSONS.md` records, his score (level, crowns, apprentices) and how often questions still go up to Gemini. If the new brain does worse, set the model back to `qwen2.5:1.5b`.

### A note on bigger models
`qwen2.5:3b` is smarter, but on this laptop (an i3 with 8 GB of memory) it would roughly double the 12–15 second thinking time. The fine-tuned 1.5B model is the better route here.
