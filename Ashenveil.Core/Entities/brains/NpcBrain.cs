using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Jobs;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
using Ashenveil.Core.GameWorld;
using Ashenveil.Core.GameWorld.Locations;

namespace Ashenveil.Core.Entities
{

    /// <summary>
    /// Decides how an NPC moves. NPC owns the body (position, push-out collision,
    /// animation); the brain owns the heading - where to go and how to react when
    /// something gets in the way.
    /// </summary>
    public abstract class NpcBrain
    {
        /// <summary>The NPC this brain controls. Set once by <see cref="Attach"/> when the NPC is built.</summary>
        public NPC Npc { get; private set; }
        public Entity target { get; set; } = null; //use for later AI, like following the player or attacking them
        //State machine for NPCs
        public enum NPCState
        {
            Idle,
            Walking,
            Talking,
            Working, // running its job queue (go_to, wait, ...); Idle/Walking again once it empties
        }
        /// <summary>
        /// Binds this brain to its NPC and runs <see cref="Start"/>. Called from the NPC
        /// constructor; a brain belongs to exactly one NPC, so attaching twice throws.
        /// </summary>
        internal void Attach(NPC npc)
        {
            if (Npc != null) throw new InvalidOperationException("This brain is already attached to an NPC.");
            Npc = npc ?? throw new ArgumentNullException(nameof(npc));
            Start();
        }
        public Journal Journal { get; } = new Journal();
        /// <summary>What this NPC thinks of people and factions.</summary>
        public brains.Opinions Opinions { get; } = new brains.Opinions();
        public Queue<IJob> JobQueue { get; } = new Queue<IJob>();
        public NPCState State { get; protected set; } = NPCState.Idle;
        // One blackboard per NPC, reused across every tick and every job, so jobs can
        // share scratch state (a GoTo's target cell, "have I started") between frames.
        private readonly Blackboard _blackboard = new();

        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        protected virtual void Start() {}

        /// <summary>
        /// Hand the NPC a job to run; switches it into the Working state. A follow never ends
        /// on its own, so anything queued after one would never run: the new job replaces it
        /// instead. That keeps a follow always last in the queue.
        /// </summary>
        public void Enqueue(IJob job)
        {
            if (JobQueue.Count > 0 && JobQueue.Last() is FollowJob)
            {
                // A Queue can't drop its back item, so rebuild it without the follow.
                var keep = JobQueue.Take(JobQueue.Count - 1).ToList();
                JobQueue.Clear();
                foreach (var kept in keep) JobQueue.Enqueue(kept);
            }
            JobQueue.Enqueue(job);
            State = NPCState.Working;
        }

        public virtual void StartTalking()
        {
            State = NPCState.Talking;
            //face the player
            Npc.Direction = Vector2.Zero;
        }
        public virtual void StopTalking()
        {
            // Resume an interrupted errand if one is waiting; otherwise go back to wandering.
            State = JobQueue.Count > 0 ? NPCState.Working : NPCState.Walking;
            if (State == NPCState.Walking)
            {
                float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                Npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
        }

        /// <summary>
        /// The fixed per-frame flow, shared by every brain: talking stands still, a working
        /// NPC ticks its queue, and anything else defers to the subclass's default behaviour.
        /// Subclasses override <see cref="UpdateDefault"/>, not this, so the job machinery
        /// can never be accidentally shadowed.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            switch (State)
            {
                case NPCState.Talking:
                    // Stand still while talking.
                    break;
                case NPCState.Working:
                    TickJobs(dt);
                    break;
                default: // Idle / Walking
                    UpdateDefault(gameTime);
                    break;
            }
        }

        /// <summary>A brain's own idle/wander behaviour. Base does nothing; WanderBrain walks.</summary>
        protected virtual void UpdateDefault(GameTime gameTime) { }

        // Runs the job at the front of the queue until it reports Completed or Failed, then moves on.
        // A job may take many ticks; Peek (not Dequeue) keeps it at the front until it's done.
        private void TickJobs(float dt)
        {
            if (JobQueue.Count == 0)
            {
                State = NPCState.Walking;   // nothing left to do -> back to the default loop
                return;
            }

            IJob job = JobQueue.Peek();
            if (job.State == JobState.Inactive) job.State = JobState.Active;

            job.Execute(new JobContext
            {
                Owner = Npc,
                Blackboard = _blackboard,
                DeltaTime = dt,
                Nav = Npc.Location?.Nav,
                Post = Npc.PostEvent,
                FindPerson = Npc.FindPerson,
            });

            // A failed job is dropped the same way, so the NPC moves on to the next one. Either
            // way the world hears about it - but only for trips: a wait is never news.
            if (job.State is JobState.Completed or JobState.Failed)
            {
                JobQueue.Dequeue();
                if (job is GoToJob go) Npc.PostEvent?.Invoke(TripEvent(go));
            }
        }

        // "Sir Lancelot reached the entry" / "Sir Lancelot couldn't reach the well". The job only
        // knows a point, so the place is the POI standing there, if any.
        private GameEvent TripEvent(GoToJob go)
        {
            var poi = Npc.Location?.pointsOfInterest.Values
                .FirstOrDefault(p => Vector2.Distance(p.StandingSpot(), go.Target) < 0.01f);
            string place = poi != null ? $"the {poi.Name.Replace('_', ' ')}" : $"({go.Target.X:0.0}, {go.Target.Y:0.0})";
            bool done = go.State == JobState.Completed;
            return GameEvent.Of(done ? EventType.TaskDone : EventType.TaskFailed, Npc.Location?.Name, Npc.Feet,
                                Npc.Id, null, done ? $"{Npc.Name} reached {place}" : $"{Npc.Name} couldn't reach {place}");
        }

        // TODO (separate feature): combat — enemy detection, targeting, a Combat state.
        // Parked until the NPC memory/behaviour work (docs/npc-memory-next-plan.md) is further along.

        /// <summary>
        /// Called after the NPC was pushed out of another entity. <paramref name="horizontal"/>
        /// is true when the push was along X. Default: do nothing, just stay pushed out.
        /// </summary>
        public virtual void OnBlocked(bool horizontal) { }

        /// <param name="transcript">The conversation this decision came from, if any. Its summary is
        /// filled in here, and every new fact is stamped with its id so it can be traced back.</param>
        public void ApplyDecision(NpcDecisionDto decision, Location location, Transcript transcript = null)
        {
            if (decision == null) return;

            // 0. Label the conversation in the cold tier. The transcript is shared by everyone in
            // the conversation, so the first NPC's summary sticks rather than each one overwriting it.
            if (transcript != null && transcript.Summary == null && !string.IsNullOrWhiteSpace(decision.summary))
                transcript.Summary = decision.summary.Trim();

            // 1. Remember: each extracted fact becomes a hot memory, stamped with the transcript
            // it came from and its day.
            Remember(decision.memories, 5, MemoryKind.Conversation, transcript?.Id, transcript?.Day ?? 0);

            // 2. Optionally drop whatever the NPC was doing.
            if (decision.clear_current_tasks) JobQueue.Clear();

            // 3. Turn each spec into a real job, dropping anything we can't honour.
            foreach (var spec in decision.tasks ?? Array.Empty<TaskSpecDto>())
            {
                IJob job = spec.type switch
                {
                    "go_to" => MakeGoTo(spec, location),
                    "wait"  => new WaitJob(Math.Clamp(spec.seconds, 0f, 600f)),
                    "follow" => MakeFollow(spec, transcript),
                    _       => null,                    // unknown type -> ignore
                };
                if (job != null) Enqueue(job);
            }

            // 4. Resulting state: queued work always wins; otherwise honour the mood.
            State = JobQueue.Count > 0 ? NPCState.Working
                : decision.behaviour == "idle" ? NPCState.Idle
                : NPCState.Walking;
        }

        /// <summary>
        /// The day-end reflection on <paramref name="day"/> has come back: the diary entry goes in
        /// the cold tier, the lasting facts in the hot tier, and the raw events it was written
        /// from (<paramref name="reflected"/>) leave the hot tier - the entry stands in for them,
        /// and they're still in the event log.
        /// </summary>
        public void ApplyReflection(ReflectionDto reflection, int day, IEnumerable<Memory> reflected)
        {
            if (reflection == null) return;

            if (!string.IsNullOrWhiteSpace(reflection.diary)) Journal.Add(new DayEntry(day, reflection.diary.Trim()));
            Remember(reflection.facts, 3, MemoryKind.Reflection, null, day);
            Journal.Forget(reflected);
            Journal.LastReflectedDay = Math.Max(Journal.LastReflectedDay, day);
        }

        // Each fact becomes a discrete Memory (the hot tier), at most `max` of them, stamped with
        // where it came from and the day. Facts already in the journal (ignoring case and edge
        // whitespace) are skipped, so repeats don't push real memories out of the injected
        // window. Reworded duplicates still get through; merging those is consolidation (A4).
        private void Remember(IEnumerable<string> facts, int max, MemoryKind kind, int? sourceId, int day)
        {
            foreach (var fact in (facts ?? Array.Empty<string>())
                         .Where(m => !string.IsNullOrWhiteSpace(m))
                         .Select(m => m.Trim())
                         .Take(max))
            {
                bool known = Journal.Memories.Any(m =>
                    string.Equals(m.Text, fact, StringComparison.OrdinalIgnoreCase));
                if (!known) Journal.Add(new Memory(fact, kind) { SourceId = sourceId, Day = day });
            }
        }

        // A go_to survives only if its target names a real POI in this level.
        private IJob MakeGoTo(TaskSpecDto spec, Location location)
        {
            var poi = location?.Poi(spec.target);
            return poi == null ? null : new GoToJob(poi.StandingSpot());
        }

        // Someone else from the conversation, by name: the transcript says who was there and
        // their ids. No transcript, a stranger, or the NPC itself: no job.
        private IJob MakeFollow(TaskSpecDto spec, Transcript transcript)
        {
            string name = spec.target?.Trim();
            var who = transcript?.Participants.FirstOrDefault(p =>
                p.Id != Npc.Id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            return who == null ? null : new FollowJob(who.Id);
        }


    }
}
