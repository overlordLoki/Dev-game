"""
Middle man between Ashenveil and a local Ollama server.

The game posts who is talking and where; this builds the prompt, asks Ollama for
JSON, and hands back lines shaped like Conversation.Lines (one chat box each).

Run:  fastapi dev main.py        (docs at http://127.0.0.1:8000/docs)
"""

import json
import os

import fastapi
import httpx
from pydantic import BaseModel, Field

OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "gemma4:e4b")
# First call after Ollama starts has to load the model, which can take a while.
OLLAMA_TIMEOUT = float(os.getenv("OLLAMA_TIMEOUT", "120"))

app = fastapi.FastAPI(title="Ashenveil dialogue API")


# ------------------------------------------------------------------- payload

class Participant(BaseModel):
    name: str
    # Free text: job, mood, what they know. Optional, so the game can start with just names.
    persona: str | None = None
    # False for anyone who is in the conversation but shouldn't be given lines (the player).
    speaks: bool = True
    # Facts this participant remembers from earlier conversations ("The traveller's name is
    # Loki"). Rendered as things they know, separate from what was said in this one.
    memories: list[str] = []


class ConversationRequest(BaseModel):
    # Same order as Conversation.Participants: the first one started it.
    participants: list[Participant] = Field(min_length=1)
    location: str | None = None
    topic: str | None = None
    # Earlier lines, if the game wants the NPC to remember what was already said.
    history: list[str] = []
    max_lines: int = Field(default=4, ge=1, le=12)
    # Rough cap so a line fits in one chat box.
    max_line_chars: int = Field(default=120, ge=20, le=400)
    # Anything else the game wants the model to know. Goes into the prompt as-is.
    extra: dict[str, str] = {}


class Turn(BaseModel):
    speaker: str
    text: str


class ConversationResponse(BaseModel):
    # Drops straight into Conversation.Lines.
    lines: list[str]
    # Same lines with who said each one, for when the chat box shows a name/portrait.
    turns: list[Turn]
    model: str


# -------------------------------------------------------------------- prompt

SYSTEM_PROMPT = (
    "You write short in-game dialogue for Ashenveil, a top-down fantasy RPG. "
    "Stay in character, keep it grounded in the setting, and never mention being an AI. "
    "Each line is shown in its own small text box, so keep lines short and spoken, "
    "with no stage directions, no speaker prefixes and no quotation marks. "
    "If a character remembers facts about who they are speaking to, they use them "
    "naturally and never pretend not to know something they remember."
)


def build_prompt(req: ConversationRequest) -> str:
    speakers = [p for p in req.participants if p.speaks]
    parts = []

    if req.location:
        parts.append(f"Location: {req.location}")

    parts.append("Present:")
    for p in req.participants:
        role = "" if p.speaks else " (listening, give them no lines)"
        persona = f" - {p.persona}" if p.persona else ""
        parts.append(f"  {p.name}{persona}{role}")

    # What each speaker already knows, kept apart from "Already said" so the model treats it
    # as reliable knowledge rather than lines drifting past in the history.
    for p in speakers:
        if p.memories:
            parts.append(f"What {p.name} remembers from before:")
            parts.extend(f"  - {m}" for m in p.memories)

    if req.topic:
        parts.append(f"Topic: {req.topic}")
    for key, value in req.extra.items():
        parts.append(f"{key}: {value}")
    if req.history:
        parts.append("Already said:")
        parts.extend(f"  {line}" for line in req.history)

    names = ", ".join(p.name for p in speakers)
    parts.append(
        f"Write up to {req.max_lines} lines, each under {req.max_line_chars} characters. "
        f"Only these may speak: {names}."
    )
    return "\n".join(parts)


def turns_schema(speaker_names: list[str]) -> dict:
    """JSON schema Ollama is held to, so the reply always parses."""
    return {
        "type": "object",
        "properties": {
            "turns": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "speaker": {"type": "string", "enum": speaker_names},
                        "text": {"type": "string"},
                    },
                    "required": ["speaker", "text"],
                },
            }
        },
        "required": ["turns"],
    }


# ----------------------------------------------------------------- endpoints

@app.get("/")
async def read_root():
    return {"service": "Ashenveil dialogue API", "model": OLLAMA_MODEL, "docs": "/docs"}


@app.get("/health")
async def health():
    """Is Ollama up, and does it have the model we're going to ask for?"""
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            res = await client.get(f"{OLLAMA_URL}/api/tags")
            res.raise_for_status()
    except httpx.HTTPError as e:
        raise fastapi.HTTPException(503, f"Ollama not reachable at {OLLAMA_URL}: {e}")

    models = [m["name"] for m in res.json().get("models", [])]
    return {"ollama": "ok", "model": OLLAMA_MODEL, "model_installed": OLLAMA_MODEL in models, "models": models}


@app.post("/conversation", response_model=ConversationResponse)
async def conversation(req: ConversationRequest):
    speakers = [p.name for p in req.participants if p.speaks]
    if not speakers:
        raise fastapi.HTTPException(422, "At least one participant must have speaks=true")

    body = {
        "model": OLLAMA_MODEL,
        "stream": False,
        "think": False,
        "format": turns_schema(speakers),
        "messages": [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": build_prompt(req)},
        ],
    }

    try:
        async with httpx.AsyncClient(timeout=OLLAMA_TIMEOUT) as client:
            res = await client.post(f"{OLLAMA_URL}/api/chat", json=body)
            res.raise_for_status()
    except httpx.HTTPStatusError as e:
        raise fastapi.HTTPException(502, f"Ollama error: {e.response.text}")
    except httpx.HTTPError as e:
        raise fastapi.HTTPException(503, f"Ollama not reachable at {OLLAMA_URL}: {e}")

    try:
        raw = json.loads(res.json()["message"]["content"])
        turns = [Turn(**t) for t in raw["turns"]]
    except (KeyError, ValueError, TypeError) as e:
        raise fastapi.HTTPException(502, f"Ollama returned something unusable: {e}")

    # The model is asked for max_lines but not forced to it; blank lines would show as empty boxes.
    turns = [t for t in turns if t.text.strip()][: req.max_lines]
    if not turns:
        raise fastapi.HTTPException(502, "Ollama returned no lines")

    return ConversationResponse(lines=[t.text.strip() for t in turns], turns=turns, model=OLLAMA_MODEL)


# --------------------------------------------------------------- NPC decision
# Produced once, when a conversation ends: what the NPC remembers and does next.
# Mirrors Ashenveil's NpcDecisionDto. The enums keep the model to tasks, places and
# behaviours the game actually knows about.

# The task vocabulary, single-sourced: feeds BOTH the schema enum (the hard limit) and
# the prompt (so the model knows what each task means). C#'s ApplyDecision must know the
# same type names.
TASK_TYPES = {
    "go_to": "walk to a named place (needs a target place)",
    "wait": "stand still for a while (needs seconds)",
}
BEHAVIOURS = ["resume", "idle", "do_tasks"]


class DecisionRequest(BaseModel):
    npc_name: str
    persona: str | None = None
    location: str | None = None
    # The conversation so far, one "Name: line" per entry.
    transcript: list[str] = []
    # The places a go_to may target - the current level's points of interest.
    poi_names: list[str] = []
    max_tasks: int = Field(default=4, ge=0, le=8)
    max_memories: int = Field(default=5, ge=0, le=10)


class TaskSpec(BaseModel):
    type: str
    target: str | None = None
    seconds: float = 0.0


class DecisionResponse(BaseModel):
    # Drops straight into NpcDecisionDto (the game ignores the extra `model` field).
    summary: str
    memories: list[str] = []
    behaviour: str = "resume"
    clear_current_tasks: bool = False
    tasks: list[TaskSpec] = []
    model: str


DECISION_SYSTEM_PROMPT = (
    "You are the mind of an NPC in Ashenveil, a top-down fantasy RPG, reflecting after a "
    "conversation. Decide what the character remembers and what they do next. Stay in "
    "character and grounded in the setting. Only use the tasks, places and behaviours you "
    'are given. If nothing has changed, choose behaviour "resume" and give no tasks.'
)


def decision_schema(poi_names: list[str]) -> dict:
    """JSON schema Ollama is held to. The enums stop the model inventing tasks or places."""
    # A go_to target is constrained to real POIs when the game sends them.
    target = {"type": "string", "enum": poi_names} if poi_names else {"type": "string"}
    return {
        "type": "object",
        "properties": {
            "summary": {"type": "string"},
            "memories": {"type": "array", "items": {"type": "string"}},
            "behaviour": {"type": "string", "enum": BEHAVIOURS},
            "clear_current_tasks": {"type": "boolean"},
            "tasks": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "type": {"type": "string", "enum": list(TASK_TYPES)},
                        "target": target,
                        "seconds": {"type": "number"},
                    },
                    "required": ["type"],
                },
            },
        },
        "required": ["summary", "behaviour"],
    }


def build_decision_prompt(req: DecisionRequest) -> str:
    parts = [f"You are {req.npc_name}."]
    if req.persona:
        parts.append(req.persona)
    if req.location:
        parts.append(f"Location: {req.location}")

    parts.append("\nThe conversation that just happened:")
    if req.transcript:
        parts.extend(f"  {line}" for line in req.transcript)
    else:
        parts.append("  (nothing was said)")

    parts.append("\nTasks you can give yourself:")
    for name, desc in TASK_TYPES.items():
        parts.append(f"  {name}: {desc}")
    if req.poi_names:
        parts.append("Places you can go: " + ", ".join(req.poi_names))

    parts.append(
        f"\nSummarise the conversation in one sentence. Then extract the concrete facts worth "
        "remembering long-term - names, promises, requests, and anything notable about who you "
        f"spoke to - as up to {req.max_memories} short standalone statements in `memories` "
        '(for example "The traveller\'s name is Loki"). Put each fact in `memories`, not only '
        "in the summary. Only record things that are NEW to you: what you learned about the "
        "others or what happened. Never record your own background, your persona, or what you "
        "said in your greeting - you already know those. "
        "Choose a behaviour: resume (carry on as before), idle (stand still), or do_tasks "
        f"(then give up to {req.max_tasks} tasks). Set clear_current_tasks true only to drop "
        "what you were already doing."
    )
    return "\n".join(parts)


@app.post("/decision", response_model=DecisionResponse)
async def decision(req: DecisionRequest):
    body = {
        "model": OLLAMA_MODEL,
        "stream": False,
        "think": False,
        "format": decision_schema(req.poi_names),
        "messages": [
            {"role": "system", "content": DECISION_SYSTEM_PROMPT},
            {"role": "user", "content": build_decision_prompt(req)},
        ],
    }

    try:
        async with httpx.AsyncClient(timeout=OLLAMA_TIMEOUT) as client:
            res = await client.post(f"{OLLAMA_URL}/api/chat", json=body)
            res.raise_for_status()
    except httpx.HTTPStatusError as e:
        raise fastapi.HTTPException(502, f"Ollama error: {e.response.text}")
    except httpx.HTTPError as e:
        raise fastapi.HTTPException(503, f"Ollama not reachable at {OLLAMA_URL}: {e}")

    try:
        raw = json.loads(res.json()["message"]["content"])
    except (KeyError, ValueError, TypeError) as e:
        raise fastapi.HTTPException(502, f"Ollama returned something unusable: {e}")

    # The schema guarantees the shape; we still trim to the caps the game asked for and
    # drop blank memories (the game does this too, but cheap to do here as well).
    memories = [m.strip() for m in raw.get("memories", []) if m.strip()][: req.max_memories]
    tasks = [TaskSpec(**t) for t in raw.get("tasks", [])][: req.max_tasks]
    return DecisionResponse(
        summary=raw.get("summary", "").strip(),
        memories=memories,
        behaviour=raw.get("behaviour", "resume"),
        clear_current_tasks=bool(raw.get("clear_current_tasks", False)),
        tasks=tasks,
        model=OLLAMA_MODEL,
    )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", reload=True)
