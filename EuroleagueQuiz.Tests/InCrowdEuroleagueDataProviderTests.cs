using System.Net;
using System.Text;
using EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;

namespace EuroleagueQuiz.Tests;

public sealed class InCrowdEuroleagueDataProviderTests
{
    [Fact]
    public async Task GetTeamPlayersAsync_SkipsMalformedEntries_AndReadsValidPlayers()
    {
        const string json = """
        {
          "data": [
            null,
            {
              "type": "J",
              "externalId": "membership-without-person",
              "person": null
            },
            {
              "type": "J",
              "externalId": "membership-1",
              "person": {
                "code": "player-1",
                "displayName": "Player One",
                "country": null
              }
            }
          ]
        }
        """;

        using var http = new HttpClient(new JsonMessageHandler(json))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var provider = new InCrowdEuroleagueDataProvider(http);

        var players = await provider.GetTeamPlayersAsync("E2025", "ABC", CancellationToken.None);

        var player = Assert.Single(players);
        Assert.Equal("player-1", player.ExternalId);
        Assert.Equal("Player One", player.DisplayName);
        Assert.Null(player.Nationality);
        Assert.Null(player.NationalityCode);
    }

    private sealed class JsonMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
