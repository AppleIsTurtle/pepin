"""Tests du serveur « bande » : flux complet des visites, sécurité et pages."""

import hashlib
import json
import sqlite3
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import app as serveur  # noqa: E402
import catalogue  # noqa: E402
import db  # noqa: E402
import pages  # noqa: E402

T0 = 1_780_000_000  # heure de départ des tests (été 2026)


class Horloge:
    def __init__(self):
        self.t = T0

    def avance(self, s):
        self.t += s


@pytest.fixture
def horloge(monkeypatch):
    h = Horloge()
    monkeypatch.setattr(serveur, "now", lambda: h.t)
    return h


@pytest.fixture
def client(tmp_path, monkeypatch, horloge):
    monkeypatch.setattr(db, "DB_PATH", str(tmp_path / "bande.db"))
    serveur._dernier_hb.clear()
    serveur._inscriptions.clear()
    with TestClient(serveur.app) as c:
        yield c


_ip = [0]


def inscrire(client, version="1.0"):
    _ip[0] += 1
    r = client.post("/api/register", json={"version": version}, headers={"X-Real-IP": f"10.0.0.{_ip[0] % 250}"})
    assert r.status_code == 201, r.text
    d = r.json()
    d["h"] = {"Authorization": f"Bearer {d['token']}"}
    return d


def battement(client, tortue, horloge, status="home", ack=None, accept_messages=True, tier=0):
    horloge.avance(5)  # respecte la limite d'un heartbeat toutes les 4 s
    corps = {"version": "1.0", "status": status, "tier": tier, "accept_messages": accept_messages}
    if ack is not None:
        corps["ack"] = ack
    r = client.post("/api/heartbeat", json=corps, headers=tortue["h"])
    assert r.status_code == 200, r.text
    return r.json()


def sql(requete, *params):
    c = sqlite3.connect(db.DB_PATH)
    try:
        return c.execute(requete, params).fetchall()
    finally:
        c.close()


def visiter(client, visiteur, hote=None, message=None):
    corps = {"to": hote["id"] if hote else None, "message": message}
    return client.post("/api/visit", json=corps, headers=visiteur["h"])


# --- bases ---

def test_health(client):
    r = client.get("/api/health")
    assert r.status_code == 200 and r.json() == {"ok": True}


def test_inscription(client):
    a = inscrire(client)
    assert len(a["id"]) == 8 and a["id"].isalnum() and a["id"] == a["id"].lower()
    assert len(a["token"]) >= 40
    assert a["name"] in catalogue.NOMS
    ligne = sql("SELECT token_hash, status, last_seen FROM turtles WHERE id = ?", a["id"])[0]
    assert ligne[0] == hashlib.sha256(a["token"].encode()).hexdigest()
    assert a["token"] not in json.dumps(sql("SELECT * FROM turtles"))
    assert ligne[1] == "home" and ligne[2] == T0


def test_noms_catalogue():
    assert len(catalogue.NOMS) >= 60
    assert len({n.casefold() for n in catalogue.NOMS}) == len(catalogue.NOMS)
    assert all(serveur.nom_valide(n) == n for n in catalogue.NOMS)


def test_noms_suffixe_quand_tous_pris(client, monkeypatch):
    monkeypatch.setattr(catalogue, "NOMS", ["Pistache"])
    noms = {inscrire(client)["name"] for _ in range(4)}
    assert "Pistache" in noms and len(noms) == 4
    assert all(n.startswith("Pistache") for n in noms)


def test_inscription_limite_par_ip(client, horloge):
    h = {"X-Real-IP": "1.2.3.4"}
    for _ in range(10):
        assert client.post("/api/register", json={"version": "1"}, headers=h).status_code == 201
    r = client.post("/api/register", json={"version": "1"}, headers=h)
    assert r.status_code == 429
    # une autre IP passe, et la limite retombe au bout d'une heure
    assert client.post("/api/register", json={"version": "1"}, headers={"X-Real-IP": "5.6.7.8"}).status_code == 201
    horloge.avance(3601)
    assert client.post("/api/register", json={"version": "1"}, headers=h).status_code == 201


@pytest.mark.parametrize("methode,chemin,corps", [
    ("post", "/api/heartbeat", {"version": "1", "status": "home", "tier": 0, "accept_messages": True}),
    ("post", "/api/visit", {"to": None}),
    ("post", "/api/visit/1/accept", None),
    ("post", "/api/visit/1/end", {}),
    ("post", "/api/visit/1/abort", None),
    ("get", "/api/band", None),
    ("post", "/api/rename", {"name": "Zut"}),
    ("post", "/api/block", {"id": "abcdefgh"}),
    ("post", "/api/unblock", {"id": "abcdefgh"}),
    ("put", "/api/carnet", {"tier": 0, "bond": 0}),
])
def test_401_sans_jeton(client, methode, chemin, corps):
    for entetes in ({}, {"Authorization": "Bearer faux"}, {"Authorization": "Basic abc"}):
        r = client.request(methode.upper(), chemin, json=corps, headers=entetes)
        assert r.status_code == 401, (chemin, entetes)
        assert r.json() == {"error": "auth"}


def test_last_seen_mis_a_jour(client, horloge):
    a = inscrire(client)
    horloge.avance(1000)
    client.get("/api/band", headers=a["h"])
    assert sql("SELECT last_seen FROM turtles WHERE id = ?", a["id"])[0][0] == T0 + 1000


def test_corps_trop_gros(client):
    a = inscrire(client)
    gros = {"version": "1", "status": "home", "tier": 0, "accept_messages": True, "x": "a" * 40_000}
    r = client.post("/api/heartbeat", json=gros, headers=a["h"])
    assert r.status_code == 413
    # même sans Content-Length (envoi par morceaux)
    r = client.post("/api/register", content=iter([b"{" + b" " * 20_000, b" " * 20_000 + b"}"]))
    assert r.status_code == 413


def test_validation(client, horloge):
    a = inscrire(client)
    for corps in (
        {"version": "1", "status": "dehors", "tier": 0, "accept_messages": True},
        {"version": "1", "status": "home", "tier": 5, "accept_messages": True},
        {"version": "1", "status": "home", "tier": 0},
        {"version": "x" * 33, "status": "home", "tier": 0, "accept_messages": True},
    ):
        horloge.avance(5)
        r = client.post("/api/heartbeat", json=corps, headers=a["h"])
        assert r.status_code == 422 and r.json() == {"error": "invalide"}
    r = client.post("/api/register", content=b"pas du json", headers={"Content-Type": "application/json"})
    assert r.status_code == 422


# --- heartbeat ---

def test_heartbeat(client, horloge):
    a, b = inscrire(client), inscrire(client)
    d = battement(client, a, horloge, status="sleeping", tier=3, accept_messages=False)
    assert d["events"] == [] and d["now"] == horloge.t
    assert d["band_size"] == 2 and d["online"] == 2
    assert sql("SELECT status, tier, accept_messages FROM turtles WHERE id = ?", a["id"])[0] == ("sleeping", 3, 0)
    horloge.avance(200)
    d = battement(client, a, horloge)
    assert d["online"] == 1  # b n'a plus donné de nouvelles
    assert b["id"]


def test_heartbeat_limite(client, horloge):
    a = inscrire(client)
    corps = {"version": "1", "status": "home", "tier": 0, "accept_messages": True}
    assert client.post("/api/heartbeat", json=corps, headers=a["h"]).status_code == 200
    horloge.avance(3)
    assert client.post("/api/heartbeat", json=corps, headers=a["h"]).status_code == 429
    horloge.avance(1)
    assert client.post("/api/heartbeat", json=corps, headers=a["h"]).status_code == 200


# --- visites ---

def test_flux_complet_de_visite(client, horloge):
    a, b = inscrire(client), inscrire(client)
    battement(client, a, horloge)
    battement(client, b, horloge)

    r = visiter(client, a, b, message="  Coucou\n\t toi\x07 !  ")
    assert r.status_code == 201
    v = r.json()["visit"]
    assert v["host"] == {"id": b["id"], "name": b["name"]}
    assert 180 <= v["duration"] <= 360
    assert sql("SELECT status FROM turtles WHERE id = ?", a["id"])[0][0] == "visiting"

    evs = battement(client, b, horloge)["events"]
    assert len(evs) == 1
    ev = evs[0]
    assert ev["type"] == "visitor"
    assert ev["visit"] == {
        "id": v["id"], "from": {"id": a["id"], "name": a["name"], "tier": 0},
        "message": "Coucou toi !", "duration": v["duration"],
    }
    # non acquitté : relivré ; acquitté : disparaît
    assert [e["eid"] for e in battement(client, b, horloge)["events"]] == [ev["eid"]]
    assert battement(client, b, horloge, ack=[ev["eid"]])["events"] == []

    assert client.post(f"/api/visit/{v['id']}/accept", headers=b["h"]).status_code == 200
    assert client.post(f"/api/visit/{v['id']}/accept", headers=b["h"]).status_code == 200  # idempotent
    assert sql("SELECT state, accepted FROM visits WHERE id = ?", v["id"])[0] == ("active", horloge.t)

    r = client.post(f"/api/visit/{v['id']}/end", headers=b["h"],
                    json={"souvenir": "fraise-doree", "played": ["chat", "danse", "chat", "sieste"]})
    assert r.status_code == 200 and r.json()["friendship"] == 2

    evs = battement(client, a, horloge, status="visiting")["events"]
    assert len(evs) == 1 and evs[0]["type"] == "return"
    assert evs[0]["visit"] == {
        "id": v["id"], "host": {"id": b["id"], "name": b["name"]},
        "souvenir": "fraise-doree", "souvenir_label": "une fraise dorée",
        "played": ["chat", "danse", "sieste"], "friendship": 2, "reason": None,
    }
    assert sql("SELECT state, souvenir FROM visits WHERE id = ?", v["id"])[0] == ("done", "fraise-doree")
    # déjà terminée
    assert client.post(f"/api/visit/{v['id']}/end", headers=b["h"], json={}).status_code == 409

    # deuxième visite, moins d'activités : +1 seulement
    battement(client, a, horloge)
    v2 = visiter(client, a, b).json()["visit"]
    client.post(f"/api/visit/{v2['id']}/accept", headers=b["h"])
    r = client.post(f"/api/visit/{v2['id']}/end", headers=b["h"], json={"souvenir": None, "played": ["chat"]})
    assert r.json()["friendship"] == 3


def test_fin_validation_listes_blanches(client, horloge):
    a, b = inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    for corps in ({"souvenir": "diamant"}, {"played": ["boxe"]}):
        assert client.post(f"/api/visit/{v['id']}/end", headers=b["h"], json=corps).status_code == 422


def test_permissions_visite(client, horloge):
    a, b, c = inscrire(client), inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    assert client.post(f"/api/visit/{v['id']}/accept", headers=a["h"]).status_code == 403
    assert client.post(f"/api/visit/{v['id']}/end", headers=a["h"], json={}).status_code == 403
    assert client.post(f"/api/visit/{v['id']}/abort", headers=c["h"]).status_code == 403
    assert client.post("/api/visit/9999/accept", headers=b["h"]).status_code == 404


def test_message_nettoye_et_tronque(client, horloge):
    a, b = inscrire(client), inscrire(client)
    visiter(client, a, b, message="x" * 200)
    assert sql("SELECT message FROM visits")[0][0] == "x" * 80
    assert serveur.nettoyer(" ‮\x00 ", 80) is None
    assert serveur.nettoyer("a​b", 80) == "ab"


def test_message_refuse_par_hote(client, horloge):
    a, b = inscrire(client), inscrire(client)
    battement(client, b, horloge, accept_messages=False)
    visiter(client, a, b, message="Salut")
    assert sql("SELECT message FROM visits")[0][0] is None
    ev = battement(client, b, horloge, accept_messages=False)["events"][0]
    assert ev["visit"]["message"] is None


def test_regles_eligibilite(client, horloge):
    a, b, c = inscrire(client), inscrire(client), inscrire(client)
    # soi-même
    assert visiter(client, a, a).json() == {"error": "indisponible"}
    # inconnue
    assert visiter(client, a, {"id": "zzzzzzzz"}).status_code == 409
    # pas à la maison
    battement(client, b, horloge, status="sleeping")
    r = visiter(client, a, b)
    assert r.status_code == 409 and r.json() == {"error": "indisponible"}
    # hors ligne
    battement(client, b, horloge, status="home")
    horloge.avance(151)
    battement(client, a, horloge)
    battement(client, c, horloge)
    assert visiter(client, a, b).json() == {"error": "indisponible"}
    # déjà occupée (c reçoit a) : b ne peut pas aller chez c
    battement(client, b, horloge)
    assert visiter(client, a, c).status_code == 201
    assert visiter(client, b, c).json() == {"error": "indisponible"}
    # a est déjà en visite
    assert visiter(client, a, b).json() == {"error": "deja_en_visite"}
    # a est visiteur : b ne peut pas aller chez a non plus
    assert visiter(client, b, a).json() == {"error": "indisponible"}


def test_visite_au_hasard_personne(client, horloge):
    a = inscrire(client)
    r = visiter(client, a)
    assert r.status_code == 409 and r.json() == {"error": "personne"}
    b, c = inscrire(client), inscrire(client)
    battement(client, c, horloge, status="away")
    r = visiter(client, a)
    assert r.status_code == 201 and r.json()["visit"]["host"]["id"] == b["id"]


def test_tirage_pondere_par_amitie(client, horloge, monkeypatch):
    a, b, c = inscrire(client), inscrire(client), inscrire(client)
    sql_ecrire(*sorted([a["id"], b["id"]]), 9)
    vus = {}

    def choices(pop, weights):
        vus.update({r["id"]: w for r, w in zip(pop, weights)})
        return [pop[0]]

    monkeypatch.setattr(serveur.random, "choices", choices)
    assert visiter(client, a).status_code == 201
    assert vus == {b["id"]: 10, c["id"]: 1}


def sql_ecrire(x, y, pts):
    c = sqlite3.connect(db.DB_PATH)
    c.execute("INSERT INTO friendships (a, b, points) VALUES (?, ?, ?)", (x, y, pts))
    c.commit()
    c.close()


def test_limite_visites_par_heure(client, horloge):
    a, b = inscrire(client), inscrire(client)
    for _ in range(6):
        v = visiter(client, a, b).json()["visit"]
        assert client.post(f"/api/visit/{v['id']}/abort", headers=a["h"]).status_code == 200
        battement(client, b, horloge)  # garde l'hôte en ligne
    r = visiter(client, a, b)
    assert r.status_code == 429 and r.json() == {"error": "trop_de_visites"}
    horloge.avance(3600)
    battement(client, b, horloge)
    assert visiter(client, a, b).status_code == 201


def test_annulation_par_hote(client, horloge):
    a, b = inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    client.post(f"/api/visit/{v['id']}/accept", headers=b["h"])
    assert client.post(f"/api/visit/{v['id']}/abort", headers=b["h"]).status_code == 200
    assert sql("SELECT status FROM turtles WHERE id = ?", a["id"])[0][0] == "away"
    ev = battement(client, a, horloge, status="visiting")["events"]
    assert len(ev) == 1 and ev[0]["type"] == "return"
    assert ev[0]["visit"]["reason"] == "interrompue" and ev[0]["visit"]["souvenir"] is None
    assert sql("SELECT state FROM visits")[0][0] == "aborted"
    assert client.post(f"/api/visit/{v['id']}/abort", headers=b["h"]).status_code == 409


def test_annulation_par_visiteur(client, horloge):
    a, b = inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    battement(client, b, horloge)  # l'hôte voit arriver le visiteur (non acquitté)
    assert client.post(f"/api/visit/{v['id']}/abort", headers=a["h"]).status_code == 200
    assert battement(client, a, horloge)["events"] == []
    ev = battement(client, b, horloge)["events"]
    assert [e["type"] for e in ev] == ["visit_cancelled"]  # l'annonce périmée a été retirée
    assert ev[0]["visit"] == {"id": v["id"]}


def test_personne_a_la_maison(client, horloge):
    a, b = inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    horloge.avance(140)
    battement(client, a, horloge, status="visiting")
    assert sql("SELECT state FROM visits")[0][0] == "pending"
    horloge.avance(20)
    ev = battement(client, a, horloge, status="visiting")["events"]
    assert ev[0]["type"] == "return" and ev[0]["visit"]["reason"] == "personne_a_la_maison"
    assert ev[0]["visit"]["souvenir"] is None and ev[0]["visit"]["id"] == v["id"]
    assert sql("SELECT state FROM visits")[0][0] == "no_one_home"
    assert sql("SELECT status FROM turtles WHERE id = ?", a["id"])[0][0] == "away"
    # l'hôte ne reçoit pas un visiteur fantôme, et ne peut plus l'accepter
    assert battement(client, b, horloge)["events"] == []
    assert client.post(f"/api/visit/{v['id']}/accept", headers=b["h"]).status_code == 409
    # maintenance idempotente : pas de deuxième évènement
    ack = [e["eid"] for e in ev]
    assert battement(client, a, horloge, ack=ack)["events"] == []


def test_fin_automatique_visite_active(client, horloge):
    a, b = inscrire(client), inscrire(client)
    v = visiter(client, a, b).json()["visit"]
    client.post(f"/api/visit/{v['id']}/accept", headers=b["h"])
    horloge.avance(v["duration"] + 180 - 5)  # battement() avance encore de 5 s
    battement(client, a, horloge, status="visiting")
    assert sql("SELECT state FROM visits")[0][0] == "active"
    horloge.avance(1)
    ev = battement(client, a, horloge, status="visiting")["events"]
    assert len(ev) == 1 and ev[0]["type"] == "return"
    assert ev[0]["visit"]["souvenir"] in catalogue.SOUVENIRS
    assert ev[0]["visit"]["souvenir_label"] == catalogue.SOUVENIRS[ev[0]["visit"]["souvenir"]]
    assert ev[0]["visit"]["played"] == [] and ev[0]["visit"]["friendship"] == 1
    assert sql("SELECT state FROM visits")[0][0] == "done"


def test_blocage(client, horloge):
    a, b = inscrire(client), inscrire(client)
    assert client.post("/api/block", json={"id": b["id"]}, headers=a["h"]).status_code == 200
    assert client.post("/api/block", json={"id": b["id"]}, headers=a["h"]).status_code == 200
    assert visiter(client, a, b).json() == {"error": "indisponible"}
    assert visiter(client, b, a).json() == {"error": "indisponible"}
    assert visiter(client, b).json() == {"error": "personne"}
    band = client.get("/api/band", headers=a["h"]).json()["turtles"]
    assert band[0]["blocked"] is True
    assert client.post("/api/unblock", json={"id": b["id"]}, headers=a["h"]).status_code == 200
    assert visiter(client, b, a).status_code == 201
    # cibles invalides
    assert client.post("/api/block", json={"id": a["id"]}, headers=a["h"]).status_code == 400
    assert client.post("/api/block", json={"id": "zzzzzzzz"}, headers=a["h"]).status_code == 404
    assert client.post("/api/block", json={"id": "<script>"}, headers=a["h"]).status_code == 422


# --- bande, renommage, carnet ---

def test_band(client, horloge):
    a, b, c, d = (inscrire(client) for _ in range(4))
    client.post("/api/rename", json={"name": "Zébulon"}, headers=b["h"])
    client.post("/api/rename", json={"name": "Écureuil"}, headers=c["h"])
    client.post("/api/rename", json={"name": "Ancien"}, headers=d["h"])
    sql_ecrire(*sorted([a["id"], c["id"]]), 4)
    horloge.avance(200)
    battement(client, a, horloge)
    battement(client, b, horloge)
    liste = client.get("/api/band", headers=a["h"]).json()["turtles"]
    assert [t["name"] for t in liste] == ["Zébulon", "Ancien", "Écureuil"]  # en ligne d'abord, puis par nom
    assert liste[0]["online"] is True and liste[1]["online"] is False
    assert liste[2]["friendship"] == 4 and liste[0]["friendship"] == 0
    assert set(liste[0]) == {"id", "name", "tier", "online", "status", "friendship", "blocked"}
    # plus de 30 jours sans nouvelles : disparaît
    horloge.avance(30 * 86400)
    battement(client, a, horloge)
    assert client.get("/api/band", headers=a["h"]).json()["turtles"] == []


def test_renommage(client, horloge):
    a, b = inscrire(client), inscrire(client)
    r = client.post("/api/rename", json={"name": "  Élo d'Artagnan-2 "}, headers=a["h"])
    assert r.status_code == 200 and r.json() == {"name": "Élo d'Artagnan-2"}
    for nom in ("x", "a" * 17, "<b>gras</b>", "tab\tici", "ok_non", "😀😀"):
        r = client.post("/api/rename", json={"name": nom}, headers=b["h"])
        assert r.status_code == 400 and r.json() == {"error": "nom_invalide"}, nom
    r = client.post("/api/rename", json={"name": "élo D'ARTAGNAN-2"}, headers=b["h"])
    assert r.status_code == 409 and r.json() == {"error": "nom_pris"}
    # changer la casse de son propre nom est permis
    assert client.post("/api/rename", json={"name": "ÉLO d'artagnan-2"}, headers=a["h"]).status_code == 200


def test_carnet(client, horloge):
    a = inscrire(client)
    corps = {
        "tier": 2, "bond": 0.42,
        "stats": {"snacks": 12, "naps": 3, "truc_perso": 7},
        "collection": {"fraise": 3, "diamant": 9, "plume": 0},
        "journal": [{"t": T0 - 10, "text": "Il a\x00 mangé\n une   fraise"}, {"t": T0 - 5, "text": "\x01 "}],
    }
    assert client.put("/api/carnet", json=corps, headers=a["h"]).status_code == 200
    stocke = json.loads(sql("SELECT carnet_json FROM turtles")[0][0])
    assert stocke["collection"] == {"fraise": 3, "plume": 0}
    assert stocke["journal"] == [{"t": T0 - 10, "text": "Il a mangé une fraise"}]
    assert stocke["tier"] == 2 and stocke["bond"] == 0.42
    assert sql("SELECT tier, carnet_updated FROM turtles")[0] == (2, T0)

    long = dict(corps, journal=[{"t": 1, "text": "é" * 300}])
    client.put("/api/carnet", json=long, headers=a["h"])
    assert len(json.loads(sql("SELECT carnet_json FROM turtles")[0][0])["journal"][0]["text"]) == 140


@pytest.mark.parametrize("modif", [
    {"journal": [{"t": 1, "text": "x"}] * 61},
    {"stats": {f"k{i}": 1 for i in range(21)}},
    {"stats": {"k" * 25: 1}},
    {"stats": {"<script>": 1}},
    {"stats": {"snacks": -1}},
    {"tier": 9},
    {"bond": "nan"},
])
def test_carnet_invalide(client, modif):
    a = inscrire(client)
    corps = dict({"tier": 0, "bond": 0.0, "stats": {}, "collection": {}, "journal": []}, **modif)
    r = client.put("/api/carnet", json=corps, headers=a["h"])
    assert r.status_code == 422, modif


# --- pages ---

def _verifie_entetes(r):
    assert r.headers["content-type"] == "text/html; charset=utf-8"
    assert r.headers["cache-control"] == "no-cache"
    csp = r.headers["content-security-policy"]
    assert "default-src 'none'" in csp and "script-src" not in csp and "unsafe-inline" not in csp


def test_page_carnet(client, horloge):
    a, b = inscrire(client), inscrire(client)
    client.post("/api/rename", json={"name": "Galette"}, headers=b["h"])
    client.put("/api/carnet", headers=a["h"], json={
        "tier": 3, "bond": 1.0,
        "stats": {"naps": 4, "snacks": 1200, "zz_inconnue": 2},
        "collection": {"coquillage": 2},
        "journal": [
            {"t": T0 - 3 * 86400, "text": "Ancien <script>alert(1)</script>"},
            {"t": T0 - 7200, "text": "Récent & joyeux"},
        ],
    })
    v = visiter(client, a, b).json()["visit"]
    client.post(f"/api/visit/{v['id']}/accept", headers=b["h"])
    client.post(f"/api/visit/{v['id']}/end", headers=b["h"], json={"souvenir": "plume", "played": ["gouter", "renifler"]})

    r = client.get(f"/t/{a['id']}")
    assert r.status_code == 200
    _verifie_entetes(r)
    h = r.text
    assert '<html lang="fr">' in h and '<meta charset="utf-8">' in h
    assert "/friend/img/icone-256.png" in h and "/friend/img/neutre.png" in h
    assert "Meilleur ami" in h and "En ligne" in h
    assert "fraises et salades croquées" in h and "1 200" in h and "siestes" in h
    assert h.index("fraises et salades croquées") < h.index("siestes") < h.index("zz_inconnue")
    assert '/friend/img/items/coquillage.png' in h and 'alt="un coquillage"' in h and "× 2" in h
    assert f'href="/friend/t/{b["id"]}">Galette</a>' in h and "1 point d'amitié" in h
    assert "Visite chez" in h and "Ont partagé un goûter · se sont reniflés · souvenir rapporté : une plume" in h
    assert "<script>" not in h and "&lt;script&gt;alert(1)&lt;/script&gt;" in h
    assert "Récent &amp; joyeux" in h and "il y a 2 heures" in h
    assert h.index("Récent") < h.index("Ancien")  # plus récent d'abord
    assert "/friend/bande" in h and 'href="/friend/"' in h

    # côté hôte : visite reçue, souvenir offert
    h = client.get(f"/t/{b['id']}").text
    assert "Visite de" in h and "souvenir offert : une plume" in h
    assert "Ce carnet est encore vide." in h

    # hors ligne
    horloge.avance(3 * 3600)
    assert "Vue il y a 3 heures" in client.get(f"/t/{a['id']}").text


def test_page_introuvable(client):
    for chemin in ("/t/abcdefgh", "/t/%3Cscript%3E"):
        r = client.get(chemin)
        assert r.status_code == 404
        _verifie_entetes(r)
        assert "Tortue introuvable" in r.text and "<script>" not in r.text


def test_page_bande(client, horloge):
    a, b, c = inscrire(client), inscrire(client), inscrire(client)
    horloge.avance(200)
    battement(client, a, horloge)
    r = client.get("/bande")
    assert r.status_code == 200
    _verifie_entetes(r)
    h = r.text
    assert "3 tortues, 1 en ligne" in h
    for t in (a, b, c):
        assert f'href="/friend/t/{t["id"]}"' in h
    assert h.count('class="dot on"') == 1 and "Nouvelle tortue" in h
    assert 'href="/friend/"' in h


def test_page_bande_vide(client):
    h = client.get("/bande").text
    assert "0 tortue, 0 en ligne" in h and "Personne pour l'instant." in h


def test_csp_empreinte_style(client):
    import base64
    r = client.get("/bande")
    css = r.text.split("<style>", 1)[1].split("</style>", 1)[0]
    empreinte = base64.b64encode(hashlib.sha256(css.encode()).digest()).decode()
    assert f"'sha256-{empreinte}'" in r.headers["content-security-policy"]
    assert " style=" not in r.text  # aucun attribut style inline (bloqué par la CSP)


# --- dates ---

def test_il_y_a():
    assert pages.il_y_a(10) == "à l'instant"
    assert pages.il_y_a(60) == "il y a 1 minute"
    assert pages.il_y_a(125) == "il y a 2 minutes"
    assert pages.il_y_a(3600) == "il y a 1 heure"
    assert pages.il_y_a(86400) == "hier"
    assert pages.il_y_a(5 * 86400) == "il y a 5 jours"
    assert pages.il_y_a(65 * 86400) == "il y a 2 mois"
    assert pages.il_y_a(800 * 86400) == "il y a 2 ans"


def test_heure_paris():
    # 1er juillet 2026 12:00 UTC → 14:00 (été) ; 15 janvier 2026 12:00 UTC → 13:00 (hiver)
    assert pages.heure_paris(1782907200).strftime("%d/%m %H:%M") == "01/07 14:00"
    assert pages.heure_paris(1768478400).strftime("%d/%m %H:%M") == "15/01 13:00"
    # bascule du 25 octobre 2026 à 01:00 UTC
    assert pages.heure_paris(1792890000 - 1).hour == 2
    assert pages.heure_paris(1792890000).hour == 2 and pages.heure_paris(1792890000).minute == 0
    assert pages.date_fr(T0 - 3 * 86400, T0) == pages.heure_paris(T0 - 3 * 86400).strftime("%d/%m %H:%M")


def test_bande_publique(client, horloge):
    a = inscrire(client)
    b = inscrire(client)
    horloge.avance(200)                       # a et b passent hors ligne…
    battement(client, a, horloge)             # …puis a revient
    r = client.get("/api/public/band")        # sans jeton : public, comme la page /bande
    assert r.status_code == 200
    d = r.json()
    assert d["size"] == 2 and d["online"] == 1
    assert [t["id"] for t in d["turtles"]] == [a["id"], b["id"]]   # en ligne d'abord
    assert set(d["turtles"][0]) == {"id", "name", "tier", "online"}  # rien de plus que la page publique
    assert "token" not in r.text
