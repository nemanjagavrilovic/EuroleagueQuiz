using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Tests;

public sealed class PlayerDtoTests
{
    [Fact]
    public void Comma_formatted_provider_name_is_output_as_given_name_then_family_name()
    {
        var player = new PlayerDto(313, "002543", "WESTERMANN, LEO", "LEO JOSEPH PAUL", "WESTERMANN",
            "France", "FRA", null, null);

        Assert.Equal("LEO WESTERMANN", player.DisplayName);
    }

    [Fact]
    public void Existing_given_name_then_family_name_is_not_changed()
    {
        var player = new PlayerDto(1, "1", "Mike James", "Mike", "James", null, null, null, null);

        Assert.Equal("Mike James", player.DisplayName);
    }

    [Fact]
    public void Persisted_daily_player_is_normalized_during_deserialization()
    {
        var player = JsonSerializer.Deserialize<PlayerDto>("""
            {"Id":313,"ExternalId":"002543","DisplayName":"WESTERMANN, LEO","FirstName":"LEO JOSEPH PAUL",
             "LastName":"WESTERMANN","Nationality":"France","NationalityCode":"FRA","ImageUrl":null,"CountryFlagUrl":null}
            """);

        Assert.NotNull(player);
        Assert.Equal("LEO WESTERMANN", player.DisplayName);
    }
}
