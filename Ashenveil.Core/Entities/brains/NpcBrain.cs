using System;
using System.Collections.Generic;
using System.Linq;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
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
            Tasked, //for future AI, like following the player or going to a specific location
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
        public Queue<ITask> TaskQueue { get; } = new Queue<ITask>();
        public NPCState State { get; protected set; } = NPCState.Idle;
        // One blackboard per NPC, reused across every tick and every task, so tasks can
        // share scratch state (a GoTo's target cell, "have I started") between frames.
        private readonly Blackboard _blackboard = new();

        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        protected virtual void Start() {}

        /// <summary>Hand the NPC a task to run; switches it into the Tasked state.</summary>
        public void Enqueue(ITask task)
        {
            TaskQueue.Enqueue(task);
            State = NPCState.Tasked;
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
            State = TaskQueue.Count > 0 ? NPCState.Tasked : NPCState.Walking;
            if (State == NPCState.Walking)
            {
                float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                Npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
        }

        /// <summary>
        /// The fixed per-frame flow, shared by every brain: talking stands still, a tasked
        /// NPC ticks its queue, and anything else defers to the subclass's default behaviour.
        /// Subclasses override <see cref="UpdateDefault"/>, not this, so the task machinery
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
                case NPCState.Tasked:
                    TickTasks(dt);
                    break;
                default: // Idle / Walking
                    UpdateDefault(gameTime);
                    break;
            }
        }

        /// <summary>A brain's own idle/wander behaviour. Base does nothing; WanderBrain walks.</summary>
        protected virtual void UpdateDefault(GameTime gameTime) { }

        // Runs the task at the front of the queue until it reports Completed, then moves on.
        // A task may take many ticks; Peek (not Dequeue) keeps it at the front until it's done.
        private void TickTasks(float dt)
        {
            if (TaskQueue.Count == 0)
            {
                State = NPCState.Walking;   // nothing left to do -> back to the default loop
                return;
            }

            ITask task = TaskQueue.Peek();
            if (task.State == TaskState.Inactive) task.State = TaskState.Active;

            task.Execute(new TaskContext
            {
                Owner = Npc,
                Blackboard = _blackboard,
                DeltaTime = dt,
            });

            if (task.State == TaskState.Completed) TaskQueue.Dequeue();
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

            // 1. Remember: each extracted fact becomes a discrete Memory (the hot tier), stamped
            // with the transcript it came from.
            // Facts already in the journal (ignoring case and edge whitespace) are skipped, so
            // repeats don't push real memories out of the injected window. Reworded duplicates
            // still get through; merging those is the consolidation pass (plan, Phase 6).
            foreach (var fact in (decision.memories ?? Array.Empty<string>())
                         .Where(m => !string.IsNullOrWhiteSpace(m))
                         .Select(m => m.Trim())
                         .Take(5))
            {
                bool known = Journal.Memories.Any(m =>
                    string.Equals(m.Text, fact, StringComparison.OrdinalIgnoreCase));
                if (!known) Journal.Add(new Memory(fact, MemoryKind.Conversation) { SourceId = transcript?.Id });
            }

            // 2. Optionally drop whatever the NPC was doing.
            if (decision.clear_current_tasks) TaskQueue.Clear();

            // 3. Turn each spec into a real task, dropping anything we can't honour.
            foreach (var spec in decision.tasks ?? Array.Empty<TaskSpecDto>())
            {
                ITask task = spec.type switch
                {
                    "go_to" => MakeGoTo(spec, location),
                    "wait"  => new WaitTask(Math.Clamp(spec.seconds, 0f, 600f)),
                    _       => null,                    // unknown type -> ignore
                };
                if (task != null) Enqueue(task);
            }

            // 4. Resulting state: queued work always wins; otherwise honour the mood.
            State = TaskQueue.Count > 0 ? NPCState.Tasked
                : decision.behaviour == "idle" ? NPCState.Idle
                : NPCState.Walking;
        }

        // A go_to survives only if its target names a real POI in this level.
        private ITask MakeGoTo(TaskSpecDto spec, Location location)
        {
            var poi = location?.Poi(spec.target);
            return poi == null ? null : new GoToTask(poi.StandingSpot());
        }


    }
}
