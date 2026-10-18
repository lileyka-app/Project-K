#!/usr/bin/env bash
# Builds the public site: the Astro pages plus the static demo of the app under /demo/.
# No API, no database: the demo answers from fixtures recorded with
# Frontend/projectk-frontend/scripts/record-demo-fixtures.mjs and committed under
# Frontend/projectk-frontend/public/assets/demo/.
#
# Cloudflare Pages: build command `bash scripts/build-site.sh`, output directory `site/dist`;
# the Node version comes from .node-version at the repository root (Angular 22 needs 22.22.3+,
# and a bare NODE_VERSION=22 may resolve to an older patch). Locally: the same command from the
# repository root; result in site/dist.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
frontend="$root/Frontend/projectk-frontend"
site="$root/site"

echo "== badge pictures for the demo"
# The pictures are heavy (46 MB of Illustrator SVG) and public, so they are not in git: they come
# from the same PlastBadgesParser release the API extracts, once per checkout.
badges="$frontend/public/assets/demo/badges_images"
repo="lileyka-app/PlastBadgesParser"

# The API is the obvious way to find the latest asset and the wrong one for a build machine:
# api.github.com allows 60 unauthenticated calls an hour per address, and shared CI addresses
# (Cloudflare Pages among them) burn that quota long before this build starts, answering 403.
# github.com itself has no such quota, and `releases/latest` redirects to the tag, which is all
# that is needed to name the asset. The API stays as a fallback for the day the name changes.
resolve_release_url() {
  local final tag version url api body

  # github.com redirects `releases/latest` to the tag; the tag is all that is needed to name the
  # asset, and no quota is spent.
  final="$(curl --proto =https --tlsv1.2 -fsSLI -o /dev/null -w '%{url_effective}' "https://github.com/$repo/releases/latest" 2>/dev/null || true)"
  tag="${final##*/tag/}"
  if [ -n "$tag" ] && [ "$tag" != "$final" ]; then
    version="${tag#v}"
    url="https://github.com/$repo/releases/download/$tag/plast-badges-data-$version.zip"
    if curl --proto =https --tlsv1.2 -fsSL -o /dev/null -r 0-0 "$url" 2>/dev/null; then
      printf '%s' "$url"
      return 0
    fi
  fi

  # Fallback for the day the asset is named differently. Rate-limited, so it is not the first try.
  api="https://api.github.com/repos/$repo/releases/latest"
  if [ -n "${GITHUB_TOKEN:-}" ]; then
    body="$(curl --proto =https --tlsv1.2 -fsSL -H "Authorization: Bearer $GITHUB_TOKEN" "$api" 2>/dev/null || true)"
  else
    body="$(curl --proto =https --tlsv1.2 -fsSL "$api" 2>/dev/null || true)"
  fi
  printf '%s' "$body" \
    | grep -o '"browser_download_url": *"https[^"]*[.]zip"' | head -1 | grep -o 'https[^"]*' || true
}

if [ ! -d "$badges" ] || [ -z "$(ls -A "$badges" 2>/dev/null)" ]; then
  release_url="$(resolve_release_url || true)"
  [ -n "$release_url" ] || { echo "no zip asset on the latest $repo release" >&2; exit 1; }
  echo "   fetching $release_url"
  tmp="$(mktemp -d)"
  curl --proto =https --tlsv1.2 -fsSL -o "$tmp/badges.zip" "$release_url"
  mkdir -p "$frontend/public/assets/demo"
  unzip -q -o "$tmp/badges.zip" 'badges_images/*' -d "$frontend/public/assets/demo"
  rm -rf "$tmp"
fi
echo "   $(ls "$badges" | wc -l | tr -d ' ') pictures"

echo "== demo app (Angular, configuration demo)"
cd "$frontend"
if [ ! -d node_modules ]; then npm ci --ignore-scripts; fi
npm run build -- --configuration demo

echo "== placing the demo under site/public/demo"
rm -rf "$site/public/demo"
mkdir -p "$site/public/demo"
cp -R "$frontend/dist/projectk-frontend/browser/." "$site/public/demo/"
# index.html asks for env.js; the demo has no runtime config, so an empty file keeps the console clean.
: > "$site/public/demo/env.js"
# The manifest installs the phone app under /m/, which the site does not have: the demo stays web.
rm -f "$site/public/demo/manifest.webmanifest"
sed -i.bak '/rel="manifest"/d' "$site/public/demo/index.html" && rm -f "$site/public/demo/index.html.bak"
# The app's theme and pages reference /assets/... by absolute path (fonts, the hero photo), which
# on the site host is the root, not /demo/. A copy of the same folder at the root keeps them found.
rm -rf "$site/public/assets"
cp -R "$site/public/demo/assets" "$site/public/assets"

echo "== site (Astro)"
cd "$site"
if [ ! -d node_modules ]; then npm ci --ignore-scripts; fi
npm run build

echo "== done: $site/dist"
