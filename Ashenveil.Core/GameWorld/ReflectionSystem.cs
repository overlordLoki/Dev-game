using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Entities.brains.Dialogue;
using Ashenveil.Core.Entities.brains.Memorys;

namespace Ashenveil.Core.GameWorld
{
    /// <summary>
    /// The day-end reflection: each NPC looks back on a day with one LLM call, and writes a
    /// diary entry (cold) and a few lasting facts (hot). Built like the ConversationSystem's
    /// decisions - fire the call, hold the Task, apply the answer on a later frame - but queued,
    /// so a day turning over in a crowded world doesn't fire every call at once.
    ///
    ///   1. Queue  - when a day turns over, every NPC with anything from the day before; after a
    ///               load, any NPC behind on its days (<see cref="CatchUp"/>).
    ///   2. Start  - at most <see cref="MaxInFlight"/> calls at a time, from the front of the queue.
    ///   3. Apply  - on the main thread, from <see cref="Update"/>. A failed call is dropped and
    ///               the day goes unreflected, as with decisions.
    ///
    /// It's given the NPCs it needs rather than the World, and its LLM call is handed in, so it
    /// can be tested with fakes.
    /// </summary>
    public class ReflectionSystem
    {
        /// <summary>The most reflection calls waiting on the LLM at once.</summary>
        public const int MaxInFlight = 2;

        /// <summary>How many days back a load catches up on, at most.</summary>
        public const int CatchUpDays = 2;

        private readonly Func<ReflectionRequestDto, Task<ReflectionDto>> _request;

        // Waiting to start, oldest first.
        private readonly List<(NPC npc, int day)> _queue = new();

        // Asked for but not back. Each keeps the event memories the request was written from,
        // so exactly those leave the hot list when it lands - not ones seen since.
        private readonly List<(NPC npc, int day, List<Memory> events, Task<ReflectionDto> task)> _inFlight = new();

        /// <param name="request">How a reflection is asked for. Defaults to the real API.</param>
        public ReflectionSystem(Func<ReflectionRequestDto, Task<ReflectionDto>> request = null)
        {
            _request = request ?? DialogueApi.RequestReflectionAsync;
        }

        /// <summary>Reflections queued but not yet asked for.</summary>
        public int Queued => _queue.Count;

        /// <summary>Reflections asked for but not yet back.</summary>
        public int InFlight => _inFlight.Count;

        // ------------------------------------------------------------------ 1. queue

        /// <summary>
        /// Queues <paramref name="npc"/> to reflect on <paramref name="day"/>. False (and nothing
        /// queued) if it's already queued or in flight for that day.
        /// </summary>
        public bool Queue(NPC npc, int day)
        {
            if (_queue.Contains((npc, day)) || _inFlight.Any(f => f.npc == npc && f.day == day)) return false;
            _queue.Add((npc, day));
            return true;
        }

        /// <summary>
        /// A new day has begun (GameClock.DayStarted): queue every NPC that had anything happen
        /// to it the day before and hasn't reflected on it yet.
        /// </summary>
        public void DayStarted(int newDay, IEnumerable<NPC> npcs)
        {
            foreach (var npc in npcs) QueueIfDue(npc, newDay - 1);
        }

        /// <summary>
        /// After a load: a reflection in flight when the game was saved was lost, so queue any NPC
        /// behind on its days again, back to <see cref="CatchUpDays"/> days ago, oldest first.
        /// Today isn't reflected on - it isn't over.
        /// </summary>
        public void CatchUp(IEnumerable<NPC> npcs, int today)
        {
            foreach (var npc in npcs)
                for (int day = Math.Max(npc.Journal.LastReflectedDay + 1, today - CatchUpDays); day < today; day++)
                    QueueIfDue(npc, day);
        }

        private void QueueIfDue(NPC npc, int day)
        {
            if (day > npc.Journal.LastReflectedDay && npc.Journal.HasAnythingOn(day)) Queue(npc, day);
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>Call once a frame: applies reflections that have come back, then starts queued ones.</summary>
        public void Update()
        {
            ApplyArrived();
            while (_inFlight.Count < MaxInFlight && _queue.Count > 0)
            {
                var (npc, day) = _queue[0];
                _queue.RemoveAt(0);
                Start(npc, day);
            }
        }

        // ------------------------------------------------------------------ 2. start

        private void Start(NPC npc, int day)
        {
            var events = npc.Journal.EventsOn(day).ToList();
            var req = new ReflectionRequestDto
            {
                npc_name = npc.Name,
                persona = npc.Persona,
                date = GameDate.OnDay(day).Display,
                events = events.Select(m => m.Text).ToList(),
                conversations = npc.Journal.Transcripts
                    .Where(t => t.Day == day && t.Summary != null)
                    .Select(t => t.Summary)
                    .ToList(),
            };
            _inFlight.Add((npc, day, events, _request(req)));
        }

        // ------------------------------------------------------------------ 3. apply

        // Walks backwards so removing an entry doesn't skip the next one.
        private void ApplyArrived()
        {
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var (npc, day, events, task) = _inFlight[i];
                if (!task.IsCompleted) continue;
                if (task.IsCompletedSuccessfully) npc.ApplyReflection(task.Result, day, events);
                _inFlight.RemoveAt(i);
            }
        }
    }
}
