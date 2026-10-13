using System;
using System.Collections.Generic;
using System.Linq;

namespace Belote.Table.Rooms;

/// <summary>The outcome of validating a display name; <see cref="Code"/> is stable and machine-readable.</summary>
public sealed record NameResult(bool Ok, string Name, string? Code)
{
    public static NameResult Valid(string name) => new(true, name, null);

    public static NameResult Invalid(string code) => new(false, string.Empty, code);
}

/// <summary>Display-name rules shared by creating, joining and renaming (FR-010).</summary>
public static class NameRules
{
    public const string InvalidCode = "NameInvalid";
    public const string TakenCode = "NameTaken";

    /// <param name="input">The name as typed.</param>
    /// <param name="taken">Names already used by other participants of the room.</param>
    /// <param name="maxLength">The longest accepted name, in characters.</param>
    public static NameResult Validate(string? input, IEnumerable<string> taken, int maxLength)
    {
        var name = (input ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > maxLength || name.Any(char.IsControl))
        {
            return NameResult.Invalid(InvalidCode);
        }

        if (taken.Any(t => string.Equals(t.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            return NameResult.Invalid(TakenCode);
        }

        return NameResult.Valid(name);
    }
}
