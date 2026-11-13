using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.FileProviders;
using Runheim.Server;

var builder = WebApplication.CreateBuilder(args);

static string ExpandHome(string path)
{
    // Config values may use a leading "~/" for the user's home on every OS.
    if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
    return path;
}

string ResolveRoot(string? configured, string fallback, string folderName)
{
    var root = ExpandHome(string.IsNullOrWhiteSpace(configured) ? fallback : configured);
    var full = Path.GetFullPath(Path.IsPathRooted(root)
        ? root
        : Path.Combine(builder.Environment.ContentRootPath, root));
    if (Directory.Exists(full)) return full;
    // In a fresh clone the server project sits at app/Runheim.Server while the
    // repo's art/ and data/ live at the repo root: walk up from the content
    // root and take the first sibling folder of that name. An explicitly
    // configured root that exists always wins; a missing one falls back here
    // before the caller warns and serves honest 404s.
    var dir = new DirectoryInfo(builder.Environment.ContentRootPath);
    for (var i = 0; i < 4 && dir is not null; i++, dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, folderName);
        if (Directory.Exists(candidate)) return candidate;
    }
    return full;
}

var artRoot = ResolveRoot(builder.Configuration["Runheim:ArtRoot"], "../art", "art");
var dataRoot = ResolveRoot(builder.Configuration["Runheim:DataRoot"], "../data", "data");

var store = new VillageStore();
var jarl = new Jarl(() => store.ReceivedAt);
var hub = new SseHub();

var app = builder.Build();

app.Logger.LogInformation("Rúnheim Live Hall starting. ArtRoot={ArtRoot} DataRoot={DataRoot}", artRoot, dataRoot);

// The village web UI (wwwroot) plus the repo's own art and data, served
// read-only from configurable roots so the same build runs from a clone on
// any OS. Missing roots are a warning, never a crash: the API stays honest.
app.UseDefaultFiles();
app.UseStaticFiles();
if (Directory.Exists(artRoot))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(artRoot), RequestPath = "/art" });
else
    app.Logger.LogWarning("ArtRoot {ArtRoot} not found — /art will 404 until it exists.", artRoot);
if (Directory.Exists(dataRoot))
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(dataRoot), RequestPath = "/data" });
else
    app.Logger.LogWarning("DataRoot {DataRoot} not found — /data will 404 until it exists.", dataRoot);

JsonObject BuildVillagePayload()
{
    var now = DateTimeOffset.UtcNow;
    var snapshot = store.Snapshot;
    var payload = new JsonObject
    {
        ["server_time"] = now.ToString("o"),
        ["snapshot"] = snapshot?.DeepClone(),
        ["snapshot_age_seconds"] = store.ReceivedAt is { } at ? (now - at).TotalSeconds : null,
        ["jarl"] = jarl.GetState(),
    };
    var agents = new JsonArray();
    if (snapshot?["agents"] is JsonArray rawAgents)
    {
        var snapAge = store.ReceivedAt is { } at2 ? now - at2 : TimeSpan.Zero;
        foreach (var node in rawAgents)
        {
            if (node is not JsonObject agent) continue;
            var copy = (JsonObject)agent.DeepClone();
            var status = copy["status"]?.GetValue<string>() ?? "idle";
            var stale = snapAge > TimeSpan.FromMinutes(15);
            if (copy["last_seen"] is JsonValue lv
                && lv.TryGetValue<string>(out var ls)
                && DateTimeOffset.TryParse(ls, out var seen)
                && now - seen > TimeSpan.FromMinutes(15))
            {
                stale = true;
            }
            // Honesty law: stale word is shown resting at the Hearth Hall,
            // never faked as live work. Finished and failed work keeps its
            // own truth. The snapshot's spoken place/status are never
            // overwritten — the effective ones ride beside them.
            var finished = status is "done" or "failed";
            copy["is_stale"] = stale;
            copy["effective_status"] = stale && !finished ? "resting" : status;
            copy["effective_place"] = stale && !finished
                ? "hearth"
                : copy["place"]?.GetValue<string>() ?? "hearth";
            agents.Add(copy);
        }
    }
    payload["agents"] = agents;
    return payload;
}

app.MapGet("/api/health", () => Results.Json(new JsonObject
{
    ["ok"] = true,
    ["app"] = "Runheim.Server",
    ["version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
    ["utc"] = DateTimeOffset.UtcNow.ToString("o"),
    ["has_snapshot"] = store.Snapshot is not null,
    ["agents"] = store.AgentCount,
    ["art_root_ok"] = Directory.Exists(artRoot),
    ["data_root_ok"] = Directory.Exists(dataRoot),
}));

app.MapGet("/api/village", () => Results.Json(BuildVillagePayload()));

app.MapGet("/api/roster", () =>
{
    static JsonArray Scan(string dir)
    {
        var list = new JsonArray();
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.EnumerateFiles(dir, "*.webp").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            list.Add(new JsonObject
            {
                ["slug"] = Path.GetFileNameWithoutExtension(name),
                ["url"] = $"/art/{Path.GetFileName(dir)}/{name}",
            });
        }
        return list;
    }
    return Results.Json(new JsonObject
    {
        ["cast"] = Scan(Path.Combine(artRoot, "portraits")),
        ["library"] = Scan(Path.Combine(artRoot, "portraits-library")),
    });
});

app.MapPost("/api/snapshot", async (HttpRequest request) =>
{
    JsonNode? body;
    try
    {
        body = await JsonNode.ParseAsync(request.Body);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new JsonObject { ["ok"] = false, ["error"] = "body is not valid JSON" });
    }
    if (body is not JsonObject obj || obj["agents"] is not JsonArray)
        return Results.BadRequest(new JsonObject { ["ok"] = false, ["error"] = "snapshot must be a JSON object with an 'agents' array" });
    if (obj["taken_at"] is null) obj["taken_at"] = DateTimeOffset.UtcNow.ToString("o");
    store.Set(obj);
    hub.Broadcast(BuildVillagePayload());
    return Results.Json(new JsonObject { ["ok"] = true, ["agents"] = store.AgentCount });
});

app.MapPost("/api/clear", () =>
{
    store.Clear();
    hub.Broadcast(BuildVillagePayload());
    return Results.Json(new JsonObject { ["ok"] = true });
});

app.MapGet("/api/events", async (HttpContext context) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no";
    var channel = Channel.CreateUnbounded<string>();
    var id = hub.Add(channel.Writer);
    try
    {
        await context.Response.WriteAsync("event: village\ndata: " + BuildVillagePayload().ToJsonString() + "\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(25));
        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeat.WaitForNextTickAsync(context.RequestAborted))
                    channel.Writer.TryWrite(": ping\n\n");
            }
            catch (OperationCanceledException) { /* client gone */ }
        });
        await foreach (var message in channel.Reader.ReadAllAsync(context.RequestAborted))
        {
            await context.Response.WriteAsync(message, context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        await heartbeatTask;
    }
    catch (OperationCanceledException) { /* client disconnected */ }
    finally
    {
        hub.Remove(id);
    }
    return Results.Empty;
});

app.Run();

/// <summary>The latest bridge snapshot, held in memory. The hall is a window,
/// not an archive: a restart honestly shows the empty night until fresh word.</summary>
sealed class VillageStore
{
    private readonly object _lock = new();
    private JsonObject? _snapshot;
    private DateTimeOffset? _receivedAt;

    public JsonObject? Snapshot { get { lock (_lock) return _snapshot; } }
    public DateTimeOffset? ReceivedAt { get { lock (_lock) return _receivedAt; } }
    public int AgentCount
    {
        get { lock (_lock) return _snapshot?["agents"] is JsonArray a ? a.Count : 0; }
    }

    public void Set(JsonObject snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
            _receivedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _snapshot = null;
            _receivedAt = null;
        }
    }
}

/// <summary>Server-sent events fan-out: every snapshot/clear pushes the full
/// village payload to every connected hall window.</summary>
sealed class SseHub
{
    private readonly ConcurrentDictionary<Guid, ChannelWriter<string>> _clients = new();

    public Guid Add(ChannelWriter<string> writer)
    {
        var id = Guid.NewGuid();
        _clients[id] = writer;
        return id;
    }

    public void Remove(Guid id)
    {
        if (_clients.TryRemove(id, out var writer)) writer.TryComplete();
    }

    public void Broadcast(JsonObject payload)
    {
        var message = "event: village\ndata: " + payload.ToJsonString() + "\n\n";
        foreach (var writer in _clients.Values) writer.TryWrite(message);
    }
}
