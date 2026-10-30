using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.GameWorld.Locations;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// Every conversation in the world, from start to what the NPCs learn from it:
    ///   1. Start  - build the request (who's there, what each NPC remembers) and open it.
    ///   2. Finish - when one ends, file a transcript with each NPC (cold memory) and ask the
    ///               LLM, per NPC, what they learned.
    ///   3. Apply  - when an answer comes back, hand it to that NPC's brain (hot memory, tasks).
    ///
    /// It's given what it needs (the level, today's day) rather than the whole World, and its
    /// two LLM calls are handed in, so it can be tested on its own with fakes.
    /// </summary>
    public class ConversationSystem
    {
        // How many remembered facts, and how many earlier conversations (as one-line
        // summaries), to put in front of the LLM when a conversation starts.
        private const int MaxInjectedMemories = 5;
        private const int MaxRecalledConversations = 3;
        // How many of today's events, and how many diary entries (recent days), go in too.
        private const int MaxEventsToday = 8;
        private const int MaxRecalledDays = 3;
        private const int MaxRumoursToday = 5;

        private readonly Func<ConversationRequestDto, Task<ChatMessage[]>> _sendChat;
        private readonly Func<DecisionRequestDto, Task<NpcDecisionDto>> _requestDecision;

        private readonly List<Conversation> _active = new();

        // Decisions in flight: one per NPC per finished conversation. Each carries the level the
        // conversation happened in (so a go_to resolves against the right POIs even if the
        // player has moved on) and its transcript (so the summary and facts link back to it).
        // Holding the Task here also keeps it from being garbage-collected.
        private readonly List<(NPC npc, Location level, Transcript transcript, Task<NpcDecisionDto> task)> _pending = new();

        // Hands out transcript ids. Only counts up, so a Memory.SourceId always points at the
        // right conversation.
        private int _nextTranscriptId = 1;

        /// <summary>The id the next transcript will get. For saving.</summary>
        public int PeekNextTranscriptId => _nextTranscriptId;

        /// <summary>
        /// Carries on a saved game's transcript ids, so a new conversation never gets the id of
        /// a saved one (memories find their transcript by that id).
        /// </summary>
        public void Restore(int nextTranscriptId) => _nextTranscriptId = nextTranscriptId;

        /// <param name="sendChat">How a chat reply is asked for. Defaults to the real API.</param>
        /// <param name="requestDecision">How an end-of-conversation decision is asked for. Defaults to the real API.</param>
        public ConversationSystem(Func<ConversationRequestDto, Task<ChatMessage[]>> sendChat = null,
                                  Func<DecisionRequestDto, Task<NpcDecisionDto>> requestDecision = null)
        {
            _sendChat = sendChat ?? DialogueApi.RequestAsync;
            _requestDecision = requestDecision ?? DialogueApi.RequestDecisionAsync;
        }

        /// <summary>
        /// The world's event log: a finished conversation is posted to it as a Talked event, and
        /// a faction joined as Joined, and today's grudges are read from it. Set by the World;
        /// null posts nothing and holds no grudges.
        /// </summary>
        public EventLog Events { get; set; }

        /// <summary>Finds a person (player or NPC) by id, for naming who told whom and who joins. Set by the World.</summary>
        public Func<int, Entity> FindPerson { get; set; }

        /// <summary>The faction table, for checking and applying join_faction. Set by the World.</summary>
        public FactionTable Factions { get; set; }

        /// <summary>Conversations still going.</summary>
        public IReadOnlyList<Conversation> Active => _active;

        /// <summary>Decisions asked for but not yet back.</summary>
        public int PendingDecisions => _pending.Count;

        // ------------------------------------------------------------------ 1. start

        /// <summary>
        /// Opens a conversation started by <paramref name="from"/> with <paramref name="others"/>.
        /// Whoever starts a conversation speaks first, so it opens with nothing said - only a
        /// game note saying who approached whom. The LLM's first line is its answer to the
        /// opener, made with everything the NPC remembers, so it isn't the same every time.
        /// (NPCs starting conversations themselves comes later - see docs/npc-memory-next-plan.md, A1.)
        /// </summary>
        public Conversation Start(Entity from, NPC[] others, Location level, int today)
        {
            // Everyone in the room, initiator first.
            var everyone = new Entity[others.Length + 1];
            everyone[0] = from;
            others.CopyTo(everyone, 1);

            var convo = new Conversation(everyone, level, BuildRequest(everyone, others, level, today), _sendChat);
            // A note, not a line: no speaker, so it's never sent to the LLM or remembered.
            convo.Note($"You approach {others[0].Name}.");

            foreach (var npc in others) npc.StartTalking();
            _active.Add(convo);
            return convo;
        }

        // What the LLM is told about the people in this conversation. The initiator listens
        // (the player types their own lines); each NPC speaks, with its persona, what it
        // remembers and how it feels about everyone else there - attached to that NPC, so the
        // prompt frames it as their own knowledge.
        private ConversationRequestDto BuildRequest(Entity[] everyone, NPC[] others, Location level, int today)
        {
            var from = everyone[0];
            var req = new ConversationRequestDto
            {
                participants = new List<ParticipantDto> { new() { name = Conversation.NameOf(from), speaks = false } },
                location = level?.Name,
                // One reply per thing said, so it reads as a back-and-forth.
                max_lines = others.Length,
            };
            foreach (var npc in others)
                req.participants.Add(new ParticipantDto
                {
                    name = npc.Name,
                    persona = npc.Persona,
                    memories = npc.RecentMemories(MaxInjectedMemories).ToList(),
                    past_conversations = npc.PastConversationsWith(from.Id, MaxRecalledConversations, today).ToList(),
                    events_today = npc.EventsToday(today, MaxEventsToday).ToList(),
                    recent_days = npc.RecentDays(MaxRecalledDays, today).ToList(),
                    rumours = npc.RumoursToday(today, MaxRumoursToday, NameById).ToList(),
                    attitudes = everyone.Where(who => who != npc)
                                        .Select(who => Relations.Describe(npc, who, Conversation.NameOf(who), Events, today))
                                        .ToList(),
                });
            return req;
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>
        /// Call once a frame: closes finished conversations (2) and applies any decisions that
        /// have come back (3). Runs on the main thread, so changing NPC brains here is safe.
        /// </summary>
        public void Update(int today)
        {
            FinishEnded(today);
            ApplyArrivedDecisions();
        }

        // ------------------------------------------------------------------ 2. finish

        private void FinishEnded(int today)
        {
            foreach (var convo in _active.Where(c => c.IsFinished).ToList())
            {
                var npcs = convo.Participants.OfType<NPC>().ToList();
                foreach (var npc in npcs) npc.StopTalking();   // back to what they were doing

                // One transcript per conversation, shared by everyone in it; then each NPC asks
                // what it learned. Skipped entirely if there was nothing to learn.
                var transcript = Record(convo, npcs, today);
                if (transcript != null)
                    foreach (var npc in npcs) AskWhatWasLearned(npc, convo, transcript);

                _active.Remove(convo);
            }
        }

        // Snapshots a finished conversation and files it in the cold-tier memory of every NPC
        // that took part. Null when nobody but the NPCs spoke (opened and closed the chat):
        // there's nothing to remember, and asking the LLM anyway gets filler memories
        // ("the traveller was present") and a wasted call.
        private Transcript Record(Conversation convo, List<NPC> npcs, int today)
        {
            var npcNames = npcs.Select(n => n.Name).ToHashSet();
            bool anyoneElseSpoke = convo.Messages.Any(m => m.Speaker != null && !npcNames.Contains(m.Speaker));
            if (!anyoneElseSpoke) return null;

            var transcript = convo.ToTranscript(_nextTranscriptId++, today);
            foreach (var npc in npcs) npc.RememberConversation(transcript);
            Events?.Post(TalkedEvent(convo, npcs, transcript));
            return transcript;
        }

        // "Sir Lancelot talked with the traveller" - so the day as reflection sees it includes
        // conversations, and an NPC standing nearby overhears that it happened. The initiator
        // is the actor; the first NPC they approached is the target.
        private static GameEvent TalkedEvent(Conversation convo, List<NPC> npcs, Transcript transcript)
        {
            var from = convo.Participants[0];
            var others = convo.Participants.Skip(1).Select(Conversation.NameOf);
            return GameEvent.Of(EventType.Talked, convo.Location?.Name, from.Feet, from.Id,
                                npcs.FirstOrDefault(n => n != from)?.Id,
                                $"{Conversation.NameOf(from)} talked with {string.Join(" and ", others)}",
                                transcript.Id);
        }

        // Fire-and-hold: start the LLM call and keep the Task; ApplyArrivedDecisions picks up
        // the answer on a later frame. Nothing waits here, so the game never stalls.
        private void AskWhatWasLearned(NPC npc, Conversation convo, Transcript transcript)
        {
            var req = new DecisionRequestDto
            {
                npc_name = npc.Name,
                persona = npc.Persona,
                location = transcript.Location,
                transcript = transcript.Lines.ToArray(),   // already only what was actually said
                poi_names = convo.Location?.pointsOfInterest.Keys.ToArray() ?? Array.Empty<string>(),
                people = transcript.Participants.Where(p => p.Id != npc.Id).Select(p => p.Name).ToArray(),
                // Only factions a player there isn't already in: there's nobody else to let in.
                can_admit = Factions == null ? Array.Empty<string>()
                    : npc.Factions.Where(f => npc.CanAdmit(f, Factions)
                                              && convo.Participants.OfType<Player>().Any(p => !p.Factions.Contains(f)))
                                  .ToArray(),
            };
            _pending.Add((npc, convo.Location, transcript, _requestDecision(req)));
        }

        // ------------------------------------------------------------------ 3. apply

        // A failed call is dropped - the NPC just doesn't learn from that conversation.
        // Walks backwards so removing an entry doesn't skip the next one.
        private void ApplyArrivedDecisions()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var (npc, level, transcript, task) = _pending[i];
                if (!task.IsCompleted) continue;
                if (task.IsCompletedSuccessfully)
                {
                    npc.ApplyDecision(task.Result, level, transcript);
                    Admit(npc, task.Result?.join_faction, transcript);
                }
                _pending.RemoveAt(i);
            }
        }

        // The decision's join_faction: the NPC lets the players it talked to into a faction. The
        // LLM only proposes it - it happens only if this NPC can admit people to that faction.
        private void Admit(NPC npc, string faction, Transcript transcript)
        {
            if (string.IsNullOrWhiteSpace(faction) || Factions == null || !npc.CanAdmit(faction, Factions)) return;

            foreach (var player in transcript.Participants.Select(p => FindPerson?.Invoke(p.Id)).OfType<Player>())
                if (player.Join(faction, Factions))
                    Events?.Post(GameEvent.Of(EventType.Joined, transcript.Location, player.Feet, player.Id, npc.Id,
                                              $"{player.Name} joined the {faction}, sworn in by {npc.Name}"));
        }

        private string NameById(int id) => FindPerson?.Invoke(id) is { } who ? Conversation.NameOf(who) : null;
    }
}
