using System;
using System.Collections.Generic;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Entities.brains.Task;
using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities
{

    /// <summary>
    /// Decides how an NPC moves. NPC owns the body (position, push-out collision,
    /// animation); the brain owns the heading - where to go and how to react when
    /// something gets in the way.
    /// </summary>
    public abstract class NpcBrain
    {
        public List<Memory> Memories { get; } = new List<Memory>();
        public Entity target { get; set; } = null; //use for later AI, like following the player or attacking them
        //State machine for NPCs
        public enum NPCState
        {
            Idle,
            Walking,
            Talking,
            Tasked, //for future AI, like following the player or going to a specific location
        }
        public Journal Journal { get; } = new Journal();
        public Queue<ITask> TaskQueue { get; } = new Queue<ITask>();
        public NPCState State { get; protected set; } = NPCState.Idle;
        // One blackboard per NPC, reused across every tick and every task, so tasks can
        // share scratch state (a GoTo's target cell, "have I started") between frames.
        private readonly Blackboard _blackboard = new();

        /// <summary>Called once when the NPC is created, to set its starting heading.</summary>
        public virtual void Start(NPC npc) {}

        /// <summary>Hand the NPC a task to run; switches it into the Tasked state.</summary>
        public void Enqueue(ITask task)
        {
            TaskQueue.Enqueue(task);
            State = NPCState.Tasked;
        }

        public virtual void StartTalking(NPC npc)
        {
            State = NPCState.Talking;
            //face the player
            npc.Direction = Vector2.Zero;
        }
        public virtual void StopTalking(NPC npc)
        {
            // Resume an interrupted errand if one is waiting; otherwise go back to wandering.
            State = TaskQueue.Count > 0 ? NPCState.Tasked : NPCState.Walking;
            if (State == NPCState.Walking)
            {
                float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                npc.Direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
        }

        /// <summary>
        /// The fixed per-frame flow, shared by every brain: talking stands still, a tasked
        /// NPC ticks its queue, and anything else defers to the subclass's default behaviour.
        /// Subclasses override <see cref="UpdateDefault"/>, not this, so the task machinery
        /// can never be accidentally shadowed.
        /// </summary>
        public void Update(NPC npc, GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            switch (State)
            {
                case NPCState.Talking:
                    // Stand still while talking.
                    break;
                case NPCState.Tasked:
                    TickTasks(npc, dt);
                    break;
                default: // Idle / Walking
                    UpdateDefault(npc, gameTime);
                    break;
            }
        }

        /// <summary>A brain's own idle/wander behaviour. Base does nothing; WanderBrain walks.</summary>
        protected virtual void UpdateDefault(NPC npc, GameTime gameTime) { }

        // Runs the task at the front of the queue until it reports Completed, then moves on.
        // A task may take many ticks; Peek (not Dequeue) keeps it at the front until it's done.
        private void TickTasks(NPC npc, float dt)
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
                Owner = npc,
                Blackboard = _blackboard,
                DeltaTime = dt,
            });

            if (task.State == TaskState.Completed) TaskQueue.Dequeue();
        }

        /// <summary>
        /// Called after the NPC was pushed out of another entity. <paramref name="horizontal"/>
        /// is true when the push was along X. Default: do nothing, just stay pushed out.
        /// </summary>
        public virtual void OnBlocked(NPC npc, bool horizontal) { }
    }
}
