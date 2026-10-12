using System.Text.Json;

namespace Belote.Table;

/// <summary>JSON options shared by the hub and the tests: camelCase, nulls kept.</summary>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static RejectionDto ToDto(this Rejection rejection) =>
        new(Rejection.CategoryName(rejection.Category), rejection.Code, rejection.Detail);
}
