using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Ashenveil.Core.Dialogue
{
    // Mirrors main.py's ConversationRequest / ConversationResponse.
    public class ParticipantDto
    {
        public string name { get; set; }
        public string persona { get; set; } 
        public bool speaks { get; set; } = true; 
    }
    public class ConversationRequestDto
    {
         public List<ParticipantDto> participants { get; set; }
         public string location { get; set; }
         public string topic { get; set; }
         public List<string> history { get; set; } = new();  // lines already said, to continue from
         public int max_lines { get; set; } = 4;
    }
    public class TurnDto
    {
        public string speaker { get; set; }
        public string text { get; set; }
    }
    public class ConversationResponseDto 
    {
        public string[] lines { get; set; } 
        public TurnDto[] turns { get; set; }   // the same lines, with who said each one
    }

    public static class DialogueApi
    {
        private static readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:8000") };

        public static async Task<ChatMessage[]> RequestAsync(ConversationRequestDto req)
        {
            var res = await _http.PostAsJsonAsync("/conversation", req);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<ConversationResponseDto>();
            // A 200 with no turns comes back as empty, which Conversation.Poll treats as "no answer".
            return body?.turns?.Select(t => new ChatMessage(t.speaker, t.text)).ToArray() ?? Array.Empty<ChatMessage>();
        }
    }
}