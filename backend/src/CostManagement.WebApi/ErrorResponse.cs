namespace CostManagement.WebApi;

/// <summary>Error response body for 400 / 404 / 409 ({ "error": "日本語のメッセージ" }).</summary>
/// <param name="Error">Message that can be shown to the user.</param>
public sealed record ErrorResponse(string Error);

/// <summary>OpenAPI metadata helpers for error responses.</summary>
internal static class ErrorResponseMetadata
{
    /// <summary>
    /// Declares the 409 response (<see cref="ErrorResponse"/>, application/json) that the exception
    /// middleware returns when a save to a budget conflicts with another accepted save (SPEC-004).
    /// </summary>
    public static RouteHandlerBuilder ProducesConflict(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status409Conflict,
            typeof(ErrorResponse), ["application/json"]));
}
