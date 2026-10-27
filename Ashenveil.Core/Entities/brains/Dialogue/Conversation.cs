using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ashenveil.Core.Entities.brains.Memorys;
using Ashenveil.Core.Screens;
using Microsoft.Xna.Framework;
using Ashenveil.Core.GameWorld.Locations;
namespace Ashenveil.Core.Entities.brains.Dialogue
{
    /// <summary>One entry in the transcript. A null Speaker is a note from the game, not something anyone said.</summary>
    public record ChatMessage(string Speaker, string Text);

    public class Conversation
    {
        // Shown in place of a reply when the API is down or answers with nothing.
        public const string NoReply = "(no answer)";

        // Everything said so far, oldest first. Only ever grows.
        public List<ChatMessage> Messages { get; } = new();
        public Entity[] Participants { get; set; }
        public bool IsFinished { get; private set; }
        public Vector2 CenterOfMass { get; set; }
        public Location Location { get; set; }

        // Who is present and where, plus the history so far. Re-sent on every turn so the
        // LLM always replies with the whole conversation in view.
        private ConversationRequestDto _request;
        private Func<ConversationRequestDto, Task<ChatMessage[]>> _send;
        private Task<ChatMessage[]> _pending;
        // True while we're still waiting on the LLM. One turn at a time: the chat box
        // should not let the player send again until the reply has arrived.
        public bool IsLoading => _pending != null;

        /// <summary>
        /// A conversation between two or more entities. The first entity is the one that starts the conversation.
        /// <paramref name="send"/> is how a reply is asked for (DialogueApi.RequestAsync in the game); it is
        /// handed in rather than called directly so the conversation never talks to the API itself.
        /// </summary>
        public Conversation(Entity[] entities, Location location, ConversationRequestDto request,
                            Func<ConversationRequestDto, Task<ChatMessage[]>> send)
        {
            Participants = entities;
            CenterOfMass = GetAveragePosition(entities);
            Location = location;
            _request = request;
            _send = send;
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

        // Puts a line in the transcript without asking for a reply.
        public void Add(string speaker, string text)
        {
            Messages.Add(new ChatMessage(speaker, text));
            _request.history.Add($"{speaker}: {text}");
        }

        // A note from the game ("You approach Sir Aldric."): shown in the chat, but nobody
        // said it, so it stays out of the LLM's history and out of the transcript.
        public void Note(string text) => Messages.Add(new ChatMessage(null, text));

        // Says a line and asks the LLM to answer it. Ignored while a reply is still on its way.
        public void Say(string speaker, string text)
        {
            if (IsLoading || IsFinished) return;
            Add(speaker, text);
            _pending = _send(_request);
        }

        // Call every frame while the conversation is on screen.
        public void Poll()
        {
            if (_pending == null) return;
            if (!_pending.IsCompleted) return;          // still waiting

            // API down or empty → a note in the transcript, and the player can try again.
            var reply = _pending.IsCompletedSuccessfully ? _pending.Result : null;
            _pending = null;
            if (reply == null || reply.Length == 0)
            {
                Note(NoReply);
                return;
            }
            foreach (var message in reply) Add(message.Speaker, message.Text);
        }

        // Walking away: the world lets everyone go back to what they were doing.
        public void End() => IsFinished = true;

        /// <summary>
        /// A plain-data snapshot of this conversation for the cold tier of memory. Copies
        /// the lines, so later messages don't change it. Game notes like "(no answer)" (null
        /// speaker) are left out - only what was actually said is kept.
        /// </summary>
        public Transcript ToTranscript(int id, int day) => new Transcript(
            id,
            day,
            Location?.Name,
            Participants.Select(p => new Participant(p.Id, NameOf(p))).ToList(),
            Messages.Where(m => m.Speaker != null)
                    .Select(m => $"{m.Speaker}: {m.Text}")
                    .ToList());

        /// <summary>
        /// What someone in a conversation is called right now. Only NPCs and players have
        /// names; anything else (tests use plain entities) is shown by its id.
        /// </summary>
        public static string NameOf(Entity who) => who switch
        {
            NPC npc => npc.Name,
            Player player => player.Name,
            _ => $"#{who.Id}",
        };
    }
}
