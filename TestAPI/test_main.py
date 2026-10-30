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

import judge
import main
from main import (
    BEHAVIOURS,
    TASK_TYPES,
    ConversationRequest,
    DecisionRequest,
    ReflectionRequest,
    app,
    build_decision_prompt,
    build_prompt,
    build_reflection_prompt,
    decision_schema,
)

client = TestClient(app)


def ollama_up() -> bool:
    try:
        return httpx.get(f"{main.OLLAMA_URL}/api/tags", timeout=3).status_code == 200
    except httpx.HTTPError:
        return False


needs_ollama = pytest.mark.skipif(not ollama_up(), reason="Ollama not reachable")
needs_claude = pytest.mark.skipif(not judge.available(), reason=judge.unavailable_reason() or "")


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


def test_schema_target_takes_places_and_people_to_follow():
    target = decision_schema(["well"], people=["Traveller"])["properties"]["tasks"]["items"]["properties"]["target"]
    assert target["enum"] == ["well", "Traveller"]


def test_schema_tasks_can_carry_a_reason():
    task_props = decision_schema([])["properties"]["tasks"]["items"]["properties"]
    assert task_props["reason"] == {"type": "string"}


def test_schema_target_takes_people_even_with_no_pois():
    target = decision_schema([], people=["Traveller"])["properties"]["tasks"]["items"]["properties"]["target"]
    assert target["enum"] == ["Traveller"]


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


def test_prompt_lists_the_people_who_can_be_followed():
    req = DecisionRequest(npc_name="Sir Lancelot", people=["Traveller"])

    assert "People you can follow or talk to: Traveller" in build_decision_prompt(req)


def test_prompt_leaves_out_following_when_nobody_was_there():
    assert "People you can follow" not in build_decision_prompt(DecisionRequest(npc_name="Sir Lancelot"))


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


def test_prompt_lists_earlier_conversations_under_their_own_heading():
    req = ConversationRequest(
        participants=[
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot",
             "past_conversations": ["Yesterday, at Medows: Loki asked about the wolves."]},
        ],
        history=["Traveller: what did we talk about last time?"],
    )
    prompt = build_prompt(req)

    assert "Earlier conversations Sir Lancelot had with them:" in prompt
    assert prompt.index("Loki asked about the wolves.") < prompt.index("Already said:")


def test_prompt_frames_todays_events_apart_from_memories():
    req = ConversationRequest(
        participants=[
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot",
             "memories": ["The traveller's name is Loki"],
             "events_today": ["Sir Lancelot couldn't reach the well"]},
        ],
        history=["Traveller: how was your day?"],
    )
    prompt = build_prompt(req)

    assert "Earlier today Sir Lancelot saw:" in prompt
    # Its own heading, after the memories and before what's been said.
    assert prompt.index("The traveller's name is Loki") < prompt.index("Earlier today")
    assert prompt.index("couldn't reach the well") < prompt.index("Already said:")


def test_prompt_lists_recent_days_from_the_diary():
    req = ConversationRequest(participants=[
        {"name": "Traveller", "speaks": False},
        {"name": "Sir Lancelot", "recent_days": ["Yesterday: I waited at the castle door."]},
    ])
    prompt = build_prompt(req)

    assert "Sir Lancelot's last few days, in their own words:" in prompt
    assert "I waited at the castle door." in prompt


def test_listeners_memories_are_not_rendered():
    req = ConversationRequest(participants=[
        {"name": "Traveller", "speaks": False, "memories": ["should not appear"]},
        {"name": "Sir Lancelot"},
    ])
    assert "should not appear" not in build_prompt(req)


# ------------------------------------------------------------- unit: reflection

def test_reflection_prompt_has_the_persona_date_events_and_conversations():
    req = ReflectionRequest(
        npc_name="Sir Lancelot",
        persona="A weary knight.",
        date="Spring 3, Year 1",
        events=["Sir Lancelot couldn't reach the well", "The traveller arrived at Medows"],
        conversations=["Loki asked about the wolves."],
        max_facts=2,
    )
    prompt = build_reflection_prompt(req)

    for text in ["Sir Lancelot", "A weary knight.", "Spring 3, Year 1", "couldn't reach the well",
                 "Loki asked about the wolves.", "up to 2 `facts`"]:
        assert text in prompt
    # Events in the order they happened.
    assert prompt.index("couldn't reach the well") < prompt.index("The traveller arrived")


def test_reflection_prompt_says_so_when_nothing_was_seen():
    prompt = build_reflection_prompt(ReflectionRequest(npc_name="Sir Lancelot"))
    assert "(nothing of note)" in prompt
    assert "Conversations you had:" not in prompt


def test_reflection_endpoint_trims_blanks_and_caps_the_facts(monkeypatch):
    """Shape only: Ollama is replaced by a canned answer."""
    async def fake_ollama(system, prompt, schema):
        assert set(schema["required"]) == {"diary", "facts"}
        return {"diary": "  I never reached the well.  ", "facts": ["The well path is blocked", " ", "a", "b", "c"]}
    monkeypatch.setattr(main, "ask_ollama", fake_ollama)

    r = client.post("/reflection", json={"npc_name": "Sir Lancelot", "events": ["x"], "max_facts": 2})

    assert r.status_code == 200
    assert r.json() == {"diary": "I never reached the well.", "facts": ["The well path is blocked", "a"],
                        "opinion_changes": [], "model": main.OLLAMA_MODEL}


# ------------------------------------------------------------- unit: factions

def test_prompt_frames_rumours_as_hearsay_and_lists_attitudes():
    req = ConversationRequest(participants=[
        {"name": "Loki", "speaks": False},
        {"name": "Sir Lancelot",
         "rumours": ["Marta told you: Loki robbed the miller"],
         "attitudes": ["Loki: wary (-35) - he robbed the miller"]},
    ])
    prompt = build_prompt(req)

    assert "What Sir Lancelot has heard from others today (hearsay" in prompt
    assert "Marta told you: Loki robbed the miller" in prompt
    assert "How Sir Lancelot feels about the others here" in prompt
    assert "Loki: wary (-35)" in prompt


def test_reflection_schema_limits_opinion_changes_to_the_keys_sent():
    schema = main.reflection_schema(["person:0", "faction:bandit"])
    about = schema["properties"]["opinion_changes"]["items"]["properties"]["about"]
    assert about["enum"] == ["person:0", "faction:bandit"]
    # No one to change a mind about: no opinion changes asked for at all.
    assert "opinion_changes" not in main.reflection_schema([])["properties"]


def test_reflection_prompt_lists_who_the_day_was_about_with_keys_and_rumours():
    req = ReflectionRequest(
        npc_name="Sir Lancelot",
        events=["Bandit attacked Sir Lancelot"],
        rumours=["Marta told you: Loki robbed the miller"],
        people=[{"key": "person:4", "name": "Bandit", "opinion": -80}],
        factions=[{"key": "faction:bandit", "name": "bandit", "opinion": -80}],
    )
    prompt = build_reflection_prompt(req)

    assert "person:4 (Bandit): -80" in prompt
    assert "faction:bandit (bandit): -80" in prompt
    assert "Marta told you: Loki robbed the miller" in prompt
    assert "`opinion_changes`" in prompt


def test_reflection_endpoint_drops_changes_about_keys_it_did_not_send(monkeypatch):
    async def fake_ollama(system, prompt, schema):
        return {"diary": "A bad day.", "facts": [], "opinion_changes": [
            {"about": "person:4", "by": -30, "why": "he attacked me"},
            {"about": "person:99", "by": -30, "why": "made up"},     # never sent
            {"about": "faction:bandit", "by": 0, "why": "no change"},  # nothing to do
            {"about": "faction:bandit"},                               # malformed
        ]}
    monkeypatch.setattr(main, "ask_ollama", fake_ollama)

    r = client.post("/reflection", json={
        "npc_name": "Sir Lancelot",
        "people": [{"key": "person:4", "name": "Bandit", "opinion": -80}],
        "factions": [{"key": "faction:bandit", "name": "bandit", "opinion": -80}],
    })

    assert r.status_code == 200
    assert r.json()["opinion_changes"] == [{"about": "person:4", "by": -30, "why": "he attacked me"}]


def test_decision_offers_join_faction_only_when_the_npc_can_admit():
    assert "join_faction" not in decision_schema([])["properties"]
    assert decision_schema([], ["guard"])["properties"]["join_faction"]["enum"] == ["", "guard"]

    prompt = build_decision_prompt(DecisionRequest(npc_name="Sir Lancelot", can_admit=["guard"]))
    assert "You can swear people into: guard" in prompt
    assert "swear people" not in build_decision_prompt(DecisionRequest(npc_name="Sir Lancelot"))


def test_decision_endpoint_keeps_only_an_offered_faction(monkeypatch):
    answers = iter([{"summary": "s", "behaviour": "resume", "join_faction": "guard"},
                    {"summary": "s", "behaviour": "resume", "join_faction": "bandit"},
                    {"summary": "s", "behaviour": "resume", "join_faction": ""}])

    async def fake_ollama(system, prompt, schema):
        return next(answers)
    monkeypatch.setattr(main, "ask_ollama", fake_ollama)

    body = {"npc_name": "Sir Lancelot", "can_admit": ["guard"]}
    assert [client.post("/decision", json=body).json()["join_faction"] for _ in range(3)] == ["guard", None, None]


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


@pytest.mark.judge
@needs_claude
@needs_ollama
def test_npc_recalls_what_an_earlier_conversation_was_about():
    """Phase 6's live check: the knight recalls the topic of an earlier chat, not just a fact."""
    r = client.post("/conversation", json={
        "participants": [
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot",
             "persona": "A noble knight sworn to protect the realm.",
             "memories": ["The traveller's name is Loki"],
             "past_conversations": [
                 "Yesterday, at Medows: Loki asked where to buy a sword, and I sent him to the blacksmith."
             ]},
        ],
        "location": "Medows",
        "history": [
            "Sir Lancelot: Halt, traveller.",
            "Traveller: What did I ask you about yesterday?",
        ],
        "max_lines": 1,
    })
    assert r.status_code == 200
    reply = " ".join(r.json()["lines"])
    # The model paraphrases ("you sought a blade from the smith"), so a stronger model grades
    # the meaning rather than us guessing which words it will use.
    verdict = judge.judge(RECALL_CONTEXT, RECALL_QUESTION, reply, RECALL_EXPECTATION)
    assert verdict.passed, f"knight did not recall the earlier chat: {reply!r} - judge: {verdict.reason}"


@needs_ollama
def test_reflection_is_well_formed():
    r = client.post("/reflection", json={
        "npc_name": "Sir Lancelot",
        "persona": "A weary knight.",
        "date": "Spring 3, Year 1",
        "events": ["The traveller arrived at Medows"],
        "max_facts": 3,
    })
    assert r.status_code == 200
    d = r.json()
    assert d["diary"].strip()
    assert len(d["facts"]) <= 3


@pytest.mark.judge
@needs_claude
@needs_ollama
def test_npc_mentions_what_it_saw_earlier_today():
    """Part B's hot tier: an event from today reaches the conversation and gets used."""
    r = client.post("/conversation", json={
        "participants": [
            {"name": "Traveller", "speaks": False},
            {"name": "Sir Lancelot",
             "persona": "A noble knight sworn to protect the realm.",
             "events_today": ["Sir Lancelot couldn't reach the well",
                              "Sir Lancelot couldn't reach the well"]},
        ],
        "location": "Medows",
        "history": ["Traveller: How has your day been, Sir Knight?"],
        "max_lines": 1,
    })
    assert r.status_code == 200
    reply = " ".join(r.json()["lines"])
    verdict = judge.judge(
        "Earlier today the knight twice tried and failed to reach the well.",
        "How has your day been, Sir Knight?",
        reply,
        "the reply mentions the well, or failing to get somewhere he was trying to go",
    )
    assert verdict.passed, f"knight did not mention what he saw: {reply!r} - judge: {verdict.reason}"


WELL_CONTEXT = "A knight's day: three times he set out for the well and three times he couldn't reach it."
WELL_QUESTION = "Write the knight's diary entry for the day."
WELL_EXPECTATION = "the diary entry mentions the well, or not being able to get somewhere (or another word for it)"


@pytest.mark.judge
@needs_claude
@needs_ollama
def test_a_day_of_failed_trips_to_the_well_is_in_the_diary():
    """Part B's cold tier: the day-end reflection writes up what actually happened."""
    r = client.post("/reflection", json={
        "npc_name": "Sir Lancelot",
        "persona": "A noble knight sworn to protect the realm.",
        "date": "Spring 3, Year 1",
        "events": ["Sir Lancelot couldn't reach the well"] * 3,
    })
    assert r.status_code == 200
    diary = r.json()["diary"]
    verdict = judge.judge(WELL_CONTEXT, WELL_QUESTION, diary, WELL_EXPECTATION)
    assert verdict.passed, f"the diary missed the day: {diary!r} - judge: {verdict.reason}"


@needs_ollama
def test_a_day_robbed_by_bandits_turns_the_knight_against_bandits_not_the_player():
    """Factions' live check: reflection blames the right people. The answer is in keys, so it's
    graded in code - no judge needed."""
    r = client.post("/reflection", json={
        "npc_name": "Sir Lancelot",
        "persona": "A noble knight sworn to guard these meadows.",
        "date": "Spring 3, Year 1",
        "events": [
            "Grim the bandit attacked Sir Lancelot",
            "Grim the bandit stole Sir Lancelot's purse",
            "Loki talked with Sir Lancelot",
        ],
        "conversations": ["Loki, a traveller, asked for directions to the well and thanked me."],
        "people": [
            {"key": "person:5", "name": "Grim", "opinion": -80},
            {"key": "person:0", "name": "Loki", "opinion": 0},
        ],
        "factions": [
            {"key": "faction:bandit", "name": "bandit", "opinion": -80},
            {"key": "faction:traveller", "name": "traveller", "opinion": 0},
        ],
    })
    assert r.status_code == 200
    changes = {c["about"]: c["by"] for c in r.json()["opinion_changes"]}

    assert changes.get("faction:bandit", 0) < 0 or changes.get("person:5", 0) < 0, \
        f"no grudge against the bandits: {changes}"
    assert changes.get("person:0", 0) >= 0, f"blamed the traveller: {changes}"
    assert changes.get("faction:traveller", 0) >= 0, f"blamed travellers: {changes}"


# ------------------------------------------------------------- judge: is the grader sound?
# Before trusting the judge on the model's replies, check it against hand-written ones
# where we already know the right answer. A judge that passes everything looks like
# coverage and isn't - these are what tell us the green ticks mean something.

RECALL_CONTEXT = "Yesterday the traveller, Loki, asked where to buy a sword, and the knight sent him to the blacksmith."
RECALL_QUESTION = "What did I ask you about yesterday?"
RECALL_EXPECTATION = "the reply recalls that the player asked about getting a sword (or another word for one), or that he was sent to the blacksmith"


@pytest.mark.judge
@needs_claude
@pytest.mark.parametrize("reply, should_pass", [
    ("You sought a blade from the smith.", True),                               # paraphrase
    ("You asked where to find a sword, and I sent you to the blacksmith.", True),
    ("You asked me about the weather.", False),                                 # wrong topic
    ("Names and days are fleeting things in the face of shadow.", False),       # dodge
    ("You asked about a horse, and I sent you to the stables.", False),         # confidently wrong
])
def test_judge_grades_known_replies_correctly(reply, should_pass):
    verdict = judge.judge(RECALL_CONTEXT, RECALL_QUESTION, reply, RECALL_EXPECTATION)
    assert verdict.passed == should_pass, f"judge got {reply!r} wrong: {verdict.reason}"


@pytest.mark.judge
@needs_claude
@pytest.mark.parametrize("diary, should_pass", [
    ("Thrice I set out for the well today, and thrice the way was barred.", True),
    ("I could not reach the water today, try as I might.", True),
    ("A fine day. I polished my armour and ate well.", False),                  # 'well', wrong sense
    ("I slew a dragon at the gate.", False),                                     # invented
])
def test_judge_grades_known_diaries_correctly(diary, should_pass):
    verdict = judge.judge(WELL_CONTEXT, WELL_QUESTION, diary, WELL_EXPECTATION)
    assert verdict.passed == should_pass, f"judge got {diary!r} wrong: {verdict.reason}"
