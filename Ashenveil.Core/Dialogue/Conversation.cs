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

    }
}