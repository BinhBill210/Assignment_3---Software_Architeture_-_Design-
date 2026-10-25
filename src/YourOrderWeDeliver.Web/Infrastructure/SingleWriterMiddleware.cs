namespace YourOrderWeDeliver.Web.Infrastructure;

/// <summary>
/// Processes page requests one at a time. The object model is held in memory and is not
/// thread-safe, so serialising requests keeps every change and its save consistent. At the
/// expected load (about ten concurrent users, A1 Section 6.2) the queueing delay is negligible.
/// </summary>
public sealed class SingleWriterMiddleware(RequestDelegate next)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Runs the rest of the pipeline while holding the gate.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await Gate.WaitAsync(context.RequestAborted);
        try
        {
            await next(context);
        }
        finally
        {
            Gate.Release();
        }
    }
}
