namespace CostManagement.WebApi;

/// <summary>Error response body for 400 / 404 ({ "error": "日本語のメッセージ" }).</summary>
/// <param name="Error">Message that can be shown to the user.</param>
public sealed record ErrorResponse(string Error);
