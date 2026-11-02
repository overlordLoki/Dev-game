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
        /// <summary>Who this NPC is fighting; null when it isn't. Set by <see cref="StartFighting"/>.</summary>
        public Entity target { get; private set; }
        //State machine for NPCs
        public enum NPCState
        {
            Idle,
            Walking,
            Talking,
            Working, // running its job queue (go_to, wait, ...); Idle/Walking again once it empties
            Dead,    // health reached 0: does nothing at all until healed (Revive)
            Fighting,// chasing and swinging at `target`; pauses the job queue like Talking does
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
        // The most stops a patrol from a decision can have; any after that are dropped.
        private const int MaxPatrolStops = 8;

        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        protected virtual void Start() {}

        /// <summary>
        /// Hand the NPC a job to run; switches it into the Working state. A follow or patrol
        /// never ends on its own, so anything queued after one would never run: the new job
        /// replaces it instead. That keeps an endless job always last in the queue.
        /// </summary>
        public void Enqueue(IJob job)
        {
            if (JobQueue.Count > 0 && JobQueue.Last() is IEndlessJob)
            {
                // A Queue can't drop its back item, so rebuild it without the endless job.
                var keep = JobQueue.Take(JobQueue.Count - 1).ToList();
                JobQueue.Clear();
                foreach (var kept in keep) JobQueue.Enqueue(kept);
            }
            JobQueue.Enqueue(job);
            // The dead keep their queue for if they're healed, but don't get up to run it, and a
            // fighter runs it once the fight's over.
            if (State is not (NPCState.Dead or NPCState.Fighting)) State = NPCState.Working;
        }

        public virtual void StartTalking()
        {
            State = NPCState.Talking;
            //face the player
            Npc.Direction = Vector2.Zero;
        }
        public virtual void StopTalking() => Resume();

        /// <summary>Health reached 0: stop dead. The job queue is kept, for if they're healed.</summary>
        public virtual void Die()
        {
            State = NPCState.Dead;
            target = null;
            Npc.Direction = Vector2.Zero;
        }

        // ------------------------------------------------------------------ fighting

        /// <summary>Further than this from the target, feet to feet, in cells, counts as out of range.</summary>
        public const float GiveUpDistance = 8f;
        /// <summary>Out of range (or unreachable) this long, in seconds, and the NPC gives up.</summary>
        public const float GiveUpSeconds = 4f;
        /// <summary>Seconds between the end of one swing and the start of the next.</summary>
        public const float AttackCooldown = 0.6f;
        /// <summary>How much faster than walking a fighter chases (the knight runs).</summary>
        public const float ChaseSpeedFactor = 1.6f;
        /// <summary>How long a guard is held, in seconds.</summary>
        public const float DefendSeconds = 0.5f;

        private Navigation.Chase _chase;
        private float _outOfRange, _cooldown;
        private int _nextAttack;
        // The last enemy swing this NPC decided whether to guard against, so it decides once per swing.
        private Combat.Swing _swingSeen;

        /// <summary>
        /// Starts (or switches) a fight with <paramref name="who"/>: the job queue pauses, like
        /// talking, and resumes after <see cref="StopFighting"/>. Nothing happens if this NPC is
        /// dead, talking, or <paramref name="who"/> is itself, dead or null.
        /// </summary>
        public virtual void StartFighting(Entity who)
        {
            if (who == null || who == Npc || who.IsDead) return;
            if (State is NPCState.Dead or NPCState.Talking) return;
            if (State == NPCState.Fighting && target == who) return;

            target = who;
            State = NPCState.Fighting;
            _chase = new Navigation.Chase(ChaseStopDistance);
            _outOfRange = 0;
        }

        /// <summary>The fight's over: back to the interrupted errand, or wandering.</summary>
        public virtual void StopFighting()
        {
            if (State != NPCState.Fighting) return;
            target = null;
            _chase = null;
            Npc.Direction = Vector2.Zero;
            Resume();
        }

        /// <summary>Where the chase is still headed, in cells; empty when not chasing. For the nav drawing.</summary>
        public IReadOnlyList<Vector2> ChasePath => _chase?.Remaining ?? (IReadOnlyList<Vector2>)Array.Empty<Vector2>();

        // Close enough to stop chasing, feet to feet. Reach decides when to swing; this only stops
        // the chase walking into them once it's there.
        private const float ChaseStopDistance = 0.4f;

        /// <summary>
        /// One frame of a fight: give up if the target's gone, dead or kept out of range; guard
        /// (if this brain wants to) against a swing coming its way; swing when in reach; otherwise
        /// chase. Virtual so a brain can fight its own way.
        /// </summary>
        protected virtual void UpdateFighting(float dt)
        {
            // Looked up through the level, like a follow: someone who left it can't be chased.
            // Not wired (tests with no world): the target as given.
            Entity who = Npc.FindPerson != null ? Npc.FindPerson(target.Id) : target;
            if (who == null || who.IsDead)
            {
                StopFighting();
                return;
            }

            _cooldown = Math.Max(0f, _cooldown - dt);
            Npc.Direction = Vector2.Zero;

            bool far = Vector2.Distance(Npc.Feet, who.Feet) > GiveUpDistance;
            _outOfRange = far ? _outOfRange + dt : 0f;
            if (_outOfRange >= GiveUpSeconds)
            {
                StopFighting();
                return;
            }

            // Mid-swing or guarding: rooted until it's over.
            if (Npc.Swing != null || Npc.IsGuarding) return;

            if (Coming(who) && ShouldDefend(who))
            {
                Npc.FaceTowards(who.Bounds.Center);
                Npc.StartGuard(DefendSeconds);
                return;
            }

            var attacks = Npc.Attacks;
            if (attacks.Count > 0 && InReach(attacks[_nextAttack % attacks.Count], who))
            {
                Npc.FaceTowards(who.Bounds.Center);
                if (_cooldown <= 0f)
                {
                    Npc.StartSwing(attacks[_nextAttack % attacks.Count]);
                    _nextAttack++;
                    // Counted from the start of the swing, so it covers the swing as well.
                    _cooldown = attacks[(_nextAttack - 1) % attacks.Count].Duration + AttackCooldown;
                }
                return;
            }

            switch (_chase.Step(Npc.Location?.Nav, Npc.Feet, who.Feet, dt, out Vector2 heading))
            {
                case Navigation.RouteStep.Walking:
                    Npc.Direction = heading * ChaseSpeedFactor;   // Entity.Move steps Direction * Speed
                    break;
                case Navigation.RouteStep.GaveUp:
                    // No way through: as good as out of range. Plan afresh next frame.
                    _outOfRange += dt;
                    _chase = new Navigation.Chase(ChaseStopDistance);
                    break;
            }
        }

        // Whether `who` has just started a swing that would hit this NPC: a new one, still
        // winding up, aimed so it reaches. Each enemy swing is only considered once.
        private bool Coming(Entity who)
        {
            var swing = who.Swing;
            if (swing == null || swing == _swingSeen || swing.Phase != Combat.Swing.SwingPhase.Windup) return false;
            if (!swing.HitboxFrom(who.Bounds.Center).Intersects(Npc.Bounds)) return false;
            _swingSeen = swing;
            return true;
        }

        /// <summary>
        /// Whether to guard against a swing <paramref name="who"/> just started at this NPC. Asked
        /// once per swing. Base: never; KnightBrain sometimes does.
        /// </summary>
        protected virtual bool ShouldDefend(Entity who) => false;

        // Would `attack`, thrown now towards `who`, catch them? The real hitbox, so reach is
        // exactly what the swing would do.
        private bool InReach(Combat.AttackData attack, Entity who)
        {
            var facing = Entity.FacingOf(who.Bounds.Center - Npc.Bounds.Center);
            return new Combat.Swing(attack, Npc, facing).HitboxFrom(Npc.Bounds.Center).Intersects(who.Bounds);
        }

        /// <summary>Healed back from 0: carry on like after a conversation.</summary>
        public virtual void Revive() => Resume();

        // Back to what it was doing: an interrupted errand if one is waiting, otherwise wandering.
        private void Resume()
        {
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
                case NPCState.Dead:
                    break;
                case NPCState.Fighting:
                    UpdateFighting(dt);
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
                StartConversation = Npc.StartConversation,
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
                    "follow" => OtherPerson(spec, transcript) is Participant who ? new FollowJob(who.Id) : null,
                    "talk_to" => OtherPerson(spec, transcript) is Participant who
                        ? new TalkToJob(who.Id, string.IsNullOrWhiteSpace(spec.reason) ? null : spec.reason.Trim())
                        : null,
                    "patrol" => MakePatrol(spec, location),
                    _       => null,                    // unknown type -> ignore
                };
                if (job != null) Enqueue(job);
            }

            // 4. Resulting state: queued work always wins; otherwise honour the mood. (A decision
            // that comes back after its NPC died doesn't raise them, or one mid-fight stop it:
            // the queue runs once the fight's over.)
            if (State is NPCState.Dead or NPCState.Fighting) return;
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

        // The places that exist, in order, up to MaxPatrolStops. Fewer than two isn't a patrol
        // (one is a go_to): no job.
        private IJob MakePatrol(TaskSpecDto spec, Location location)
        {
            var stops = (spec.targets ?? Array.Empty<string>())
                .Select(name => location?.Poi(name))
                .Where(poi => poi != null)
                .Take(MaxPatrolStops)
                .Select(poi => poi.StandingSpot())
                .ToList();
            return stops.Distinct().Count() < 2 ? null : new PatrolJob(stops, Math.Clamp(spec.seconds, 0f, 60f));
        }

        // The task's target, if it's someone else from the conversation, by name: the transcript
        // says who was there and their ids. No transcript, a stranger, or the NPC itself: null.
        private Participant OtherPerson(TaskSpecDto spec, Transcript transcript)
        {
            string name = spec.target?.Trim();
            return transcript?.Participants.FirstOrDefault(p =>
                p.Id != Npc.Id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }


    }
}
