using Microsoft.Xna.Framework;

namespace Ashenveil.Core.Entities.brains.Jobs
{
    public class WaitJob : IJob
    {
        public JobState State { get; set; } = JobState.Inactive;
        private readonly float _seconds;
        private float _elapsed;

        public WaitJob(float seconds) => _seconds = seconds;

        // How much of the wait is still to go. Saving keeps this, so a load waits out the rest
        // rather than starting the wait over.
        public float SecondsLeft => System.Math.Max(0f, _seconds - _elapsed);

        public void Execute(JobContext context)
        {
            ((NPC)context.Owner).Direction = Vector2.Zero;   // stand still while waiting
            _elapsed += context.DeltaTime;
            if (_elapsed >= _seconds) State = JobState.Completed;
        }
    }
}