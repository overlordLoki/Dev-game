
using System.Collections.Generic;

namespace Ashenveil.Core.Entities.brains.Task
{
    public enum TaskState
    {
        Inactive,
        Active,
        Completed,
    }
    public interface ITask
    {
        TaskState State { get; set; }
        void Execute(TaskContext context);
        
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

    public sealed class TaskContext
    {
        public required Entity Owner { get; init; }
        public required Blackboard Blackboard { get; init; }
        public float DeltaTime { get; init; }
        // The owner's level's walkable cells, for tasks that route round obstacles. Null when
        // the owner isn't in a level (tests): tasks fall back to walking straight.
        public Navigation.NavGrid Nav { get; init; }
    }
}