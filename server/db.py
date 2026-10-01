"""Accès SQLite : schéma, connexions et transactions."""

import os
import sqlite3
from contextlib import contextmanager

# Lu à chaque connexion : les tests peuvent le remplacer.
DB_PATH = os.environ.get("DB_PATH", "bande.db")

SCHEMA = """
CREATE TABLE IF NOT EXISTS turtles (
    id             TEXT PRIMARY KEY,
    token_hash     TEXT NOT NULL UNIQUE,
    name           TEXT NOT NULL,
    name_key       TEXT NOT NULL UNIQUE,          -- nom en casefold, pour l'unicité insensible à la casse
    created        INTEGER NOT NULL,
    last_seen      INTEGER NOT NULL,
    status         TEXT NOT NULL DEFAULT 'home'
                   CHECK (status IN ('home', 'away', 'sleeping', 'visiting')),
    tier           INTEGER NOT NULL DEFAULT 0 CHECK (tier BETWEEN 0 AND 4),
    version        TEXT NOT NULL DEFAULT '',
    accept_messages INTEGER NOT NULL DEFAULT 1,
    carnet_json    TEXT,
    carnet_updated INTEGER
);
CREATE INDEX IF NOT EXISTS turtles_last_seen ON turtles(last_seen);

CREATE TABLE IF NOT EXISTS visits (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    from_id  TEXT NOT NULL REFERENCES turtles(id),
    to_id    TEXT NOT NULL REFERENCES turtles(id),
    message  TEXT,
    state    TEXT NOT NULL
             CHECK (state IN ('pending', 'active', 'done', 'aborted', 'no_one_home')),
    created  INTEGER NOT NULL,
    accepted INTEGER,
    duration INTEGER NOT NULL,
    souvenir TEXT,
    played   TEXT NOT NULL DEFAULT '[]',
    ended    INTEGER
);
CREATE INDEX IF NOT EXISTS visits_state ON visits(state);
CREATE INDEX IF NOT EXISTS visits_from ON visits(from_id, created);
CREATE INDEX IF NOT EXISTS visits_to ON visits(to_id, created);

CREATE TABLE IF NOT EXISTS friendships (
    a      TEXT NOT NULL,
    b      TEXT NOT NULL,
    points INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (a, b),
    CHECK (a < b)
);
CREATE INDEX IF NOT EXISTS friendships_b ON friendships(b);

CREATE TABLE IF NOT EXISTS blocks (
    blocker TEXT NOT NULL,
    blocked TEXT NOT NULL,
    PRIMARY KEY (blocker, blocked)
);
CREATE INDEX IF NOT EXISTS blocks_blocked ON blocks(blocked);

CREATE TABLE IF NOT EXISTS events (
    eid       INTEGER PRIMARY KEY AUTOINCREMENT,
    turtle_id TEXT NOT NULL,
    payload   TEXT NOT NULL,
    created   INTEGER NOT NULL,
    acked     INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS events_pending ON events(turtle_id, acked);
"""


def connect() -> sqlite3.Connection:
    # isolation_level=None : les transactions sont ouvertes explicitement (BEGIN IMMEDIATE).
    c = sqlite3.connect(DB_PATH, timeout=10, isolation_level=None)
    c.row_factory = sqlite3.Row
    c.execute("PRAGMA busy_timeout = 10000")
    return c


def init() -> None:
    c = connect()
    try:
        c.execute("PRAGMA journal_mode = WAL")
        c.executescript(SCHEMA)
        migrer(c)
    finally:
        c.close()


def migrer(c: sqlite3.Connection) -> None:
    """Ajouts de colonnes des versions suivantes (sans effet si déjà faits)."""
    colonnes = {r[1] for r in c.execute("PRAGMA table_info(turtles)")}
    if "species" not in colonnes:
        # v3 : l'espèce du compagnon ; tous les comptes existants sont des tortues
        c.execute("ALTER TABLE turtles ADD COLUMN species TEXT NOT NULL DEFAULT 'tortue'")


@contextmanager
def tx():
    """Transaction d'écriture : verrou pris dès le début, annulée si une exception passe."""
    c = connect()
    try:
        c.execute("BEGIN IMMEDIATE")
        try:
            yield c
        except BaseException:
            c.execute("ROLLBACK")
            raise
        c.execute("COMMIT")
    finally:
        c.close()


@contextmanager
def lecture():
    """Connexion en lecture seule (pages publiques)."""
    c = connect()
    try:
        yield c
    finally:
        c.close()
