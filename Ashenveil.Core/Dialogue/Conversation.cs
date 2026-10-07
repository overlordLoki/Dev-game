using System.Linq;
using System.Threading.Tasks;
using Ashenveil.Core.Entities;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
namespace Ashenveil.Core.Dialogue
{
    public class Conversation
    {
        public string[] Lines { get; set; }
        public Entity[] Participants { get; set; }
        public int CurrentLineIndex { get; set; } = 0;
        public bool IsFinished => CurrentLineIndex >= Lines.Length;
        public Vector2 CenterOfMass { get; set; }
        public Location Location { get; set; }
        private Task<string[]> _pending;
        private string _greeting;
        // True while we're still waiting on the LLM. The chat box should not let the
        // player advance past the greeting until the rest of the lines have arrived.
        public bool IsLoading => _pending != null;

        /// <summary>
        /// A conversation between two or more entities. The first entity is the one that starts the conversation.
        /// </summary>
        public Conversation(Entity[] entities, string[] lines, Location location)
        {
            Participants = entities;
            Lines = lines;
            CenterOfMass = GetAveragePosition(entities);
            Location = location;
        }

        //based the position of of all participants, find the center position of all participants. usful for getting npc to face the direction of the converstation.
        public Vector2 GetAveragePosition(Entity[] entities)
        {
            Vector2 averagePosition = Vector2.Zero;
            foreach (var entity in entities)
            {
                averagePosition += entity.Position;
            }
            averagePosition /= entities.Length;
            return averagePosition;
        }
        public void StartLoading(Task<string[]> request, string greeting)
        {
            _greeting = greeting;
            _pending = request;
            Lines = new[] { greeting };   // greeting shows at once, no "..." wait
        }

        // Call every frame from World.Update.
        public void Poll()
        {
            if (_pending == null) return;
            if (!_pending.IsCompleted) return;          // still waiting — keep showing the greeting

            // Greeting stays as the opening line; generated lines (if any) follow it.
            // API down or empty → the greeting is the whole conversation.
            Lines = _pending.IsCompletedSuccessfully && _pending.Result.Length > 0
                ? new[] { _greeting }.Concat(_pending.Result).ToArray()
                : new[] { _greeting };
            _pending = null;
        }

    }
}