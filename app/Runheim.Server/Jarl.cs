using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace Runheim.Server;

/// <summary>
/// The Jarl is a first-class villager. His activity is a GUESS — he authorized
/// guessing about himself only — and every surface labels it as one
/// (activity_is_guess is true in the API; the UI says "the village guesses…").
///
/// HIS OWN WORD (his order: "make it so I can select various actions I am
/// doing, or even write my own status for what I am doing"): he may set his
/// own status in the hall — a preset action or free text. While his word
/// stands it is NOT a guess: the activity is his words verbatim,
/// activity_is_guess is false, and the basis says the word is his own, set
/// in the hall. Clearing it hands his activity back to the guesses. His
/// word persists beside his chosen place, in the same state file, and
/// bridge snapshots never touch it.
///
/// Signals for the guess are composed in this order:
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
    private readonly object _stateLock = new();
    private string? _chosenPlace;
    private DateTimeOffset? _chosenAt;
    private string? _ownStatus;
    private DateTimeOffset? _ownStatusAt;

    public Jarl(Func<DateTimeOffset?> lastSnapshotAt, string? statePath = null, Action<string>? log = null)
    {
        _lastSnapshotAt = lastSnapshotAt;
        _statePath = statePath;
        _log = log;
        (_chosenPlace, _chosenAt, _ownStatus, _ownStatusAt) = LoadState();
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
    public string? ChosenPlace { get { lock (_stateLock) return _chosenPlace; } }

    /// <summary>Set (a visit place) or clear (null) his chosen place, and
    /// persist it. Persistence failure is logged, never fatal: the hall
    /// keeps serving and the choice still holds for this run.</summary>
    public void SetChosenPlace(string? placeId)
    {
        if (placeId is not null && !Visits.ContainsKey(placeId))
            throw new ArgumentException($"'{placeId}' is not a place the Jarl can visit.", nameof(placeId));
        lock (_stateLock)
        {
            _chosenPlace = placeId;
            _chosenAt = placeId is null ? null : DateTimeOffset.UtcNow;
            PersistState();
        }
    }

    /// <summary>The longest own word the hall accepts — longer word is
    /// refused whole, never silently cut.</summary>
    public const int MaxOwnStatusLength = 200;

    /// <summary>The Jarl's own word about what he is doing, or null while
    /// the village's guesses govern his activity.</summary>
    public string? OwnStatus { get { lock (_stateLock) return _ownStatus; } }

    /// <summary>Set (non-empty text) or clear (null/empty) his own word, and
    /// persist it in the same state file as his chosen place. While it
    /// stands, his activity is the word verbatim and is not a guess.</summary>
    public void SetOwnStatus(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) trimmed = null;
        if (trimmed is not null && trimmed.Length > MaxOwnStatusLength)
            throw new ArgumentException($"The Jarl's word is at most {MaxOwnStatusLength} characters.", nameof(text));
        lock (_stateLock)
        {
            _ownStatus = trimmed;
            _ownStatusAt = trimmed is null ? null : DateTimeOffset.UtcNow;
            PersistState();
        }
    }

    /// <summary>Load his durable word — chosen place and own status — from
    /// the one state file. The shape is
    /// { "chosen_place", "chosen_at", "own_status", "own_status_at" } with
    /// either half optional; the walking build's place-only files load
    /// unchanged. Anything unreadable is logged and the guesses govern.</summary>
    private (string? Place, DateTimeOffset? PlaceAt, string? Status, DateTimeOffset? StatusAt) LoadState()
    {
        if (_statePath is null) return (null, null, null, null);
        try
        {
            if (!File.Exists(_statePath)) return (null, null, null, null);
            if (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_statePath))
                    is not System.Text.Json.Nodes.JsonObject obj)
                return (null, null, null, null);
            string? place = null;
            DateTimeOffset? placeAt = null;
            if (obj["chosen_place"]?.GetValue<string>() is { } p)
            {
                if (Visits.ContainsKey(p)) { place = p; placeAt = ParseStamp(obj["chosen_at"]); }
                else _log?.Invoke($"Jarl state at {_statePath} names no visit place; ignoring the place.");
            }
            string? status = null;
            DateTimeOffset? statusAt = null;
            if (obj["own_status"]?.GetValue<string>() is { } s && !string.IsNullOrWhiteSpace(s))
            {
                status = s.Trim();
                statusAt = ParseStamp(obj["own_status_at"]);
            }
            return (place, placeAt, status, statusAt);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Jarl state at {_statePath} could not be read ({ex.GetType().Name}); the guess engine governs.");
            return (null, null, null, null);
        }
    }

    private static DateTimeOffset? ParseStamp(System.Text.Json.Nodes.JsonNode? node) =>
        node?.GetValue<string>() is { } s && DateTimeOffset.TryParse(s, out var at) ? at : null;

    /// <summary>Persist both halves of his word. The state file exists only
    /// while something stands in it: when the place is handed back AND his
    /// own word is cleared, the file is deleted. Caller holds the lock.</summary>
    private void PersistState()
    {
        if (_statePath is null) return;
        try
        {
            if (_chosenPlace is null && _ownStatus is null)
            {
                if (File.Exists(_statePath)) File.Delete(_statePath);
                return;
            }
            var obj = new System.Text.Json.Nodes.JsonObject();
            if (_chosenPlace is not null)
            {
                obj["chosen_place"] = _chosenPlace;
                obj["chosen_at"] = (_chosenAt ?? DateTimeOffset.UtcNow).ToString("o");
            }
            if (_ownStatus is not null)
            {
                obj["own_status"] = _ownStatus;
                obj["own_status_at"] = (_ownStatusAt ?? DateTimeOffset.UtcNow).ToString("o");
            }
            var dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = _statePath + ".tmp";
            File.WriteAllText(tmp, obj.ToJsonString());
            File.Move(tmp, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Jarl state at {_statePath} could not be saved ({ex.GetType().Name}); his word holds for this run only.");
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

        // His own word outranks every guess about his activity — the words
        // are his verbatim and the flag says so. Placement is untouched:
        // a chosen place still governs where he stands, and the time bands
        // still place him while no word stands.
        var own = OwnStatus;
        var isGuess = own is null;
        if (own is not null)
        {
            activity = own;
            basis = chosen is not null
                ? new List<string> { "place chosen by the Jarl himself", "activity is the Jarl's own word, set in the hall" }
                : new List<string> { "the Jarl's own word, set in the hall" };
        }

        return new JsonObject
        {
            ["name"] = "Volmarr",
            ["role"] = "The Jarl",
            ["portrait"] = "/art/portraits/volmarr.webp",
            ["place"] = place,
            ["station"] = station,
            ["activity"] = activity,
            ["activity_is_guess"] = isGuess,
            ["own_status"] = own,
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
