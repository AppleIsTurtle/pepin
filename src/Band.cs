using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pepin;

// ---------------------------------------------------------------- messages échangés avec le serveur

public sealed class RegisterReq { public string Version { get; set; } = ""; }
public sealed class RegisterResp { public string Id { get; set; } = ""; public string Token { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class HeartbeatReq
{
    public string Version { get; set; } = "";
    public string Status { get; set; } = "home";
    public int Tier { get; set; }
    public bool AcceptMessages { get; set; } = true;
    public List<long>? Ack { get; set; }
}
public sealed class TurtleRef { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public int Tier { get; set; } }
public sealed class VisitDto
{
    public int Id { get; set; }
    public TurtleRef? From { get; set; }
    public TurtleRef? Host { get; set; }
    public string? Message { get; set; }
    public int Duration { get; set; }
    public string? Souvenir { get; set; }
    public string? SouvenirLabel { get; set; }
    public List<string>? Played { get; set; }
    public int Friendship { get; set; }
    public string? Reason { get; set; }
}
public sealed class BandEventDto { public long Eid { get; set; } public string Type { get; set; } = ""; public VisitDto? Visit { get; set; } }
public sealed class HeartbeatResp { public List<BandEventDto> Events { get; set; } = []; public long Now { get; set; } public int BandSize { get; set; } public int Online { get; set; } }
public sealed class VisitReq { public string? To { get; set; } public string? Message { get; set; } }
public sealed class VisitResp { public VisitDto? Visit { get; set; } }
public sealed class EndReq { public string? Souvenir { get; set; } public List<string> Played { get; set; } = []; }
public sealed class BandTurtle
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public bool Online { get; set; }
    public string Status { get; set; } = "";
    public int Friendship { get; set; }
}
public sealed class BandList { public List<BandTurtle> Turtles { get; set; } = []; }
public sealed class NameReq { public string Name { get; set; } = ""; }
public sealed class IdReq { public string Id { get; set; } = ""; }
public sealed class CarnetEntry { public long T { get; set; } public string Text { get; set; } = ""; }
public sealed class CarnetReq
{
    public int Tier { get; set; }
    public double Bond { get; set; }
    public Dictionary<string, int> Stats { get; set; } = [];
    public Dictionary<string, int> Collection { get; set; } = [];
    public List<CarnetEntry> Journal { get; set; } = [];
}
public sealed class ErrorResp { public string? Error { get; set; } }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RegisterReq))]
[JsonSerializable(typeof(RegisterResp))]
[JsonSerializable(typeof(HeartbeatReq))]
[JsonSerializable(typeof(HeartbeatResp))]
[JsonSerializable(typeof(VisitReq))]
[JsonSerializable(typeof(VisitResp))]
[JsonSerializable(typeof(EndReq))]
[JsonSerializable(typeof(BandList))]
[JsonSerializable(typeof(NameReq))]
[JsonSerializable(typeof(IdReq))]
[JsonSerializable(typeof(CarnetReq))]
[JsonSerializable(typeof(ErrorResp))]
internal partial class BandJson : JsonSerializerContext { }

/// <summary>
/// La bande, côté client. Tout le réseau vit dans un thread à part ; les résultats reviennent au thread
/// de l'interface par <see cref="DrainUi"/>, appelé à chaque tick. Hors ligne, rien ne casse : on réessaie.
/// </summary>
public sealed class Band
{
    static string Api => Net.Base + "api/";

    readonly LifeData d;                                   // lu/écrit sur le thread UI uniquement
    volatile string? token;
    public volatile string Status = "home";                // home | away | sleeping | visiting
    public volatile int Tier;
    public volatile bool AcceptMessages = true;
    public volatile bool Fast;                             // visite en cours : heartbeat rapproché
    public volatile bool Connected;
    public volatile int Online, BandSize;
    public volatile List<BandTurtle> Turtles = [];

    // état des visites (thread UI)
    public VisitDto? Outgoing;                             // ma tortue est chez quelqu'un
    public bool Hosting;                                   // un visiteur est chez nous

    public Action<BandEventDto>? OnEvent;                  // appelé sur le thread UI
    public Action? OnChanged;                              // identité reçue / renommée : à sauvegarder

    readonly ConcurrentQueue<Action> toUi = new();
    readonly ConcurrentQueue<Func<Task>> jobs = new();
    readonly AutoResetEvent wake = new(false);
    readonly List<long> acks = [];                         // thread réseau
    readonly HashSet<long> seen = [];                      // thread UI : évite les doublons d'évènements
    long nextHeartbeat, nextBandFetch;

    public Band(LifeData d)
    {
        this.d = d;
        token = d.BandToken;
        AcceptMessages = d.AcceptMessages;
    }

    public string? Id => d.BandId;
    public string? Name => d.Name;
    public bool Registered => d.BandToken is not null;

    public void Start() => new Thread(Loop) { IsBackground = true, Name = "bande" }.Start();

    public void DrainUi()
    {
        while (toUi.TryDequeue(out var a)) a();
    }

    void Ui(Action a) => toUi.Enqueue(a);
    void Job(Func<Task> j) { jobs.Enqueue(j); wake.Set(); }

    // ---------------------------------------------------------------- boucle réseau

    void Loop()
    {
        int failures = 0;
        while (true)
        {
            try
            {
                if (token is null) Register().Wait();
                while (jobs.TryDequeue(out var j)) j().Wait();
                long now = Environment.TickCount64;
                if (now >= nextHeartbeat)
                {
                    Heartbeat().Wait();
                    nextHeartbeat = now + (Fast ? 10_000 : 60_000);
                }
                if (now >= nextBandFetch)
                {
                    FetchBand().Wait();
                    nextBandFetch = now + 180_000;
                }
                Connected = true;
                failures = 0;
            }
            catch
            {
                Connected = false;
                failures++;
                // hors ligne : on réessaie de plus en plus tard (max 5 min)
                nextHeartbeat = Environment.TickCount64 + Math.Min(300_000, 15_000 * failures);
                nextBandFetch = nextHeartbeat;
            }
            long wait = Math.Max(200, Math.Min(nextHeartbeat, nextBandFetch) - Environment.TickCount64);
            wake.WaitOne((int)Math.Min(wait, 60_000));
        }
    }

    async Task<(HttpStatusCode code, string body)> Send(HttpMethod method, string path, string? json)
    {
        using var req = new HttpRequestMessage(method, Api + path);
        if (token is not null) req.Headers.Authorization = new("Bearer", token);
        if (json is not null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await Net.Http.SendAsync(req);
        string body = await resp.Content.ReadAsStringAsync();
        if (resp.StatusCode == HttpStatusCode.Unauthorized && token is not null)
        {
            // jeton refusé (base remise à zéro ?) : on se réinscrira
            token = null;
            Ui(() => { d.BandToken = null; d.BandId = null; });
        }
        return (resp.StatusCode, body);
    }

    static string Error(string body)
    {
        try { return JsonSerializer.Deserialize(body, BandJson.Default.ErrorResp)?.Error ?? "erreur"; }
        catch { return "erreur"; }
    }

    async Task Register()
    {
        var (code, body) = await Send(HttpMethod.Post, "register",
            JsonSerializer.Serialize(new RegisterReq { Version = Updater.Current.ToString() }, BandJson.Default.RegisterReq));
        if (code != HttpStatusCode.Created && code != HttpStatusCode.OK) throw new HttpRequestException(code.ToString());
        var r = JsonSerializer.Deserialize(body, BandJson.Default.RegisterResp)!;
        token = r.Token;
        Ui(() =>
        {
            d.BandId = r.Id;
            d.BandToken = r.Token;
            d.Name = r.Name;
            OnChanged?.Invoke();
        });
    }

    async Task Heartbeat()
    {
        List<long>? ack = null;
        lock (acks) if (acks.Count > 0) { ack = [.. acks]; acks.Clear(); }
        var req = new HeartbeatReq { Version = Updater.Current.ToString(), Status = Status, Tier = Tier, AcceptMessages = AcceptMessages, Ack = ack };
        var (code, body) = await Send(HttpMethod.Post, "heartbeat", JsonSerializer.Serialize(req, BandJson.Default.HeartbeatReq));
        if (code != HttpStatusCode.OK)
        {
            if (ack is not null) lock (acks) acks.AddRange(ack);
            if (code == HttpStatusCode.TooManyRequests) return;
            throw new HttpRequestException(code.ToString());
        }
        var r = JsonSerializer.Deserialize(body, BandJson.Default.HeartbeatResp)!;
        Online = r.Online;
        BandSize = r.BandSize;
        foreach (var e in r.Events)
        {
            lock (acks) acks.Add(e.Eid);
            Ui(() =>
            {
                if (seen.Add(e.Eid)) OnEvent?.Invoke(e);
            });
        }
        if (r.Events.Count > 0) nextHeartbeat = Environment.TickCount64 + 1000;   // acquitter vite
    }

    async Task FetchBand()
    {
        var (code, body) = await Send(HttpMethod.Get, "band", null);
        if (code == HttpStatusCode.OK) Turtles = JsonSerializer.Deserialize(body, BandJson.Default.BandList)?.Turtles ?? [];
    }

    // ---------------------------------------------------------------- actions (appelées depuis le thread UI)

    /// <summary>Demande une visite. La tâche se termine sur le thread réseau ; le comportement la surveille.</summary>
    public Task<(VisitDto? visit, string? error)> RequestVisit(string? to, string? message)
    {
        var tcs = new TaskCompletionSource<(VisitDto?, string?)>();
        Job(async () =>
        {
            try
            {
                var (code, body) = await Send(HttpMethod.Post, "visit",
                    JsonSerializer.Serialize(new VisitReq { To = to, Message = message }, BandJson.Default.VisitReq));
                if (code == HttpStatusCode.Created || code == HttpStatusCode.OK)
                    tcs.SetResult((JsonSerializer.Deserialize(body, BandJson.Default.VisitResp)?.Visit, null));
                else tcs.SetResult((null, Error(body)));
            }
            catch { tcs.SetResult((null, "hors_ligne")); }
        });
        return tcs.Task;
    }

    public void Accept(int visitId) => Job(() => Send(HttpMethod.Post, $"visit/{visitId}/accept", "{}"));

    public void End(int visitId, Item? souvenir, List<string> played)
    {
        var json = JsonSerializer.Serialize(new EndReq { Souvenir = souvenir is Item i ? Items.Id(i) : null, Played = played }, BandJson.Default.EndReq);
        Job(() => Send(HttpMethod.Post, $"visit/{visitId}/end", json));
    }

    public void Abort(int visitId) => Job(() => Send(HttpMethod.Post, $"visit/{visitId}/abort", "{}"));

    /// <summary>À la fermeture du programme : on prévient tout de suite (2 s max).</summary>
    public void AbortNow(int visitId)
    {
        try { Task.Run(() => Send(HttpMethod.Post, $"visit/{visitId}/abort", "{}")).Wait(2000); } catch { }
    }

    public void Rename(string name, Action<string?> done)
    {
        var json = JsonSerializer.Serialize(new NameReq { Name = name }, BandJson.Default.NameReq);
        Job(async () =>
        {
            string? err;
            string? newName = null;
            try
            {
                var (code, body) = await Send(HttpMethod.Post, "rename", json);
                if (code == HttpStatusCode.OK) { newName = JsonSerializer.Deserialize(body, BandJson.Default.NameReq)?.Name; err = null; }
                else err = Error(body);
            }
            catch { err = "hors_ligne"; }
            Ui(() =>
            {
                if (newName is not null) { d.Name = newName; OnChanged?.Invoke(); }
                done(err);
            });
        });
    }

    public void Block(string id) => Job(() => Send(HttpMethod.Post, "block", JsonSerializer.Serialize(new IdReq { Id = id }, BandJson.Default.IdReq)));

    public void PushCarnet(CarnetReq c)
    {
        var json = JsonSerializer.Serialize(c, BandJson.Default.CarnetReq);
        Job(() => Send(HttpMethod.Put, "carnet", json));
    }

    /// <summary>Rafraîchit tout de suite (ouverture du menu, visite en préparation).</summary>
    public void Poke() { nextHeartbeat = 0; nextBandFetch = 0; wake.Set(); }

    // ---------------------------------------------------------------- règles

    /// <summary>Visite spontanée : rare (≥ 2 h d'écart), en forme, réveillé, humain présent, quelqu'un en ligne.</summary>
    public bool CanGoSpontaneously(Pet p) =>
        d.SpontaneousVisits && Connected && token is not null && Online > 1 && Outgoing is null && !Hosting &&
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() - d.LastVisitUnix > 2 * 3600 &&
        p.M.Energy > 0.5 && !p.Current.Asleep && p.S.IdleSeconds < 300;

    public static string ErrorText(string? error) => error switch
    {
        "personne" => "personne n'est dispo dans la bande",
        "indisponible" => "cette tortue n'est pas dispo",
        "deja_en_visite" => "déjà en visite",
        "nom_pris" => "ce nom est déjà pris",
        "nom_invalide" => "nom invalide (2 à 16 lettres)",
        "hors_ligne" => "pas de connexion",
        _ => "ça n'a pas marché",
    };
}
