"""
Tests for the Ashenveil dialogue API.

Two kinds:
  * Unit tests (schema + prompt building) - fast, no Ollama.
  * Integration tests that hit /conversation and /decision for real, so they need
    Ollama running with the model. They skip automatically if it isn't reachable.

Run:  pytest            (from the TestAPI folder, in the venv)
      pytest -m unit    (just the fast ones)
"""

import httpx
import pytest
from fastapi.testclient import TestClient

import main
from main import (
    BEHAVIOURS,
    TASK_TYPES,
    ConversationRequest,
    DecisionRequest,
    app,
    build_decision_prompt,
    build_prompt,
    decision_schema,
)

client = TestClient(app)


def ollama_up() -> bool:
    try:
        return httpx.get(f"{main.OLLAMA_URL}/api/tags", timeout=3).status_code == 200
    except httpx.HTTPError:
        return False


needs_ollama = pytest.mark.skipif(not ollama_up(), reason="Ollama not reachable")


# ----------------------------------------------------------------- unit: schema

def test_schema_constrains_behaviour_and_task_type():
    props = decision_schema(["well", "gate"])["properties"]
    assert props["behaviour"]["enum"] == BEHAVIOURS

    task_props = props["tasks"]["items"]["properties"]
    assert task_props["type"]["enum"] == list(TASK_TYPES)
    assert task_props["target"]["enum"] == ["well", "gate"]   # go_to limited to real POIs


def test_schema_target_is_free_text_when_no_pois_given():
    target = decision_schema([])["properties"]["tasks"]["items"]["properties"]["target"]
    assert "enum" not in target


# ----------------------------------------------------------------- unit: prompt

def test_prompt_mentions_npc_persona_transcript_and_places():
    req = DecisionRequest(
        npc_name="Sir Lancelot",
        persona="A weary knight.",
        location="Medows",
        transcript=["Traveller: my name is Aldric"],
        poi_names=["well", "gate"],
    )
    prompt = build_decision_prompt(req)

    assert "Sir Lancelot" in prompt
    assert "A weary knight." in prompt
    assert "my name is Aldric" in prompt
    assert "well" in prompt and "gate" in prompt
    for task_name in TASK_TYPES:
        assert task_name in prompt


# ------------------------------------------------------- integration: endpoints

def test_prompt_frames_memories_as_knowledge_not_history():
    req = ConversationRequest(
        participants=[
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot", "memories": ["The traveller's name is Loki"]},
        ],
        history=["Traveller: what is my name?"],
    )
    prompt = build_prompt(req)

    assert "What Sir Lancelot remembers from before:" in prompt
    # The memory sits under its own heading, before (not inside) the "Already said" block.
    assert prompt.index("The traveller's name is Loki") < prompt.index("Already said:")


def test_listeners_memories_are_not_rendered():
    req = ConversationRequest(participants=[
        {"name": "Traveller", "speaks": False, "memories": ["should not appear"]},
        {"name": "Sir Lancelot"},
    ])
    assert "should not appear" not in build_prompt(req)


@needs_ollama
def test_health_reports_the_model_is_installed():
    body = client.get("/health").json()
    assert body["model_installed"] is True


@needs_ollama
def test_conversation_returns_at_least_one_line():
    r = client.post("/conversation", json={
        "participants": [
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot", "persona": "A gruff knight."},
        ],
        "location": "Medows",
        "history": ["Sir Lancelot: Halt, who goes there?"],
        "max_lines": 1,
    })
    assert r.status_code == 200
    body = r.json()
    assert len(body["lines"]) >= 1
    assert body["turns"][0]["speaker"] == "Sir Lancelot"   # the only speaker


@needs_ollama
def test_decision_is_well_formed_and_respects_the_enums():
    r = client.post("/decision", json={
        "npc_name": "Sir Lancelot",
        "persona": "A weary knight.",
        "location": "Medows",
        "transcript": ["Traveller: fetch water from the well", "Sir Lancelot: Aye."],
        "poi_names": ["well", "Knight_spawn"],
    })
    assert r.status_code == 200
    d = r.json()

    assert d["behaviour"] in BEHAVIOURS
    for task in d["tasks"]:
        assert task["type"] in TASK_TYPES
        if task["type"] == "go_to":
            assert task["target"] in ["well", "Knight_spawn"]   # can't invent a place


@needs_ollama
def test_decision_remembers_a_name_said_in_the_chat():
    """The feature under scrutiny: a name stated in the conversation should land in the
    summary or memories, so the next conversation can inject it back."""
    r = client.post("/decision", json={
        "npc_name": "Sir Lancelot",
        "persona": "A knight who greets travellers warmly.",
        "location": "Medows",
        "transcript": [
            "Traveller: Hello, my name is Aldric.",
            "Sir Lancelot: Well met, Aldric.",
        ],
        "poi_names": [],
    })
    assert r.status_code == 200
    d = r.json()

    haystack = (d["summary"] + " " + " ".join(d["memories"])).lower()
    assert "aldric" in haystack, \
        f"name not remembered: summary={d['summary']!r} memories={d['memories']}"



@needs_ollama
def test_npc_uses_a_remembered_name_when_asked():
    """The in-game bug as a test: the knight remembers the name from an earlier
    conversation and the player asks for it. He should answer, not deflect."""
    r = client.post("/conversation", json={
        "participants": [
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot",
             "persona": "A noble knight sworn to protect the realm.",
             "memories": ["The traveller's name is Loki"]},
        ],
        "location": "Medows",
        "history": [
            "Sir Lancelot: Halt, traveller. The roads have grown dangerous since the ash began to fall.",
            "Traveller: Do you remember my name?",
        ],
        "max_lines": 1,
    })
    assert r.status_code == 200
    reply = " ".join(r.json()["lines"]).lower()
    assert "loki" in reply, f"knight did not use the remembered name: {reply!r}"
