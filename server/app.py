"""Serveur « bande » de Pépin : les tortues de la bande se rendent visite.

API JSON sous /api, pages publiques /t/{id} et /bande (voir pages.py).
"""

import hashlib
import json
import random
import secrets
import string
import threading
import time
import unicodedata
from contextlib import asynccontextmanager
from typing import Annotated, Literal, Optional

from fastapi import Depends, FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import AfterValidator, BaseModel, Field, StringConstraints

import catalogue
import db
import pages
from catalogue import ACTIVITES, SOUVENIRS

EN_LIGNE = 150            # s : au-delà, une tortue est hors ligne
ATTENTE_MAX = 150         # s : une visite 'pending' plus vieille → personne à la maison
MARGE_ACTIVE = 180        # s : marge après la durée prévue avant de clore une visite 'active'
TRENTE_JOURS = 30 * 86400
MAX_CORPS = 32 * 1024     # octets
INTERVALLE_HB = 4         # s entre deux heartbeats d'une même tortue
INSCRIPTIONS_PAR_HEURE = 10
VISITES_PAR_HEURE = 6


def now() -> int:
    """Horloge du serveur (remplaçable dans les tests)."""
    return int(time.time())


class ApiError(Exception):
    def __init__(self, status: int, code: str):
        self.status = status
        self.code = code


# --- limites de débit en mémoire (un seul processus uvicorn) ---
_verrou = threading.Lock()
_dernier_hb: dict[str, int] = {}          # tortue → dernier heartbeat
_inscriptions: dict[str, list[int]] = {}  # IP → horodatages des inscriptions


class LimiteCorps:
    """Middleware ASGI : refuse (413) tout corps de requête au-delà de MAX_CORPS."""

    def __init__(self, app):
        self.app = app

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            return await self.app(scope, receive, send)
        trop_gros = JSONResponse({"error": "trop_gros"}, 413)
        longueur = dict(scope["headers"]).get(b"content-length")
        if longueur is not None and (not longueur.isdigit() or int(longueur) > MAX_CORPS):
            return await trop_gros(scope, receive, send)
        # Lit tout le corps (au plus MAX_CORPS) puis le rejoue à l'application.
        corps, encore = b"", True
        while encore:
            msg = await receive()
            if msg["type"] == "http.disconnect":
                return
            corps += msg.get("body", b"")
            encore = msg.get("more_body", False)
            if len(corps) > MAX_CORPS:
                return await trop_gros(scope, receive, send)
        rejoue = False

        async def recevoir():
            nonlocal rejoue
            if not rejoue:
                rejoue = True
                return {"type": "http.request", "body": corps, "more_body": False}
            return await receive()

        await self.app(scope, recevoir, send)


@asynccontextmanager
async def lifespan(_app):
    db.init()
    yield


app = FastAPI(lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)
app.add_middleware(LimiteCorps)


@app.exception_handler(ApiError)
async def _erreur_api(_request, exc: ApiError):
    return JSONResponse({"error": exc.code}, exc.status)


@app.exception_handler(RequestValidationError)
async def _erreur_validation(_request, _exc):
    return JSONResponse({"error": "invalide"}, 422)


# --- utilitaires ---

def nettoyer(texte: Optional[str], max_len: int) -> Optional[str]:
    """Retire les caractères de contrôle, compacte les espaces, tronque ; vide → None."""
    if texte is None:
        return None
    garde = []
    for ch in texte:
        if ch.isspace():
            garde.append(" ")
        elif unicodedata.category(ch)[0] != "C" or ch == "‍":  # garde le ZWJ des émojis
            garde.append(ch)
    s = " ".join("".join(garde).split())[:max_len].rstrip()
    return s or None


def nom_valide(brut: str) -> Optional[str]:
    nom = unicodedata.normalize("NFC", brut).strip()
    if not 2 <= len(nom) <= 16:
        return None
    if not all(ch.isalpha() or ch.isdecimal() or ch in " -'’" for ch in nom):
        return None
    return nom


def paire(x: str, y: str) -> tuple[str, str]:
    return (x, y) if x < y else (y, x)


def points(c, x: str, y: str) -> int:
    row = c.execute("SELECT points FROM friendships WHERE a = ? AND b = ?", paire(x, y)).fetchone()
    return row["points"] if row else 0


def ajouter_points(c, x: str, y: str, n: int) -> int:
    c.execute(
        "INSERT INTO friendships (a, b, points) VALUES (?, ?, ?) "
        "ON CONFLICT (a, b) DO UPDATE SET points = points + excluded.points",
        (*paire(x, y), n),
    )
    return points(c, x, y)


def evenement(c, destinataire: str, payload: dict, t: int) -> None:
    c.execute(
        "INSERT INTO events (turtle_id, payload, created) VALUES (?, ?, ?)",
        (destinataire, json.dumps(payload, ensure_ascii=False), t),
    )


def charger_visite(c, vid: int):
    v = c.execute("SELECT * FROM visits WHERE id = ?", (vid,)).fetchone()
    if v is None:
        raise ApiError(404, "introuvable")
    return v


def evenement_retour(c, v, souvenir: Optional[str], played: list, raison: Optional[str], t: int) -> None:
    hote = c.execute("SELECT name FROM turtles WHERE id = ?", (v["to_id"],)).fetchone()
    evenement(c, v["from_id"], {
        "type": "return",
        "visit": {
            "id": v["id"],
            "host": {"id": v["to_id"], "name": hote["name"]},
            "souvenir": souvenir,
            "souvenir_label": SOUVENIRS.get(souvenir) if souvenir else None,
            "played": played,
            "friendship": points(c, v["from_id"], v["to_id"]),
            "reason": raison,
        },
    }, t)


def clore(c, v, etat: str, t: int) -> None:
    """Passe la visite dans un état final et libère le visiteur."""
    c.execute("UPDATE visits SET state = ?, ended = ? WHERE id = ?", (etat, t, v["id"]))
    c.execute("UPDATE turtles SET status = 'away' WHERE id = ? AND status = 'visiting'", (v["from_id"],))
    # Si l'hôte n'a pas encore lu l'annonce du visiteur, elle n'a plus lieu d'être.
    c.execute(
        "UPDATE events SET acked = 1 WHERE turtle_id = ? AND acked = 0 "
        "AND json_extract(payload, '$.type') = 'visitor' AND json_extract(payload, '$.visit.id') = ?",
        (v["to_id"], v["id"]),
    )


def terminer(c, v, souvenir: Optional[str], played: list, t: int) -> int:
    """Fin normale d'une visite : points d'amitié, souvenir, évènement de retour."""
    gain = 2 if len(played) >= 3 else 1
    total = ajouter_points(c, v["from_id"], v["to_id"], gain)
    c.execute(
        "UPDATE visits SET souvenir = ?, played = ? WHERE id = ?",
        (souvenir, json.dumps(played), v["id"]),
    )
    clore(c, v, "done", t)
    evenement_retour(c, v, souvenir, played, None, t)
    return total


def maintenance(c, t: int) -> None:
    """Expire les visites en retard. Idempotent : appelé à chaque heartbeat."""
    for v in c.execute(
        "SELECT * FROM visits WHERE state = 'pending' AND created < ?", (t - ATTENTE_MAX,)
    ).fetchall():
        clore(c, v, "no_one_home", t)
        evenement_retour(c, v, None, [], "personne_a_la_maison", t)
    for v in c.execute(
        "SELECT * FROM visits WHERE state = 'active' AND accepted + duration + ? < ?", (MARGE_ACTIVE, t)
    ).fetchall():
        terminer(c, v, random.choice(list(SOUVENIRS)), [], t)
    # Ménage : évènements lus depuis plus d'un jour.
    c.execute("DELETE FROM events WHERE acked = 1 AND created < ?", (t - 86400,))


def auth(request: Request) -> str:
    """Dépendance : vérifie le jeton Bearer, met à jour last_seen, renvoie l'id de la tortue."""
    schema, _, jeton = request.headers.get("authorization", "").partition(" ")
    jeton = jeton.strip()
    if schema.lower() != "bearer" or not jeton or len(jeton) > 200:
        raise ApiError(401, "auth")
    empreinte = hashlib.sha256(jeton.encode()).hexdigest()
    with db.tx() as c:
        row = c.execute("SELECT id FROM turtles WHERE token_hash = ?", (empreinte,)).fetchone()
        if row is None:
            raise ApiError(401, "auth")
        c.execute("UPDATE turtles SET last_seen = ? WHERE id = ?", (now(), row["id"]))
    return row["id"]


Moi = Annotated[str, Depends(auth)]


# --- modèles des corps de requête ---

def _souvenir(v: str) -> str:
    if v not in SOUVENIRS:
        raise ValueError("souvenir inconnu")
    return v


def _activite(v: str) -> str:
    if v not in ACTIVITES:
        raise ValueError("activité inconnue")
    return v


IdTortue = Annotated[str, StringConstraints(pattern=r"^[a-z0-9]{8}$")]
Version = Annotated[str, StringConstraints(max_length=32)]
Souvenir = Annotated[str, AfterValidator(_souvenir)]
Activite = Annotated[str, AfterValidator(_activite)]
CleStat = Annotated[str, StringConstraints(pattern=r"^[A-Za-z0-9_-]{1,24}$")]
Compteur = Annotated[int, Field(ge=0, le=10**12)]


class Inscription(BaseModel):
    version: Version


class Battement(BaseModel):
    version: Version
    status: Literal["home", "away", "sleeping", "visiting"]
    tier: int = Field(ge=0, le=4)
    accept_messages: bool
    ack: list[int] = Field(default_factory=list, max_length=500)


class DemandeVisite(BaseModel):
    to: Optional[IdTortue] = None
    message: Optional[str] = Field(default=None, max_length=1000)


class FinVisite(BaseModel):
    souvenir: Optional[Souvenir] = None
    played: list[Activite] = Field(default_factory=list, max_length=20)


class Renommage(BaseModel):
    name: str = Field(max_length=100)


class Cible(BaseModel):
    id: IdTortue


class EntreeJournal(BaseModel):
    t: int = Field(ge=0, le=10**11)
    text: str = Field(max_length=1000)


class Carnet(BaseModel):
    tier: int = Field(ge=0, le=4)
    bond: float = Field(allow_inf_nan=False)
    stats: dict[CleStat, Compteur] = Field(default_factory=dict, max_length=20)
    collection: dict[str, Compteur] = Field(default_factory=dict, max_length=100)
    journal: list[EntreeJournal] = Field(default_factory=list, max_length=60)


# --- API ---

@app.get("/api/health")
def health():
    return {"ok": True}


@app.post("/api/register", status_code=201)
def register(body: Inscription, request: Request):
    ip = request.headers.get("x-real-ip") or (request.client.host if request.client else "?")
    t = now()
    with _verrou:
        for cle in [k for k, v in _inscriptions.items() if v[-1] <= t - 3600]:
            del _inscriptions[cle]
        recentes = [x for x in _inscriptions.get(ip, []) if x > t - 3600]
        if len(recentes) >= INSCRIPTIONS_PAR_HEURE:
            raise ApiError(429, "trop_de_requetes")
        _inscriptions[ip] = recentes + [t]

    jeton = secrets.token_urlsafe(32)
    alphabet = string.ascii_lowercase + string.digits
    with db.tx() as c:
        while True:
            tid = "".join(secrets.choice(alphabet) for _ in range(8))
            if not c.execute("SELECT 1 FROM turtles WHERE id = ?", (tid,)).fetchone():
                break
        pris = {r[0] for r in c.execute("SELECT name_key FROM turtles")}
        libres = [n for n in catalogue.NOMS if n.casefold() not in pris]
        if libres:
            nom = secrets.choice(libres)
        else:
            while True:
                nom = f"{secrets.choice(catalogue.NOMS)} {secrets.randbelow(998) + 2}"
                if nom.casefold() not in pris:
                    break
        c.execute(
            "INSERT INTO turtles (id, token_hash, name, name_key, created, last_seen, version) "
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            (tid, hashlib.sha256(jeton.encode()).hexdigest(), nom, nom.casefold(), t, t, body.version),
        )
    return {"id": tid, "token": jeton, "name": nom}


@app.post("/api/heartbeat")
def heartbeat(body: Battement, moi: Moi):
    t = now()
    with _verrou:
        if t - _dernier_hb.get(moi, t - INTERVALLE_HB) < INTERVALLE_HB:
            raise ApiError(429, "trop_de_requetes")
        _dernier_hb[moi] = t
    with db.tx() as c:
        c.execute(
            "UPDATE turtles SET version = ?, status = ?, tier = ?, accept_messages = ? WHERE id = ?",
            (body.version, body.status, body.tier, int(body.accept_messages), moi),
        )
        c.executemany(
            "UPDATE events SET acked = 1 WHERE eid = ? AND turtle_id = ?",
            [(eid, moi) for eid in body.ack],
        )
        maintenance(c, t)
        evs = c.execute(
            "SELECT eid, payload FROM events WHERE turtle_id = ? AND acked = 0 ORDER BY eid LIMIT 50",
            (moi,),
        ).fetchall()
        taille = c.execute("SELECT COUNT(*) FROM turtles WHERE last_seen >= ?", (t - TRENTE_JOURS,)).fetchone()[0]
        en_ligne = c.execute("SELECT COUNT(*) FROM turtles WHERE last_seen >= ?", (t - EN_LIGNE,)).fetchone()[0]
    return {
        "events": [{"eid": e["eid"], **json.loads(e["payload"])} for e in evs],
        "now": t,
        "band_size": taille,
        "online": en_ligne,
    }


@app.post("/api/visit", status_code=201)
def visit(body: DemandeVisite, moi: Moi):
    t = now()
    with db.tx() as c:
        if c.execute(
            "SELECT 1 FROM visits WHERE from_id = ? AND state IN ('pending', 'active')", (moi,)
        ).fetchone():
            raise ApiError(409, "deja_en_visite")
        recentes = c.execute(
            "SELECT COUNT(*) FROM visits WHERE from_id = ? AND created > ?", (moi, t - 3600)
        ).fetchone()[0]
        if recentes >= VISITES_PAR_HEURE:
            raise ApiError(429, "trop_de_visites")

        eligibles = c.execute(
            """
            SELECT t.id, t.name, t.accept_messages, COALESCE(f.points, 0) AS points
            FROM turtles t
            LEFT JOIN friendships f ON f.a = MIN(t.id, :moi) AND f.b = MAX(t.id, :moi)
            WHERE t.id != :moi
              AND (:to IS NULL OR t.id = :to)
              AND t.last_seen >= :seuil
              AND t.status = 'home'
              AND NOT EXISTS (SELECT 1 FROM visits v WHERE v.state IN ('pending', 'active')
                              AND (v.from_id = t.id OR v.to_id = t.id))
              AND NOT EXISTS (SELECT 1 FROM blocks b WHERE (b.blocker = :moi AND b.blocked = t.id)
                                                      OR (b.blocker = t.id AND b.blocked = :moi))
            """,
            {"moi": moi, "to": body.to, "seuil": t - EN_LIGNE},
        ).fetchall()
        if not eligibles:
            raise ApiError(409, "indisponible" if body.to else "personne")
        hote = random.choices(eligibles, weights=[r["points"] + 1 for r in eligibles])[0]

        message = nettoyer(body.message, 80) if hote["accept_messages"] else None
        duree = random.randint(180, 360)
        vid = c.execute(
            "INSERT INTO visits (from_id, to_id, message, state, created, duration) "
            "VALUES (?, ?, ?, 'pending', ?, ?)",
            (moi, hote["id"], message, t, duree),
        ).lastrowid
        visiteur = c.execute("SELECT name, tier FROM turtles WHERE id = ?", (moi,)).fetchone()
        evenement(c, hote["id"], {
            "type": "visitor",
            "visit": {
                "id": vid,
                "from": {"id": moi, "name": visiteur["name"], "tier": visiteur["tier"]},
                "message": message,
                "duration": duree,
            },
        }, t)
        c.execute("UPDATE turtles SET status = 'visiting' WHERE id = ?", (moi,))
    return {"visit": {"id": vid, "host": {"id": hote["id"], "name": hote["name"]}, "duration": duree}}


@app.post("/api/visit/{vid}/accept")
def visit_accept(vid: int, moi: Moi):
    with db.tx() as c:
        v = charger_visite(c, vid)
        if v["to_id"] != moi:
            raise ApiError(403, "interdit")
        if v["state"] == "pending":
            c.execute("UPDATE visits SET state = 'active', accepted = ? WHERE id = ?", (now(), vid))
        elif v["state"] != "active":  # déjà active : appel répété, sans effet
            raise ApiError(409, "etat")
    return {"ok": True}


@app.post("/api/visit/{vid}/end")
def visit_end(vid: int, body: FinVisite, moi: Moi):
    played = list(dict.fromkeys(body.played))[:5]  # dédoublonné, ordre conservé
    with db.tx() as c:
        v = charger_visite(c, vid)
        if v["to_id"] != moi:
            raise ApiError(403, "interdit")
        if v["state"] not in ("pending", "active"):
            raise ApiError(409, "etat")
        total = terminer(c, v, body.souvenir, played, now())
    return {"ok": True, "friendship": total}


@app.post("/api/visit/{vid}/abort")
def visit_abort(vid: int, moi: Moi):
    t = now()
    with db.tx() as c:
        v = charger_visite(c, vid)
        if moi not in (v["from_id"], v["to_id"]):
            raise ApiError(403, "interdit")
        if v["state"] not in ("pending", "active"):
            raise ApiError(409, "etat")
        clore(c, v, "aborted", t)
        if moi == v["to_id"]:
            # L'hôte interrompt : le visiteur rentre sans souvenir.
            evenement_retour(c, v, None, [], "interrompue", t)
        else:
            # Le visiteur rentre de lui-même : on prévient l'hôte.
            evenement(c, v["to_id"], {"type": "visit_cancelled", "visit": {"id": v["id"]}}, t)
    return {"ok": True}


@app.get("/api/band")
def band(moi: Moi):
    t = now()
    with db.lecture() as c:
        rows = c.execute(
            """
            SELECT t.id, t.name, t.tier, t.status, t.last_seen, COALESCE(f.points, 0) AS points,
                   EXISTS (SELECT 1 FROM blocks b WHERE b.blocker = :moi AND b.blocked = t.id) AS bloque
            FROM turtles t
            LEFT JOIN friendships f ON f.a = MIN(t.id, :moi) AND f.b = MAX(t.id, :moi)
            WHERE t.id != :moi AND t.last_seen >= :depuis
            """,
            {"moi": moi, "depuis": t - TRENTE_JOURS},
        ).fetchall()
    rows = sorted(rows, key=lambda r: (r["last_seen"] < t - EN_LIGNE, pages.cle_tri(r["name"])))[:200]
    return {"turtles": [{
        "id": r["id"],
        "name": r["name"],
        "tier": r["tier"],
        "online": r["last_seen"] >= t - EN_LIGNE,
        "status": r["status"],
        "friendship": r["points"],
        "blocked": bool(r["bloque"]),
    } for r in rows]}


@app.post("/api/rename")
def rename(body: Renommage, moi: Moi):
    nom = nom_valide(body.name)
    if nom is None:
        raise ApiError(400, "nom_invalide")
    with db.tx() as c:
        if c.execute(
            "SELECT 1 FROM turtles WHERE name_key = ? AND id != ?", (nom.casefold(), moi)
        ).fetchone():
            raise ApiError(409, "nom_pris")
        c.execute("UPDATE turtles SET name = ?, name_key = ? WHERE id = ?", (nom, nom.casefold(), moi))
    return {"name": nom}


def _cible(c, moi: str, cible: str) -> None:
    if cible == moi:
        raise ApiError(400, "soi_meme")
    if not c.execute("SELECT 1 FROM turtles WHERE id = ?", (cible,)).fetchone():
        raise ApiError(404, "introuvable")


@app.post("/api/block")
def block(body: Cible, moi: Moi):
    with db.tx() as c:
        _cible(c, moi, body.id)
        c.execute("INSERT OR IGNORE INTO blocks (blocker, blocked) VALUES (?, ?)", (moi, body.id))
    return {"ok": True}


@app.post("/api/unblock")
def unblock(body: Cible, moi: Moi):
    with db.tx() as c:
        _cible(c, moi, body.id)
        c.execute("DELETE FROM blocks WHERE blocker = ? AND blocked = ?", (moi, body.id))
    return {"ok": True}


@app.put("/api/carnet")
def carnet(body: Carnet, moi: Moi):
    journal = []
    for entree in body.journal:
        texte = nettoyer(entree.text, 140)
        if texte:
            journal.append({"t": entree.t, "text": texte})
    normalise = {
        "tier": body.tier,
        "bond": body.bond,
        "stats": body.stats,
        "collection": {k: n for k, n in body.collection.items() if k in SOUVENIRS},
        "journal": journal,
    }
    with db.tx() as c:
        c.execute(
            "UPDATE turtles SET carnet_json = ?, carnet_updated = ?, tier = ? WHERE id = ?",
            (json.dumps(normalise, ensure_ascii=False), now(), body.tier, moi),
        )
    return {"ok": True}


# --- pages publiques ---

@app.get("/t/{tid}")
def page_tortue(tid: str):
    return pages.carnet(tid, now())


@app.get("/bande")
def page_bande():
    return pages.bande(now())


@app.get("/api/public/band")
def bande_publique():
    """Même contenu que la page /bande, en JSON, pour la page de téléchargement (/friend/)."""
    t = now()
    with db.lecture() as c:
        rows = c.execute(
            "SELECT id, name, tier, last_seen FROM turtles WHERE last_seen >= ?", (t - TRENTE_JOURS,)
        ).fetchall()
    rows = sorted(rows, key=lambda r: (r["last_seen"] < t - EN_LIGNE, pages.cle_tri(r["name"])))
    tortues = [
        {"id": r["id"], "name": r["name"], "tier": r["tier"], "online": r["last_seen"] >= t - EN_LIGNE}
        for r in rows[:200]
    ]
    return JSONResponse(
        {"size": len(rows), "online": sum(1 for x in tortues if x["online"]), "turtles": tortues},
        headers={"Cache-Control": "public, max-age=30"},
    )
