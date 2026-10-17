using System.Diagnostics;

namespace CostManagement.WebApi;

/// <summary>
/// Logs one structured line per API request and opens a logging scope with the request ID
/// and the acting user, so business event logs written during the request carry them too.
/// </summary>
public static partial class RequestLogging
{
    /// <summary>Category name used for request logs.</summary>
    public const string Category = "CostManagement.WebApi.Request";

    [LoggerMessage(EventId = 2001, EventName = "HttpRequest", Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms")]
    private static partial void RequestCompleted(ILogger logger, string method, string path, int statusCode,
        long elapsedMs);

    [LoggerMessage(EventId = 2002, EventName = "BusinessRuleRejected", Level = LogLevel.Information,
        Message = "Request rejected by a business rule (400): {Reason}")]
    public static partial void BusinessRuleRejected(ILogger logger, string reason);

    [LoggerMessage(EventId = 2003, EventName = "ResourceNotFound", Level = LogLevel.Information,
        Message = "Requested resource was not found (404): {Reason}")]
    public static partial void ResourceNotFound(ILogger logger, string reason);

    /// <summary>
    /// Adds the request logging middleware. Must run after authentication so the user is known.
    /// The user is the token's "username" claim, or "anonymous" when not authenticated.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
        return app.Use(async (context, next) =>
        {
            var user = context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name ?? "unknown"
                : "anonymous";
            using var scope = logger.BeginScope(new RequestScope(context.TraceIdentifier, user));
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                RequestCompleted(logger, context.Request.Method, context.Request.Path.Value ?? "",
                    context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        });
    }

    /// <summary>Scope values (structured fields) with a readable text form for plain-text loggers.</summary>
    private sealed class RequestScope(string requestId, string user) : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly KeyValuePair<string, object>[] _items =
            [new("RequestId", requestId), new("User", user)];

        public KeyValuePair<string, object> this[int index] => _items[index];
        public int Count => _items.Length;
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object>>)_items).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => $"RequestId:{requestId} User:{user}";
    }
}
