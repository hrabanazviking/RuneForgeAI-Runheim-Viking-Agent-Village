using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace Runheim.Server;

/// <summary>
/// The Jarl is a first-class villager. His activity is a GUESS — he authorized
/// guessing about himself only — and every surface labels it as one
/// (activity_is_guess is always true in the API; the UI says "the village guesses…").
///
/// Signals are composed in this order:
///   (a) local time-of-day bands on the host;
///   (b) on Linux only, his login session state via loginctl (seat0 active /
///       idle), read best-effort behind a platform guard — every other OS,
///       and any probe failure, falls back to the time bands alone;
///   (c) recency of bridge snapshots (fresh Muse word implies he is near
///       his machines) — used in the stated basis, and never hidden.
/// This probe is the single platform-conditional in the codebase; everything
/// else is uniform managed code over Path.Combine / Environment paths.
///
/// THE JARL WALKS (his order: "but I also should be able to walk around if I
/// want to"): he may choose a place to visit. A chosen place is NOT a guess —
/// it is his own word, it outranks the guess engine, it persists in a small
/// state file across restarts, and bridge snapshots never override it. While
/// he visits, only the activity text remains a labelled guess ("visiting the
/// Dvergar Forge — the village guesses he is inspecting the work") and the
/// basis states "place chosen by the Jarl himself". Choosing the Longhouse
/// (or clearing the choice) hands placement back to the guess engine.
/// </summary>
public sealed class Jarl
{
    private readonly Func<DateTimeOffset?> _lastSnapshotAt;
    private readonly TimeSpan _probeCacheFor = TimeSpan.FromSeconds(15);
    private SessionSignal? _cachedProbe;
    private DateTimeOffset _probeCachedAt = DateTimeOffset.MinValue;
    private readonly object _probeLock = new();

    private readonly string? _statePath;
    private readonly Action<string>? _log;
    private readonly object _choiceLock = new();
    private string? _chosenPlace;

    public Jarl(Func<DateTimeOffset?> lastSnapshotAt, string? statePath = null, Action<string>? log = null)
    {
        _lastSnapshotAt = lastSnapshotAt;
        _statePath = statePath;
        _log = log;
        _chosenPlace = LoadChoice();
    }

    /// <summary>Every place id in the village, in village order.</summary>
    public static readonly string[] AllPlaceIds =
        { "longhouse", "library", "forge", "ring", "seidr", "watch", "oathstones", "hearth" };

    /// <summary>The places the Jarl may choose to visit (the Longhouse is home,
    /// never a visit): place id → display name, his station there, and the
    /// labelled-guess activity text while he visits.</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Station, string Activity)> Visits =
        new Dictionary<string, (string, string, string)>
        {
            ["library"] = ("Mimir's Rune Library", "great map table",
                "visiting Mimir's Rune Library — the village guesses he is reading the old words"),
            ["forge"] = ("Dvergar Forge", "great anvil",
                "visiting the Dvergar Forge — the village guesses he is inspecting the work"),
            ["ring"] = ("Týr's Training Ring", "judges' bench",
                "visiting Týr's Training Ring — the village guesses he is judging the bouts"),
            ["seidr"] = ("Seiðr House", "high seat",
                "visiting the Seiðr House — the village guesses he is consulting the seiðr"),
            ["watch"] = ("Huginn's Watch", "tower rail",
                "visiting Huginn's Watch — the village guesses he is scanning the horizon"),
            ["oathstones"] = ("Sacred Oathstones", "ring centre",
                "visiting the Sacred Oathstones — the village guesses he is witnessing the oaths"),
            ["hearth"] = ("Hearth Hall", "hearth fire",
                "visiting the Hearth Hall — the village guesses he is taking his ease by the fire"),
        };

    public static bool IsVisitPlace(string? id) => id is not null && Visits.ContainsKey(id);

    /// <summary>The place the Jarl himself has chosen to visit, or null when
    /// the guess engine governs his placement.</summary>
    public string? ChosenPlace { get { lock (_choiceLock) return _chosenPlace; } }

    /// <summary>Set (a visit place) or clear (null) his chosen place, and
    /// persist it. Persistence failure is logged, never fatal: the hall
    /// keeps serving and the choice still holds for this run.</summary>
    public void SetChosenPlace(string? placeId)
    {
        if (placeId is not null && !Visits.ContainsKey(placeId))
            throw new ArgumentException($"'{placeId}' is not a place the Jarl can visit.", nameof(placeId));
        lock (_choiceLock)
        {
            _chosenPlace = placeId;
            PersistChoice(placeId);
        }
    }

    private string? LoadChoice()
    {
        if (_statePath is null) return null;
        try
        {
            if (!File.Exists(_statePath)) return null;
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_statePath));
            var place = (node as System.Text.Json.Nodes.JsonObject)?["chosen_place"]?.GetValue<string>();
            if (place is not null && Visits.ContainsKey(place)) return place;
            _log?.Invoke($"Jarl state at {_statePath} names no visit place; ignoring it.");
            return null;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Jarl state at {_statePath} could not be read ({ex.GetType().Name}); the guess engine governs.");
            return null;
        }
    }

    private void PersistChoice(string? placeId)
    {
        if (_statePath is null) return;
        try
        {
            if (placeId is null)
            {
                if (File.Exists(_statePath)) File.Delete(_statePath);
                return;
            }
            var dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = new System.Text.Json.Nodes.JsonObject
            {
                ["chosen_place"] = placeId,
                ["chosen_at"] = DateTimeOffset.UtcNow.ToString("o"),
            }.ToJsonString();
            var tmp = _statePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Jarl state at {_statePath} could not be saved ({ex.GetType().Name}); the choice holds for this run only.");
        }
    }

    private sealed record SessionSignal(bool Active, bool IdleHint, double? IdleSeconds);

    public JsonObject GetState()
    {
        var now = DateTimeOffset.Now; // host-local time, by design (signal a)
        var session = ProbeSession();
        var lastSnap = _lastSnapshotAt();
        var snapAge = lastSnap is null ? (TimeSpan?)null : now - lastSnap.Value;
        var freshWord = snapAge is { } age && age <= TimeSpan.FromMinutes(15);

        var basis = new List<string> { $"local time {now:HH:mm}" };
        if (session is not null)
            basis.Add(session.IdleSeconds is { } idle
                ? $"seat0 {(session.Active ? "active" : "inactive")}, idle {FormatAge(TimeSpan.FromSeconds(idle))}"
                : $"seat0 {(session.Active ? "active" : "inactive")}");
        else
            basis.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "no readable session signal"
                : "no session signal on this OS");
        if (freshWord && snapAge is { } a)
            basis.Add($"fresh bridge word {FormatAge(a)} ago");

        string activity, place, station, status;
        var minutes = now.Hour * 60 + now.Minute;
        var longIdle = session?.IdleSeconds is { } s && s >= 3600;
        var chosen = ChosenPlace;

        if (chosen is not null && Visits.TryGetValue(chosen, out var visit))
        {
            // His own word outranks every guess: the place and station are
            // the ones he chose; only the activity text stays a guess, and
            // the basis says plainly where the placement came from.
            basis.Insert(0, "place chosen by the Jarl himself");
            activity = visit.Activity;
            place = chosen; station = visit.Station; status = "visiting";
        }
        else if (longIdle && minutes >= 330) // long idle, outside deep night
        {
            activity = "about his earthly business, away from the hall";
            place = "hearth"; station = "long tables"; status = "away";
        }
        else if (minutes < 330) // 00:00–05:29, deep night
        {
            activity = "dream-walking — the village keeps the watch";
            place = "hearth"; station = "rest benches"; status = "resting";
        }
        else if (minutes < 660) // 05:30–10:59, morning
        {
            activity = "taking his morning counsel with his tablet";
            place = "longhouse"; station = "high seat"; status = "present";
        }
        else if (minutes < 1080) // 11:00–17:59, day
        {
            activity = "surveying his fleet at the war-map board";
            place = "longhouse"; station = "war-map board"; status = "present";
        }
        else // 18:00–23:59, evening
        {
            activity = "by the long fire";
            place = "hearth"; station = "hearth fire"; status = "present";
        }

        return new JsonObject
        {
            ["name"] = "Volmarr",
            ["role"] = "The Jarl",
            ["portrait"] = "/art/portraits/volmarr.webp",
            ["place"] = place,
            ["station"] = station,
            ["activity"] = activity,
            ["activity_is_guess"] = true,
            ["basis"] = string.Join(" + ", basis),
            ["status"] = status,
            ["chosen_place"] = chosen,
        };
    }

    private SessionSignal? ProbeSession()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return null;
        lock (_probeLock)
        {
            if (DateTimeOffset.Now - _probeCachedAt < _probeCacheFor) return _cachedProbe;
            _cachedProbe = ProbeSessionUncached();
            _probeCachedAt = DateTimeOffset.Now;
            return _cachedProbe;
        }
    }

    private static SessionSignal? ProbeSessionUncached()
    {
        try
        {
            var list = RunLoginCtl("list-sessions", "--no-legend");
            if (list is null) return null;
            string? sessionId = null;
            foreach (var line in list.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // SESSION UID USER SEAT TTY …
                if (parts.Length >= 4 && parts[3] == "seat0") { sessionId = parts[0]; break; }
                sessionId ??= parts.Length >= 1 ? parts[0] : null;
            }
            if (sessionId is null) return null;

            var show = RunLoginCtl("show-session", sessionId, "-p", "Active", "-p", "IdleHint", "-p", "IdleSinceHintMonotonic");
            if (show is null) return null;
            var props = new Dictionary<string, string>();
            foreach (var line in show.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var i = line.IndexOf('=');
                if (i > 0) props[line[..i]] = line[(i + 1)..];
            }
            var active = props.GetValueOrDefault("Active") == "yes";
            var idleHint = props.GetValueOrDefault("IdleHint") == "yes";
            double? idleSeconds = null;
            if (idleHint
                && props.TryGetValue("IdleSinceHintMonotonic", out var mono)
                && ulong.TryParse(mono, out var idleSinceUsec)
                && TryReadMonotonicUsec(out var nowUsec)
                && nowUsec >= idleSinceUsec)
            {
                idleSeconds = (nowUsec - idleSinceUsec) / 1_000_000.0;
            }
            else if (!idleHint)
            {
                idleSeconds = 0;
            }
            return new SessionSignal(active, idleHint, idleSeconds);
        }
        catch
        {
            return null; // best-effort signal: any failure means "no signal"
        }
    }

    private static bool TryReadMonotonicUsec(out ulong usec)
    {
        usec = 0;
        try
        {
            // /proc/uptime: seconds since boot on the same monotonic clock
            // loginctl reports IdleSinceHintMonotonic against.
            var text = File.ReadAllText("/proc/uptime");
            var first = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (!double.TryParse(first, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds)) return false;
            usec = (ulong)(seconds * 1_000_000);
            return true;
        }
        catch { return false; }
    }

    private static string? RunLoginCtl(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("loginctl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(2000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return null;
            }
            return p.ExitCode == 0 ? output.Result : null;
        }
        catch { return null; }
    }

    public static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1)) return $"{(int)age.TotalSeconds}s";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h {(int)age.Minutes}m";
        return $"{(int)age.TotalDays}d";
    }
}
