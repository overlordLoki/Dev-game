using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{
    /// <summary>
    /// Shared code for everything the player doesn't control: a name, a brain that
    /// decides the heading, and push-out collision. The subclass only brings its art,
    /// size and speed (see Knight).
    /// </summary>
    public abstract class NPC : Entity
    {
        protected NpcBrain Brain { get; }
        public string Name { get; }
        // Finds a person by id, but only in this NPC's level. Set by the roster; null: nobody.
        public Func<int, Entity> FindPerson { get; set; }
        public string Portrait { get; set; }
        // The persona seeds who the NPC is and how they talk. There's no fixed opening line:
        // whoever starts a conversation speaks first, and the LLM writes every NPC line
        // (see ConversationSystem.Start).
        public string Persona { get; set;} = "A generic NPC.";
        // The level this NPC is in, set by NpcRoster.Add. Null until it's been added to one
        // (tests often skip that). Jobs get the level's NavGrid through it.
        public GameWorld.Locations.Location Location { get; internal set; }
        protected NPC(Vector2 position, int id, string name, NpcBrain brain) : base(position, id)
        {
            Brain = brain;
            this.Name = name;
            Brain.Attach(this);
        }

        public override void Update(GameTime gameTime)
        {
            // TODO: NPC AI logic. Subclasses register at least "idle" and "walk".
            Play(Direction == Vector2.Zero ? "idle" : "walk");
            Brain.Update(gameTime);
            base.Update(gameTime);
        }

        // Read-only views of the brain, for whoever needs to look in (the dev console). Changes
        // still go through the NPC's own methods below.
        public NpcBrain.NPCState State => Brain.State;
        public brains.Memorys.Journal Journal => Brain.Journal;
        public brains.Opinions Opinions => Brain.Opinions;
        // The job queue, front first. For saving; new jobs still go in through GiveJob.
        public IReadOnlyCollection<brains.Jobs.IJob> Jobs => Brain.JobQueue;
        // Where the current go-to job is still heading, in cells; empty when the NPC isn't
        // walking to anything. For the `nav` debug drawing.
        public IReadOnlyList<Vector2> CurrentPath =>
            State == NpcBrain.NPCState.Working && Brain.JobQueue.TryPeek(out var job) && job is brains.Jobs.GoToJob go
                ? go.RemainingWaypoints
                : Array.Empty<Vector2>();

        /// <summary>
        /// How this NPC tells the world something happened to it (EventLog.Post): NpcRoster.Add
        /// wires it up. Null outside a world, and then nothing is posted.
        /// </summary>
        public Action<GameWorld.GameEvent> PostEvent { get; set; }

        /// <summary>
        /// The factions this NPC belongs to ("guard"). Saved. The first one is what it's mostly
        /// known as; for judging it, every one counts (the lowest view wins).
        /// </summary>
        public List<string> Factions { get; } = new();

        /// <summary>
        /// Whether this NPC can let people into its own factions (a guard captain can; a
        /// villager can't). Comes with the NPC's type, so it isn't saved.
        /// </summary>
        public bool Admits { get; protected set; }

        /// <summary>Whether this NPC can let someone into <paramref name="faction"/>.</summary>
        public bool CanAdmit(string faction, GameWorld.FactionTable table) =>
            Admits && Factions.Contains(faction) && table.IsJoinable(faction);

        /// <summary>
        /// What this NPC's factions think of someone, with no personal opinion in the way: for a
        /// player, the standing they have with this NPC's factions; for an NPC, this NPC's view
        /// of their factions. The lowest wins either way; no factions is 0.
        /// </summary>
        public int FactionViewOf(Entity who) => who switch
        {
            Player player => GameWorld.FactionTable.Lowest(Factions, player.StandingWith),
            NPC npc => GameWorld.FactionTable.Lowest(npc.Factions, Opinions.OfFaction),
            _ => 0,
        };

        /// <summary>What this NPC thinks of someone: its own opinion if it has one, else its faction view.</summary>
        public int OpinionOf(Entity who) =>
            Opinions.TryPersonal(who.Id, out var mine) ? mine.Score : FactionViewOf(who);

        /// <summary>Moves this NPC's opinion of someone, starting a personal one from the faction view if needed.</summary>
        public int ChangeOpinionOf(Entity who, int by, string why = null) =>
            Opinions.Change(who.Id, FactionViewOf(who), by, why);

        // The brain is private to the NPC, so the World starts and ends talking through these.
        public void StartTalking() => Brain.StartTalking();
        public void StopTalking() => Brain.StopTalking();

        /// <summary>
        /// Push out of another entity, then let the brain decide how to react (a
        /// wanderer bounces away).
        /// </summary>
        public override void CheckEntityCollision(IEntity other)
        {
            Collision.RectF me = Bounds;
            Collision.RectF them = other.Bounds;
            if (!me.Intersects(them)) return;

            var (pushX, pushY) = Overlap(me, them);
            bool horizontal = Math.Abs(pushX) < Math.Abs(pushY);
            if (horizontal)
                Position = new Vector2(Position.X + pushX, Position.Y);
            else
                Position = new Vector2(Position.X, Position.Y + pushY);

            OnBlocked(horizontal);
        }

        /// <summary>Stopped by an entity or the map's edge: the brain decides how to react.</summary>
        public override void OnBlocked(bool horizontal) => Brain.OnBlocked(horizontal);
        public void GiveJob(brains.Jobs.IJob job)
        {
            Brain.Enqueue(job);
        }

        // Files a finished conversation in this NPC's cold-tier memory. The same transcript
        // is shared by every NPC that took part.
        public void RememberConversation(brains.Memorys.Transcript transcript)
        {
            Brain.Journal.Add(transcript);
        }

        // The brain is protected, so the World hands an end-of-conversation decision in here.
        public void ApplyDecision(brains.Dialogue.NpcDecisionDto decision, GameWorld.Locations.Location location,
                                  brains.Memorys.Transcript transcript = null)
        {
            Brain.ApplyDecision(decision, location, transcript);
        }

        /// <summary>
        /// This NPC saw <paramref name="e"/> happen (or it happened to them): a hot memory of it,
        /// with its day and the event's id. Perception decides who calls this.
        /// </summary>
        public void Witness(GameWorld.GameEvent e) =>
            Brain.Journal.Add(new brains.Memorys.Memory(e.Text, brains.Memorys.MemoryKind.Event) { SourceId = e.Id, Day = e.Day });

        // The day-end reflection came back; see NpcBrain.ApplyReflection.
        public void ApplyReflection(brains.Dialogue.ReflectionDto reflection, int day,
                                    IEnumerable<brains.Memorys.Memory> reflected) =>
            Brain.ApplyReflection(reflection, day, reflected);

        // The last few remembered facts as prompt lines, so a new conversation can open
        // with the NPC remembering past ones. Events aren't facts: they go in EventsToday.
        public IEnumerable<string> RecentMemories(int count) =>
            Brain.Journal.Recent(count).Select(m => m.Text);

        // What this NPC saw happen today, the last `count` of it, oldest first. Kept apart from
        // the facts so a busy day can't push out "the traveller's name is Loki".
        public IEnumerable<string> EventsToday(int today, int count) =>
            Brain.Journal.EventsOn(today).TakeLast(count).Select(m => m.Text);

        // What this NPC heard from others today, the last `count` of it, framed as hearsay
        // ("Marta told you: the traveller robbed the miller"). `nameOf` names the teller by id.
        public IEnumerable<string> RumoursToday(int today, int count, Func<int, string> nameOf) =>
            Brain.Journal.RumoursOn(today).TakeLast(count).Select(m => Hearsay(m, nameOf));

        public static string Hearsay(brains.Memorys.Memory rumour, Func<int, string> nameOf) =>
            $"{(rumour.TellerId is int id ? nameOf(id) ?? "Someone" : "Someone")} told you: {rumour.Text}";

        // Recall from the diary: the last `count` reflected days, oldest first ("Yesterday: I
        // waited at the castle door...").
        public IEnumerable<string> RecentDays(int count, int today) =>
            Brain.Journal.Days.TakeLast(count).Select(d => $"{DaysAgo(today - d.Day)}: {d.Summary}");

        // Recall from the cold tier: one line per earlier conversation with the person whose
        // entity Id is `personId`, oldest first, the last `count` of them ("2 days ago, at
        // Medows: ..."). By id, not name, so a rename or two people with the same name can't
        // mix memories up. Transcripts without a summary (the decision never came back) are
        // skipped - there's nothing to say.
        public IEnumerable<string> PastConversationsWith(int personId, int count, int today) =>
            Brain.Journal.Transcripts
                .Where(t => t.Summary != null && t.Participants.Any(p => p.Id == personId))
                .TakeLast(count)
                .Select(t => $"{DaysAgo(today - t.Day)}{(t.Location != null ? $", at {t.Location}" : "")}: {t.Summary}");

        private static string DaysAgo(int days) => days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            _ => $"{days} days ago",
        };
    }
}
