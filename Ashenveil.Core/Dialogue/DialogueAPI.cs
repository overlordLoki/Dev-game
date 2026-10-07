using System;
using System.Collections.Generic;
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
    public class ConversationResponseDto 
    {
        public string[] lines { get; set; } 
    }

    public static class DialogueApi
    {
        private static readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:8000") };

        public static async Task<string[]> RequestAsync(ConversationRequestDto req)
        {
            var res = await _http.PostAsJsonAsync("/conversation", req);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<ConversationResponseDto>();
            return body.lines;
        }
    }
}