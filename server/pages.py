"""Pages HTML publiques : carnet d'une tortue (/t/{id}) et liste de la bande (/bande).

Tout contenu venant d'un client passe par html.escape. Aucun script : la CSP n'autorise
que la feuille de style inline (par son empreinte) et les images du même site.
"""

import base64
import hashlib
import json
import unicodedata
from datetime import datetime, timedelta, timezone
from html import escape as e

from fastapi.responses import HTMLResponse

import db
from catalogue import ACTIVITES, PALIERS, SOUVENIRS, STATS

EN_LIGNE = 150
TRENTE_JOURS = 30 * 86400

CSS = """
  :root {
    --bg: #f6f1e7; --paper: #fffdf8; --ink: #2b2620; --muted: #6f665a; --line: #e4dccd;
    --green: #82d650; --green-dark: #4fa83a; --ground: #e9dfcb;
  }
  @media (prefers-color-scheme: dark) {
    :root { --bg: #1c1a17; --paper: #25221e; --ink: #f1ebe0; --muted: #b3a998; --line: #3a352e; --ground: #2f2b25; }
  }
  * { box-sizing: border-box; }
  html { -webkit-text-size-adjust: 100%; }
  body {
    margin: 0; background: var(--bg); color: var(--ink);
    font: 17px/1.6 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  }
  a { color: inherit; text-decoration-color: var(--green); text-decoration-thickness: 2px; text-underline-offset: 3px; }
  img.px { image-rendering: pixelated; image-rendering: crisp-edges; display: block; }
  main { max-width: 860px; margin: 0 auto; padding: 0 16px 64px; overflow-wrap: anywhere; }

  .top { display: flex; align-items: flex-end; justify-content: space-between; gap: 12px;
         padding-top: 12px; border-bottom: 6px solid var(--ground); }
  .top a { text-decoration: none; font-weight: 600; }
  .top .home { display: flex; align-items: flex-end; gap: 6px; font-size: 20px; }
  .top .home img { width: 56px; height: 54px; margin-bottom: -6px; }
  .top .home span, .top > a:last-child { padding-bottom: 8px; }

  .hero { padding: 32px 0 0; }
  h1 { font-size: clamp(34px, 8vw, 52px); line-height: 1.05; margin: 0 0 8px; letter-spacing: -1px; }
  .sous { color: var(--muted); margin: 0; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
  section { margin-top: 40px; }
  h2 { font-size: 24px; margin: 0 0 14px; }
  .card { background: var(--paper); border: 1px solid var(--line); border-radius: 10px; padding: 18px 22px; }
  .vide { color: var(--muted); margin: 0; }

  table { width: 100%; border-collapse: collapse; }
  td { padding: 8px 0; border-top: 1px solid var(--line); vertical-align: top; }
  tr:first-child td { border-top: 0; padding-top: 0; }
  tr:last-child td { padding-bottom: 0; }
  td.n { text-align: right; font-weight: 600; font-variant-numeric: tabular-nums; padding-left: 12px; white-space: nowrap; }

  .items { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 12px; }
  .items figure { margin: 0; background: var(--paper); border: 1px solid var(--line); border-radius: 10px;
                  padding: 12px 8px; text-align: center; }
  .items img { width: 64px; height: 64px; margin: 0 auto 6px; }
  .items figcaption { font-size: 14px; color: var(--muted); line-height: 1.35; }
  .items b { display: block; color: var(--ink); font-size: 16px; }

  ul.liste { list-style: none; margin: 0; padding: 0; }
  ul.liste li { display: flex; align-items: baseline; gap: 4px 10px; flex-wrap: wrap;
                padding: 10px 0; border-top: 1px solid var(--line); }
  ul.liste li:first-child { border-top: 0; padding-top: 0; }
  ul.liste li:last-child { padding-bottom: 0; }
  .quand { color: var(--muted); font-size: 14px; margin-left: auto; white-space: nowrap; }
  .detail { flex-basis: 100%; color: var(--muted); font-size: 15px; }

  .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; background: var(--line);
         border: 1px solid var(--muted); flex: none; align-self: center; }
  .dot.on { background: var(--green); border-color: var(--green-dark); }
  .sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }

  footer { margin-top: 56px; color: var(--muted); font-size: 13px; text-align: center; }
"""

_EMPREINTE_CSS = base64.b64encode(hashlib.sha256(CSS.encode()).digest()).decode()

ENTETES = {
    "Cache-Control": "no-cache",
    "Content-Security-Policy": (
        f"default-src 'none'; img-src 'self'; style-src 'sha256-{_EMPREINTE_CSS}'; "
        "base-uri 'none'; form-action 'none'; frame-ancestors 'none'"
    ),
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "same-origin",
}


# --- dates en français ---

def _dernier_dimanche(annee: int, mois: int) -> datetime:
    """Dernier dimanche de mars ou d'octobre (31 jours tous les deux), 01:00 UTC."""
    d = datetime(annee, mois, 31, 1, tzinfo=timezone.utc)
    return d - timedelta(days=(d.weekday() + 1) % 7)


def heure_paris(ts: int) -> datetime:
    """Heure de Paris sans dépendre de la base tz du système (règle UE : heure d'été fin mars → fin octobre)."""
    utc = datetime.fromtimestamp(ts, timezone.utc)
    ete = _dernier_dimanche(utc.year, 3) <= utc < _dernier_dimanche(utc.year, 10)
    return utc + timedelta(hours=2 if ete else 1)


def _pluriel(n: int, mot: str) -> str:
    return f"{n} {mot}{'' if n < 2 or mot.endswith('s') else 's'}"  # en français, 0 reste au singulier


def il_y_a(secondes: int) -> str:
    s = max(0, secondes)
    if s < 60:
        return "à l'instant"
    minutes = s // 60
    if minutes < 60:
        return f"il y a {_pluriel(minutes, 'minute')}"
    heures = minutes // 60
    if heures < 24:
        return f"il y a {_pluriel(heures, 'heure')}"
    jours = heures // 24
    if jours == 1:
        return "hier"
    if jours < 30:
        return f"il y a {jours} jours"
    if jours < 365:
        return f"il y a {jours // 30} mois"
    ans = jours // 365
    return f"il y a {_pluriel(ans, 'an')}"


def date_fr(ts: int, t: int) -> str:
    """Relative si c'était dans les dernières 24 h, sinon jj/mm hh:mm (heure de Paris)."""
    if 0 <= t - ts < 86400:
        return il_y_a(t - ts)
    return heure_paris(ts).strftime("%d/%m %H:%M")


def cle_tri(nom: str) -> str:
    """Tri alphabétique qui ignore accents et casse."""
    sans_accents = "".join(ch for ch in unicodedata.normalize("NFD", nom) if not unicodedata.combining(ch))
    return sans_accents.casefold()


def _nombre(n: int) -> str:
    return f"{n:,}".replace(",", " ")


def _palier(tier: int) -> str:
    return PALIERS[tier] if 0 <= tier < len(PALIERS) else PALIERS[0]


def _pastille(en_ligne: bool) -> str:
    etat = "en ligne" if en_ligne else "hors ligne"
    return f'<span class="dot{" on" if en_ligne else ""}" title="{etat}"></span><span class="sr">{etat}</span>'


def _lien(tid: str, nom: str) -> str:
    return f'<a href="/friend/t/{e(tid)}">{e(nom)}</a>'


# --- gabarit ---

def document(titre: str, corps: str, statut: int = 200) -> HTMLResponse:
    page = f"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{e(titre)}</title>
<link rel="icon" type="image/png" href="/friend/img/icone-256.png">
<style>{CSS}</style>
</head>
<body>
<main>
<header class="top">
  <a class="home" href="/friend/"><img class="px" src="/friend/img/neutre.png" alt="" width="56" height="54"><span>Pépin</span></a>
  <a href="/friend/bande">La bande</a>
</header>
{corps}
<footer>Fait main sur <a href="/friend/">pommetortue.tech</a></footer>
</main>
</body>
</html>
"""
    return HTMLResponse(page, statut, headers=ENTETES)


def _section(titre: str, contenu: str) -> str:
    return f"<section>\n<h2>{titre}</h2>\n{contenu}\n</section>"


def _vide(texte: str) -> str:
    return f'<div class="card"><p class="vide">{texte}</p></div>'


def introuvable() -> HTMLResponse:
    corps = """<div class="hero">
<h1>Tortue introuvable</h1>
<p class="sous">Cette tortue n'existe pas, ou plus.</p>
</div>
<section><p><a href="/friend/bande">Voir la bande</a> · <a href="/friend/">Retour à Pépin</a></p></section>"""
    return document("Tortue introuvable", corps, 404)


# --- carnet d'une tortue ---

def carnet(tid: str, t: int) -> HTMLResponse:
    with db.lecture() as c:
        tortue = c.execute("SELECT * FROM turtles WHERE id = ?", (tid,)).fetchone()
        if tortue is None:
            return introuvable()
        amis = c.execute(
            """
            SELECT t.id, t.name, f.points FROM friendships f
            JOIN turtles t ON t.id = CASE WHEN f.a = :id THEN f.b ELSE f.a END
            WHERE (f.a = :id OR f.b = :id) AND f.points > 0
            ORDER BY f.points DESC, t.name
            LIMIT 5
            """,
            {"id": tid},
        ).fetchall()
        visites = c.execute(
            """
            SELECT v.*, a.name AS from_name, b.name AS to_name FROM visits v
            JOIN turtles a ON a.id = v.from_id
            JOIN turtles b ON b.id = v.to_id
            WHERE (v.from_id = :id OR v.to_id = :id) AND v.state = 'done'
            ORDER BY v.ended DESC, v.id DESC
            LIMIT 15
            """,
            {"id": tid},
        ).fetchall()

    try:
        donnees = json.loads(tortue["carnet_json"]) if tortue["carnet_json"] else {}
    except ValueError:
        donnees = {}

    en_ligne = tortue["last_seen"] >= t - EN_LIGNE
    presence = "En ligne" if en_ligne else f"Vue {il_y_a(t - tortue['last_seen'])}"
    blocs = [f"""<div class="hero">
<h1>{e(tortue["name"])}</h1>
<p class="sous">{_pastille(en_ligne)}<span>{e(_palier(tortue["tier"]))} · {presence}</span></p>
</div>"""]

    # Statistiques : clés connues dans l'ordre prévu, puis les autres telles quelles.
    stats = donnees.get("stats") or {}
    cles = [k for k in STATS if k in stats] + sorted(k for k in stats if k not in STATS)
    if cles:
        lignes = "".join(
            f'<tr><td>{e(STATS.get(k, k))}</td><td class="n">{_nombre(int(stats[k]))}</td></tr>' for k in cles
        )
        blocs.append(_section("Statistiques", f'<div class="card"><table>{lignes}</table></div>'))
    else:
        blocs.append(_section("Statistiques", _vide("Ce carnet est encore vide.")))

    # Collection de souvenirs.
    collection = donnees.get("collection") or {}
    objets = [(k, int(collection[k])) for k in SOUVENIRS if int(collection.get(k, 0)) > 0]
    if objets:
        figures = "".join(
            f'<figure><img class="px" src="/friend/img/items/{k}.png" alt="{e(SOUVENIRS[k])}" width="64" height="64">'
            f'<figcaption><b>× {_nombre(n)}</b>{e(SOUVENIRS[k])}</figcaption></figure>'
            for k, n in objets
        )
        blocs.append(_section("Collection", f'<div class="items">{figures}</div>'))
    else:
        blocs.append(_section("Collection", _vide("Aucun souvenir pour l'instant.")))

    # Meilleurs amis.
    if amis:
        items = "".join(
            f'<li>{_lien(a["id"], a["name"])}<span class="quand">{_pluriel(a["points"], "point")} d\'amitié</span></li>'
            for a in amis
        )
        blocs.append(_section("Meilleurs amis", f'<div class="card"><ul class="liste">{items}</ul></div>'))
    else:
        blocs.append(_section("Meilleurs amis", _vide("Pas encore d'amis dans la bande.")))

    # Dernières visites.
    if visites:
        items = []
        for v in visites:
            rendue = v["from_id"] == tid
            if rendue:
                titre = f'Visite chez {_lien(v["to_id"], v["to_name"])}'
            else:
                titre = f'Visite de {_lien(v["from_id"], v["from_name"])}'
            details = [ACTIVITES[a] for a in json.loads(v["played"] or "[]") if a in ACTIVITES]
            if v["souvenir"] in SOUVENIRS:
                details.append(("souvenir rapporté : " if rendue else "souvenir offert : ") + SOUVENIRS[v["souvenir"]])
            texte = " · ".join(details)
            detail = f'<span class="detail">{e(texte[:1].upper() + texte[1:])}</span>' if texte else ""
            items.append(f'<li><span>{titre}</span><span class="quand">{date_fr(v["ended"], t)}</span>{detail}</li>')
        blocs.append(_section("Dernières visites", f'<div class="card"><ul class="liste">{"".join(items)}</ul></div>'))
    else:
        blocs.append(_section("Dernières visites", _vide("Aucune visite pour l'instant.")))

    # Journal, plus récent d'abord.
    journal = sorted(donnees.get("journal") or [], key=lambda j: j.get("t", 0), reverse=True)
    if journal:
        items = "".join(
            f'<li><span>{e(str(j.get("text", "")))}</span><span class="quand">{date_fr(int(j.get("t", 0)), t)}</span></li>'
            for j in journal
        )
        blocs.append(_section("Journal", f'<div class="card"><ul class="liste">{items}</ul></div>'))
    else:
        blocs.append(_section("Journal", _vide("Le journal est encore vide.")))

    return document(f"{tortue['name']} · carnet de tortue", "\n".join(blocs))


# --- la bande ---

def bande(t: int) -> HTMLResponse:
    with db.lecture() as c:
        rows = c.execute(
            "SELECT id, name, tier, last_seen FROM turtles WHERE last_seen >= ?", (t - TRENTE_JOURS,)
        ).fetchall()
    rows = sorted(rows, key=lambda r: (r["last_seen"] < t - EN_LIGNE, cle_tri(r["name"])))
    en_ligne = sum(1 for r in rows if r["last_seen"] >= t - EN_LIGNE)

    if rows:
        items = "".join(
            f'<li>{_pastille(r["last_seen"] >= t - EN_LIGNE)}{_lien(r["id"], r["name"])}'
            f'<span class="quand">{e(_palier(r["tier"]))}</span></li>'
            for r in rows[:200]
        )
        liste = f'<div class="card"><ul class="liste">{items}</ul></div>'
    else:
        liste = _vide("Personne pour l'instant.")

    corps = f"""<div class="hero">
<h1>La bande</h1>
<p class="sous">{_pluriel(len(rows), "tortue")}, {en_ligne} en ligne</p>
</div>
<section>
{liste}
<p><a href="/friend/">Retour à Pépin</a></p>
</section>"""
    return document("La bande · Pépin", corps)
