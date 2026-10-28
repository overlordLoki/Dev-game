
using System.Collections.Generic;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    public enum JobState
    {
        Inactive,
        Active,
        Completed,
        Failed,      // gave up (no way through, or stuck too long); dropped like Completed
    }
    public interface IJob
    {
        JobState State { get; set; }
        void Execute(JobContext context);
        
    }
    public sealed class Blackboard
    {
        private readonly Dictionary<string, object?> _data = new();

        public void Set<T>(string key, T value) => _data[key] = value;

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var v) && v is T t) { value = t; return true; }
            value = default!;
            return false;
        }

        public T Get<T>(string key, T fallback = default!) =>
            TryGet<T>(key, out var v) ? v : fallback;
    }

    public sealed class JobContext
    {
        public required Entity Owner { get; init; }
        public required Blackboard Blackboard { get; init; }
        public float DeltaTime { get; init; }
        // The owner's level's walkable cells, for jobs that route round obstacles. Null when
        // the owner isn't in a level (tests): jobs fall back to walking straight.
        public Navigation.NavGrid Nav { get; init; }
        // How the owner tells the world something happened (EventLog.Post). Null when it isn't
        // wired to a world (tests that don't care): nothing is posted.
        public System.Action<GameWorld.GameEvent> Post { get; init; }
    }
}