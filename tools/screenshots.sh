#!/usr/bin/env bash
#
# The two pictures in the README, taken from the running portal.
#
# Not by hand with a snipping tool. A screenshot taken by hand is a screenshot
# of whatever the browser happened to be showing that afternoon, at whatever
# zoom, with whatever else was open behind it — and nobody can tell later
# whether the page really said that. This signs in the way a browser does,
# saves what the portal actually served, and renders it.
#
# It needs a Chromium-based browser, which is why it is a tool and not a test:
# CI has no business installing one to check a picture. What CI does check is
# that every picture the README shows is in the repository.
#
#   tools/screenshots.sh [path-to-a-chromium-browser]
#
set -euo pipefail

here=$(cd "$(dirname "$0")/.." && pwd)
browser=${1:-}
port=5199
base="http://127.0.0.1:$port"

if [ -z "$browser" ]; then
  for candidate in \
    "/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe" \
    "/c/Program Files/Microsoft/Edge/Application/msedge.exe" \
    "$(command -v chromium || true)" \
    "$(command -v google-chrome || true)"
  do
    if [ -n "$candidate" ] && [ -x "$candidate" ]; then browser=$candidate; break; fi
  done
fi

if [ -z "$browser" ]; then
  echo "No Chromium-based browser found. Pass one as the first argument."
  exit 1
fi

work=$(mktemp -d)
trap 'rm -rf "$work"; kill %1 2>/dev/null || true' EXIT

( cd "$here" && ASPNETCORE_URLS=$base dotnet run --project src/Portal.Web -c Release >/dev/null 2>&1 ) &

for _ in $(seq 1 150); do
  curl -so /dev/null "$base/SignIn" && break
  sleep 0.2
done

# Signed in the way a browser signs in, antiforgery token and all. Anything
# that skipped that would be a screenshot of a page the portal does not serve.
token=$(curl -s -c "$work/jar" "$base/SignIn" \
  | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' \
  | sed 's/.*value="//; s/"$//')

curl -s -b "$work/jar" -c "$work/jar" -o /dev/null -X POST "$base/SignIn" \
  --data-urlencode "Patient=giulia" \
  --data-urlencode "password=ward" \
  --data-urlencode "__RequestVerificationToken=$token"

curl -s -b "$work/jar" "$base/" > "$work/documents.html"
curl -s -b "$work/jar" "$base/Code?id=ACC-100374" > "$work/code.html"

# The stylesheet goes inline, because the pages are rendered from a file and a
# file cannot fetch /portal.css. The bytes are the ones the portal served.
python - "$work" "$here" <<'PY'
import pathlib, sys

work, here = (pathlib.Path(one) for one in sys.argv[1:3])
css = (here / "src/Portal.Web/wwwroot/portal.css").read_text(encoding="utf-8")

for name in ("documents", "code"):
    page = (work / f"{name}.html").read_text(encoding="utf-8")
    at = page.find('<link rel="stylesheet"')
    end = page.find(">", at) + 1
    (work / f"{name}-inline.html").write_text(
        page[:at] + "<style>\n" + css + "\n</style>" + page[end:], encoding="utf-8")
PY

for name in documents code; do
  "$browser" --headless=new --disable-gpu --hide-scrollbars \
    --force-device-scale-factor=2 --window-size=920,620 \
    --screenshot="$(cygpath -w "$here/docs/$name.png" 2>/dev/null || echo "$here/docs/$name.png")" \
    "file:///$(cygpath -m "$work/$name-inline.html" 2>/dev/null || echo "$work/$name-inline.html")" \
    >/dev/null 2>&1
  echo "docs/$name.png"
done
