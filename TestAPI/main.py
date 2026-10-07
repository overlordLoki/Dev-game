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
    "with no stage directions, no speaker prefixes and no quotation marks."
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


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", reload=True)
