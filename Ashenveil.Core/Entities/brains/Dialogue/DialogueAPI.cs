using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ashenveil.Core.Config;

namespace Ashenveil.Core.Entities.brains.Dialogue
{
    // Mirrors main.py's ConversationRequest / ConversationResponse.
    public class ParticipantDto
    {
        public string name { get; set; }
        public string persona { get; set; }
        public bool speaks { get; set; } = true;
        public List<string> memories { get; set; } = new();  // what they remember from before
        public List<string> past_conversations { get; set; } = new();  // one-line recaps of earlier chats
        public List<string> events_today { get; set; } = new();  // what they saw happen today, oldest first
        public List<string> recent_days { get; set; } = new();   // diary entries for the last few days, oldest first
        public List<string> rumours { get; set; } = new();       // what others told them today, as hearsay
        public List<string> attitudes { get; set; } = new();     // how they feel about each other person there
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

    /// <summary>What GET /health said, boiled down to what the player needs to know.</summary>
    public enum HealthStatus
    {
        Ok,              // backend and Ollama up, model installed
        ModelMissing,    // backend and Ollama up, but the model isn't pulled
        OllamaDown,      // backend answered, Ollama didn't
        Unreachable,     // nothing answered at that address (or it timed out)
        Error,           // something answered, but not like the backend does
    }

    /// <summary>The result of a health check: the status, the model asked for, and a line to show.</summary>
    public record HealthResult(HealthStatus Status, string Model, string Message)
    {
        public bool Ok => Status == HealthStatus.Ok;
    }

    // Mirrors main.py's /health reply.
    public class HealthDto
    {
        public string ollama { get; set; }
        public string model { get; set; }
        public bool model_installed { get; set; }
    }

    /// <summary>
    /// The game's side of the Python backend. Every request goes to <see cref="BaseUrl"/>, which
    /// comes from the settings and can change while the game runs: the full address is built per
    /// request (rather than fixed in HttpClient.BaseAddress, which can't change after the first
    /// request), so the next request after a change goes to the new address.
    /// </summary>
    public static class DialogueApi
    {
        private static HttpClient _http = new();
        private static volatile string _baseUrl = GameSettings.DefaultBackendUrl;

        /// <summary>Where requests go, without a trailing slash. Set by AppSettings.Apply.</summary>
        public static string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = BackendUrl.TryNormalize(value, out var url, out var error)
                ? url : throw new ArgumentException(error, nameof(value));
        }

        /// <summary>
        /// Sends every request through <paramref name="handler"/> instead of the network - the
        /// tests' way in, like the fake sendChat / requestDecision. Null goes back to the network.
        /// </summary>
        public static void UseHandler(HttpMessageHandler handler)
        {
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
        }

        public static async Task<ChatMessage[]> RequestAsync(ConversationRequestDto req)
        {
            var res = await _http.PostAsJsonAsync(BaseUrl + "/conversation", req);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<ConversationResponseDto>();
            // A 200 with no turns comes back as empty, which Conversation.Poll treats as "no answer".
            return body?.turns?.Select(t => new ChatMessage(t.speaker, t.text)).ToArray() ?? Array.Empty<ChatMessage>();
        }

        // Asked for once when a conversation ends: what the NPC remembers and does next.
        // Null on a 200 with an empty body; ApplyDecision treats null as "nothing changed".
        public static async Task<NpcDecisionDto> RequestDecisionAsync(DecisionRequestDto req)
        {
            var res = await _http.PostAsJsonAsync(BaseUrl + "/decision", req);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<NpcDecisionDto>();
        }

        // Asked for once per NPC per day, at day end: a diary entry and lasting facts.
        // Null on a 200 with an empty body; ApplyReflection treats null as "nothing changed".
        public static async Task<ReflectionDto> RequestReflectionAsync(ReflectionRequestDto req)
        {
            var res = await _http.PostAsJsonAsync(BaseUrl + "/reflection", req);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<ReflectionDto>();
        }

        /// <summary>How long a health check waits. /health itself gives Ollama 5 seconds.</summary>
        public static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(8);

        /// <summary>
        /// Calls GET /health at <paramref name="baseUrl"/> (or <see cref="BaseUrl"/> if null) - the
        /// settings screen tests an address before it's saved. Never throws: whatever goes wrong
        /// comes back as a result with a message to show.
        /// </summary>
        public static async Task<HealthResult> CheckHealthAsync(string baseUrl = null)
        {
            baseUrl ??= BaseUrl;
            using var timeout = new CancellationTokenSource(HealthTimeout);
            try
            {
                var res = await _http.GetAsync(baseUrl + "/health", timeout.Token);

                // main.py answers 503 when it's up but can't reach Ollama.
                if (res.StatusCode == HttpStatusCode.ServiceUnavailable)
                    return new HealthResult(HealthStatus.OllamaDown, null,
                        "The backend is up, but it can't reach Ollama. Is Ollama running?");
                if (!res.IsSuccessStatusCode)
                    return new HealthResult(HealthStatus.Error, null,
                        $"{baseUrl} answered {(int)res.StatusCode} {res.ReasonPhrase}. Is that the backend?");

                var body = await res.Content.ReadFromJsonAsync<HealthDto>(timeout.Token);
                if (body?.model == null)
                    return new HealthResult(HealthStatus.Error, null, $"{baseUrl} answered, but not like the backend does.");
                if (!body.model_installed)
                    return new HealthResult(HealthStatus.ModelMissing, body.model,
                        $"Connected, but Ollama doesn't have {body.model}. Run: ollama pull {body.model}");
                return new HealthResult(HealthStatus.Ok, body.model, $"Connected. Model: {body.model}");
            }
            catch (OperationCanceledException)
            {
                return new HealthResult(HealthStatus.Unreachable, null, $"No answer from {baseUrl} in {HealthTimeout.TotalSeconds:0} seconds.");
            }
            catch (HttpRequestException e)
            {
                return new HealthResult(HealthStatus.Unreachable, null, $"Can't reach {baseUrl}: {e.Message}");
            }
            catch (Exception e) when (e is JsonException or NotSupportedException)
            {
                return new HealthResult(HealthStatus.Error, null, $"{baseUrl} answered, but not like the backend does.");
            }
        }
    }
}