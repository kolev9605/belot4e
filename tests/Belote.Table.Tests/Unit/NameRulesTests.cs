using Belote.Table.Rooms;

namespace Belote.Table.Tests;

public class NameRulesTests
{
    private static readonly string[] None = Array.Empty<string>();

    [Theory]
    [InlineData("  Ivo  ", "Ivo")]
    [InlineData("Мира", "Мира")]
    [InlineData("a", "a")]
    public void A_valid_name_is_trimmed_and_accepted(string input, string expected)
    {
        var result = NameRules.Validate(input, None, 20);
        Assert.True(result.Ok);
        Assert.Equal(expected, result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void An_empty_name_is_invalid(string? input)
    {
        var result = NameRules.Validate(input, None, 20);
        Assert.False(result.Ok);
        Assert.Equal("NameInvalid", result.Code);
    }

    [Fact]
    public void A_name_longer_than_the_limit_is_invalid_and_exactly_the_limit_is_fine()
    {
        Assert.True(NameRules.Validate(new string('x', 20), None, 20).Ok);
        var result = NameRules.Validate(new string('x', 21), None, 20);
        Assert.False(result.Ok);
        Assert.Equal("NameInvalid", result.Code);
    }

    [Fact]
    public void Control_characters_are_invalid()
    {
        var result = NameRules.Validate("Iv\u0007o", None, 20);
        Assert.False(result.Ok);
        Assert.Equal("NameInvalid", result.Code);
    }

    [Theory]
    [InlineData("ivo")]
    [InlineData("IVO")]
    [InlineData(" Ivo ")]
    public void A_name_already_used_in_the_room_is_taken_ignoring_case(string input)
    {
        var result = NameRules.Validate(input, new[] { "Ivo", "Mira" }, 20);
        Assert.False(result.Ok);
        Assert.Equal("NameTaken", result.Code);
    }
}
