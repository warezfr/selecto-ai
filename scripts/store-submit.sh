#!/usr/bin/env bash
# Soumission Microsoft Store de Selecto AI via le Microsoft Store Developer CLI (msstore), en deux temps :
#   1) ./scripts/store-submit.sh prepare <bundle.msixbundle> <notes-fr.txt> <notes-en.txt>
#      Envoie le paquet dans un BROUILLON (--noCommit), met les nouveautés FR/EN, affiche un récapitulatif. Rien n'est soumis.
#   2) ./scripts/store-submit.sh submit
#      Soumet le brouillon pour certification (à lancer seulement après validation du mainteneur), puis suit l'état.
#   ./scripts/store-submit.sh status   : état de la soumission en cours (lecture seule).
# Prérequis : msstore installé et configuré (msstore reconfigure, identifiants gardés sur la machine locale, jamais dans le dépôt).
# Prix : l'application a un prix par marché ; l'API renvoie PriceId « Base » et msstore refuse alors de publier sans
# --priceId. Renseigner STORE_PRICE_ID (palier TierNNNN correspondant au prix de base de Partner Center) et vérifier
# le récapitulatif avant « submit ».
set -euo pipefail

APP_ID="9P6W748ZHCWR"
cmd="${1:-}"

json_of() { python3 -I -c 'import sys;t=sys.stdin.read();i=t.find("{");print(t[i:])'; }

summary() {
  msstore submission get "$APP_ID" | json_of | python3 -I -c '
import sys, json
d = json.load(sys.stdin)
print("Soumission  :", d.get("Id"), "| état :", d.get("Status"), "| publication :", d.get("TargetPublishMode"))
print("Prix        :", d.get("Pricing", {}).get("PriceId"))
for p in d.get("ApplicationPackages", []):
    print("Paquet      :", p.get("FileName"), p.get("Version"), p.get("FileStatus"))
for lang, l in d.get("Listings", {}).items():
    print("Nouveautés", lang, ":", (l.get("BaseListing", {}).get("ReleaseNotes") or "").replace("\n", " / ")[:160])
'
}

case "$cmd" in
  prepare)
    bundle="${2:?chemin du .msixupload (zip du .msixbundle)}"; notes_fr="${3:?fichier des nouveautés FR}"; notes_en="${4:?fichier des nouveautés EN}"
    [ -f "$bundle" ] && [ -f "$notes_fr" ] && [ -f "$notes_en" ] || { echo "Fichier introuvable." >&2; exit 1; }
    state=$(msstore submission status "$APP_ID" 2>&1 || true)
    if grep -q "Submission Status = Pending\|PendingCommit\|CommitStarted" <<<"$state" && [ "${FORCE:-0}" != "1" ]; then
      echo "Une soumission est déjà en cours ($state). msstore publish la remplacerait. Relancer avec FORCE=1 seulement si c'est voulu." >&2
      exit 1
    fi
    echo "SHA-256 : $(shasum -a 256 "$bundle" | cut -d' ' -f1)"
    price_args=()
    [ -n "${STORE_PRICE_ID:-}" ] && price_args=(--priceId "$STORE_PRICE_ID")
    msstore publish "$bundle" --appId "$APP_ID" --noCommit "${price_args[@]}"
    tmp=$(mktemp)
    msstore submission get "$APP_ID" | json_of | NOTES_FR="$notes_fr" NOTES_EN="$notes_en" python3 -I -c '
import sys, json, os
d = json.load(sys.stdin)
fr = open(os.environ["NOTES_FR"], encoding="utf-8").read().strip()
en = open(os.environ["NOTES_EN"], encoding="utf-8").read().strip()
for lang, l in d.get("Listings", {}).items():
    l.setdefault("BaseListing", {})["ReleaseNotes"] = fr if lang.lower().startswith("fr") else en
print(json.dumps(d, ensure_ascii=False))
' > "$tmp"
    msstore submission update "$APP_ID" "$(cat "$tmp")"
    rm -f "$tmp"
    echo "--- Brouillon prêt (non soumis) ---"
    summary
    ;;
  submit)
    summary
    msstore submission publish "$APP_ID"
    msstore submission poll "$APP_ID"
    ;;
  status)
    msstore submission status "$APP_ID"
    summary
    ;;
  *)
    sed -n '2,12p' "$0"; exit 1 ;;
esac
