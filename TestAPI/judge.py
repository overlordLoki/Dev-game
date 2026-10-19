"""
LLM-as-judge for the dialogue tests, using the Claude CLI.

The game's dialogue comes from a small local model, and it paraphrases: "you sought a blade
from the smith" is a correct recall of "asked where to buy a sword". Keyword checks can't
tell that apart from a wrong answer that happens to share a word, so for tests about
*meaning* we ask a much stronger model to grade the reply instead.

Dev-only: the game itself never calls this. Needs `claude` on PATH (tests skip without it).

    JUDGE_MODEL    model name passed to `claude --model` (default: the CLI's default)
    JUDGE_TIMEOUT  seconds to wait for a verdict (default 120)
"""

import json
import os
import shutil
import subprocess
import tempfile
from dataclasses import dataclass

JUDGE_MODEL = os.getenv("JUDGE_MODEL")
JUDGE_TIMEOUT = float(os.getenv("JUDGE_TIMEOUT", "120"))


def available() -> bool:
    return shutil.which("claude") is not None


@dataclass
class Verdict:
    passed: bool
    reason: str


# The expectation is specific on purpose: "does it recall X" gets a reliable verdict,
# "is this a good reply" does not.
PROMPT = """You are grading one reply from a video game NPC, for an automated test.
Do not use any tools. Answer with ONLY a JSON object and no other text:
{{"pass": true or false, "reason": "one short sentence"}}

What the NPC knows:
{context}

What the player said:
{question}

The NPC's reply:
{reply}

Pass only if: {expectation}
Paraphrase and in-character, old-fashioned wording are fine. Fail if the reply dodges,
refuses, changes the subject, or states something that contradicts what the NPC knows."""


def judge(context: str, question: str, reply: str, expectation: str) -> Verdict:
    prompt = PROMPT.format(context=context, question=question, reply=reply, expectation=expectation)

    cmd = [shutil.which("claude"), "-p", "--output-format", "json"]
    if JUDGE_MODEL:
        cmd += ["--model", JUDGE_MODEL]

    # Prompt goes in on stdin (multi-line text survives that better than a command-line
    # argument on Windows). Run from the temp folder so the CLI doesn't pick up this
    # project's files as context.
    out = subprocess.run(
        cmd, input=prompt, capture_output=True, text=True, encoding="utf-8",
        timeout=JUDGE_TIMEOUT, cwd=tempfile.gettempdir(),
    )
    if out.returncode != 0:
        raise RuntimeError(f"claude CLI failed ({out.returncode}): {out.stderr.strip() or out.stdout.strip()}")

    # --output-format json wraps the answer: {"type": "result", "result": "<the model's text>", ...}
    text = json.loads(out.stdout)["result"]
    # Be forgiving if the verdict comes wrapped in prose or a code fence.
    start, end = text.find("{"), text.rfind("}")
    if start < 0 or end < start:
        raise RuntimeError(f"judge gave no JSON verdict: {text!r}")
    data = json.loads(text[start:end + 1])
    return Verdict(passed=bool(data["pass"]), reason=str(data.get("reason", "")))
