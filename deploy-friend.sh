#!/usr/bin/env bash
# Publie la page de téléchargement + l'exe sur https://pommetortue.tech/friend/
# (dossier statique /var/www/friend sur le VPS, servi par le bloc "location /friend/" du vhost pommetortue.tech).
# À lancer après ./build.ps1.
set -euo pipefail
cd "$(dirname "$0")"

VPS="ubuntu@51.210.14.152"
KEY="$HOME/.ssh/ovh_forge_ed25519"
DIST="$(mktemp -d)"

[ -f publish/Pepin.exe ] || { echo "publish/Pepin.exe absent : lancer build.ps1 d'abord"; exit 1; }
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Pepin.csproj)"
SHA="$(sha256sum publish/Pepin.exe | cut -d' ' -f1)"

mkdir -p "$DIST/img/items"
cp publish/Pepin.exe "$DIST/"
cp site/img/*.png "$DIST/img/"
cp site/img/items/*.png "$DIST/img/items/"
# lu par la mise à jour automatique des tortues déjà installées
printf '{"version":"%s","sha256":"%s","url":"https://pommetortue.tech/friend/Pepin.exe"}\n' "$VERSION" "$SHA" > "$DIST/version.json"
sed -e "s/{{VERSION}}/$VERSION/g" -e "s/{{SHA256}}/$SHA/g" site/index.html > "$DIST/index.html"

tar -C "$DIST" -czf "$DIST.tgz" .
scp -i "$KEY" -q "$DIST.tgz" "$VPS:/tmp/friend.tgz"
ssh -i "$KEY" "$VPS" 'set -e
  sudo -n mkdir -p /var/www/friend
  sudo -n find /var/www/friend -mindepth 1 -delete
  sudo -n tar -C /var/www/friend -xzf /tmp/friend.tgz
  sudo -n chown -R root:root /var/www/friend
  sudo -n chmod -R a+rX /var/www/friend
  rm /tmp/friend.tgz'
rm -rf "$DIST" "$DIST.tgz"
echo "Publié : v$VERSION  sha256 $SHA"
