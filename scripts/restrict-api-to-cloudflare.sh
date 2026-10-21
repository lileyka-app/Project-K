#!/usr/bin/env bash
# SEC-4.1, the Azure half: let only the Cloudflare edge reach the production API.
#
# Why it matters. The API trusts `CF-Connecting-IP` (`Security:ClientIp:Header` in
# appsettings.Production.json) to know who is calling. While the App Service answers everyone, the
# default `*.azurewebsites.net` hostname is an open door: a request sent straight there can carry
# any `CF-Connecting-IP` it likes, and with it walk past the login rate limit, the geo block and
# the address written into the audit log.
#
#   bash scripts/restrict-api-to-cloudflare.sh          # close the perimeter
#   bash scripts/restrict-api-to-cloudflare.sh undo     # open it again
#
# Cloudflare publishes its ranges at cloudflare.com/ips-v4 and /ips-v6 and changes them rarely;
# rerun this after such a change. Staging is deliberately not touched: its frontend calls the App
# Service hostname directly, with no Cloudflare in front, so the same rules would cut it off.
set -euo pipefail

subscription="projectk-prod-sub"
group="rg-projectk-prod-paid"
app="api-projectk-prod-new"
host="api.lileyka.app"

default_host="$(az webapp show --subscription "$subscription" -g "$group" -n "$app" --query defaultHostName -o tsv)"

if [ "${1:-}" = "undo" ]; then
  echo "== removing the Cloudflare rules"
  for rule in $(az webapp config access-restriction show --subscription "$subscription" -g "$group" -n "$app" \
      --query "ipSecurityRestrictions[?starts_with(name, 'cloudflare-')].name" -o tsv); do
    az webapp config access-restriction remove --subscription "$subscription" -g "$group" -n "$app" \
      --rule-name "$rule" -o none
    echo "   removed $rule"
  done
  echo "== done; the app answers everyone again"
  exit 0
fi

echo "== checks before touching anything"

# Deployment goes through the SCM site. If it inherited these rules, the next release would have
# nowhere to land, so refuse rather than discover that during a release.
scm_uses_main="$(az webapp config access-restriction show --subscription "$subscription" -g "$group" -n "$app" \
  --query scmIpSecurityRestrictionsUseMain -o tsv)"
if [ "$scm_uses_main" = "true" ]; then
  echo "   SCM inherits the main rules: closing the perimeter would lock deployments out" >&2
  exit 1
fi
echo "   deployments unaffected (SCM keeps its own rules)"

# The whole plan rests on Cloudflare actually being in front of the custom domain. Its answer says
# so itself: only the edge sets CF-RAY.
# A GET with the headers dumped, not a HEAD: /health answers 405 to HEAD, and with `pipefail` a
# failing curl would read as «Cloudflare is gone» and stop a perfectly good run.
if ! curl -sS -o /dev/null -D - "https://$host/health" | grep -qi '^cf-ray:'; then
  echo "   https://$host is not answered by Cloudflare — closing the perimeter would cut the app off" >&2
  exit 1
fi
echo "   $host is served through Cloudflare"

ranges="$(curl -fsS https://www.cloudflare.com/ips-v4; echo; curl -fsS https://www.cloudflare.com/ips-v6)"
count="$(printf '%s\n' "$ranges" | grep -c '/')"
if [ "$count" -lt 15 ]; then
  echo "   only $count ranges came back from cloudflare.com, expected about 22 — stopping" >&2
  exit 1
fi
echo "   $count Cloudflare ranges"

echo "== allowing them"
# The first Allow rule turns the implicit default into Deny, so between the first and the last rule
# a request from a range not yet added is refused. It lasts under a minute; run it off-peak.
priority=100
for cidr in $(printf '%s\n' "$ranges" | grep '/'); do
  case "$cidr" in
    *:*) name="cloudflare-v6-$priority" ;;
    *)   name="cloudflare-v4-$priority" ;;
  esac
  az webapp config access-restriction add --subscription "$subscription" -g "$group" -n "$app" \
    --rule-name "$name" --action Allow --ip-address "$cidr" --priority "$priority" \
    --description "Cloudflare edge" -o none
  priority=$((priority + 1))
done
echo "   $count rules in place"

echo "== verifying"
sleep 10
through_edge="$(curl -s -o /dev/null -w '%{http_code}' "https://$host/health")"
direct="$(curl -s -o /dev/null -w '%{http_code}' "https://$default_host/health")"
echo "   through Cloudflare: $through_edge (want 200)"
echo "   straight at the App Service: $direct (want 403)"

if [ "$through_edge" != "200" ]; then
  echo "   the app stopped answering through Cloudflare; undo with: bash $0 undo" >&2
  exit 1
fi
if [ "$direct" != "403" ]; then
  echo "   the App Service still answers directly; check the rules in the portal" >&2
  exit 1
fi
echo "== done: only the Cloudflare edge reaches the API"
