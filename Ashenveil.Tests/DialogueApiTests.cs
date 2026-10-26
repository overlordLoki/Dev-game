using System.Net;
using System.Text;
using Ashenveil.Core.Config;
using Ashenveil.Core.Entities.brains.Dialogue;

namespace Ashenveil.Tests;

/// <summary>
/// DialogueApi with a fake HttpMessageHandler in place of the network: where requests go
/// after the address changes, and what each kind of /health answer turns into.
/// </summary>
public class DialogueApiTests : IDisposable
{
    private readonly FakeHandler _handler = new();

    public DialogueApiTests() => DialogueApi.UseHandler(_handler);

    public void Dispose()
    {
        // Static, so put it back for the next test.
        DialogueApi.UseHandler(null);
        DialogueApi.BaseUrl = GameSettings.DefaultBackendUrl;
    }

    private static ConversationRequestDto Request() => new()
    {
        participants = new() { new ParticipantDto { name = "A", persona = "p" } },
        location = "here",
        topic = "t",
    };

    // ------------------------------------------------------------------ the address

    [Fact]
    public async Task Requests_go_to_the_default_address_to_start_with()
    {
        await DialogueApi.RequestAsync(Request());

        Assert.Equal("http://127.0.0.1:8000/conversation", _handler.Requests.Single().ToString());
    }

    [Fact]
    public async Task After_the_address_changes_the_next_request_goes_to_the_new_one()
    {
        await DialogueApi.RequestAsync(Request());

        DialogueApi.BaseUrl = "http://192.168.1.20:9000";
        await DialogueApi.RequestAsync(Request());
        await DialogueApi.RequestDecisionAsync(new DecisionRequestDto());

        Assert.Equal(new[]
        {
            "http://127.0.0.1:8000/conversation",
            "http://192.168.1.20:9000/conversation",
            "http://192.168.1.20:9000/decision",
        }, _handler.Requests.Select(u => u.ToString()));
    }

    [Fact]
    public async Task An_address_with_a_path_keeps_it()
    {
        DialogueApi.BaseUrl = "example.com/ashenveil/";

        await DialogueApi.RequestAsync(Request());

        Assert.Equal("http://example.com/ashenveil/conversation", _handler.Requests.Single().ToString());
    }

    [Fact]
    public void A_bad_address_is_refused_and_the_old_one_kept()
    {
        Assert.Throws<ArgumentException>(() => DialogueApi.BaseUrl = "ftp://nope");
        Assert.Equal(GameSettings.DefaultBackendUrl, DialogueApi.BaseUrl);
    }

    // ------------------------------------------------------------------ /health

    [Fact]
    public async Task Health_with_the_model_installed_is_ok_and_names_it()
    {
        _handler.Reply(HttpStatusCode.OK, """{ "ollama": "ok", "model": "qwen3:8b", "model_installed": true, "models": ["qwen3:8b"] }""");

        var r = await DialogueApi.CheckHealthAsync();

        Assert.Equal(HealthStatus.Ok, r.Status);
        Assert.Equal("qwen3:8b", r.Model);
        Assert.Contains("qwen3:8b", r.Message);
        Assert.Equal("http://127.0.0.1:8000/health", _handler.Requests.Single().ToString());
    }

    [Fact]
    public async Task Health_without_the_model_says_how_to_get_it()
    {
        _handler.Reply(HttpStatusCode.OK, """{ "ollama": "ok", "model": "qwen3:8b", "model_installed": false, "models": [] }""");

        var r = await DialogueApi.CheckHealthAsync();

        Assert.Equal(HealthStatus.ModelMissing, r.Status);
        Assert.Contains("ollama pull qwen3:8b", r.Message);
    }

    [Fact]
    public async Task Health_503_means_the_backend_is_up_but_Ollama_isnt()
    {
        _handler.Reply(HttpStatusCode.ServiceUnavailable, """{ "detail": "Ollama not reachable" }""");

        Assert.Equal(HealthStatus.OllamaDown, (await DialogueApi.CheckHealthAsync()).Status);
    }

    [Fact]
    public async Task Health_when_nothing_answers_is_unreachable_and_says_where()
    {
        _handler.Fail(new HttpRequestException("Connection refused"));

        var r = await DialogueApi.CheckHealthAsync("http://10.0.0.9:8000");

        Assert.Equal(HealthStatus.Unreachable, r.Status);
        Assert.Contains("10.0.0.9:8000", r.Message);
    }

    [Fact]
    public async Task Health_that_times_out_is_unreachable()
    {
        _handler.Fail(new TaskCanceledException());

        Assert.Equal(HealthStatus.Unreachable, (await DialogueApi.CheckHealthAsync()).Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "not found")]
    [InlineData(HttpStatusCode.OK, "<html>a web page</html>")]
    [InlineData(HttpStatusCode.OK, """{ "something": "else" }""")]
    public async Task Health_from_something_that_isnt_the_backend_is_an_error(HttpStatusCode code, string body)
    {
        _handler.Reply(code, body);

        Assert.Equal(HealthStatus.Error, (await DialogueApi.CheckHealthAsync()).Status);
    }

    [Fact]
    public async Task Health_checks_the_address_its_given_without_changing_the_saved_one()
    {
        _handler.Reply(HttpStatusCode.OK, """{ "ollama": "ok", "model": "m", "model_installed": true }""");

        await DialogueApi.CheckHealthAsync("http://other:1");

        Assert.Equal("http://other:1/health", _handler.Requests.Single().ToString());
        Assert.Equal(GameSettings.DefaultBackendUrl, DialogueApi.BaseUrl);
    }

    /// <summary>Records each request's address and answers with whatever the test set up (an empty 200 by default).</summary>
    private class FakeHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();
        private Func<HttpResponseMessage> _answer = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        public void Reply(HttpStatusCode code, string body) =>
            _answer = () => new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        public void Fail(Exception e) => _answer = () => throw e;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_answer());
        }
    }
}
