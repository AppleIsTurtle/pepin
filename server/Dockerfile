FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py db.py pages.py catalogue.py ./

# Utilisateur non-root ; uid 1000 = propriétaire habituel du dossier ./data monté depuis l'hôte.
RUN useradd --uid 1000 --no-create-home --shell /usr/sbin/nologin bande \
    && mkdir /data && chown bande /data
USER bande

EXPOSE 8000
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8000"]
