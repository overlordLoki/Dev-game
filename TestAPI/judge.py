"""
LLM-as-judge for the dialogue tests, using the Claude CLI.

The game's dialogue comes from a small local model, and it paraphrases: "you sought a blade
from the smith" is a correct recall of "asked where to buy a sword". Keyword checks can't
tell that apart from a wrong answer that happens to share a word, so for tests about
*meaning* we ask a much stronger model to grade the reply instead.

Dev-only: the game itself never calls this. Needs the claude CLI (tests skip without it).

    CLAUDE_CLI     full path to the claude executable (default: PATH, then ~/.local/bin)
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


def find_cli() -> str | None:
    """Where the claude CLI lives: CLAUDE_CLI if set, then PATH, then the native installer's
    default folder (~/.local/bin), which often isn't on PATH for venv or IDE shells."""
    override = os.getenv("CLAUDE_CLI")
    if override:
        return override if os.path.exists(override) else None
    on_path = shutil.which("claude")
    if on_path:
        return on_path
    for name in ("claude.exe", "claude"):
        default = os.path.join(os.path.expanduser("~"), ".local", "bin", name)
        if os.path.exists(default):
            return default
    return None


def unavailable_reason() -> str | None:
    """Why the judge can't run here, or None if it can."""
    if find_cli() is None:
        return "claude CLI not found (set CLAUDE_CLI to its full path)"
    # The CLI refuses to start inside another Claude Code session (e.g. a terminal opened
    # from the Claude app), and forcing past that check can crash both sessions.
    if os.getenv("CLAUDECODE"):
        return "running inside a Claude Code session - run pytest from a normal terminal"
    return None


def available() -> bool:
    return unavailable_reason() is None


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

    cmd = [find_cli(), "-p", "--output-format", "json"]
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
