// ==SelectoExtension==
// @identifier ai.selecto.library.json-net
// @name Clean JSON
// @name:fr JSON net
// @name:en Clean JSON
// @name:es JSON limpio
// @name:ar JSON منسّق
// @description:fr Rendez un JSON lisible en un clic.
// @description:en Turn messy JSON into readable JSON.
// @description:es Convierte un JSON desordenado en uno legible.
// @description:ar حوّل JSON غير المنظم إلى صيغة مقروءة.
// @icon {}
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var out = null;
try { out = JSON.stringify(JSON.parse(selecto.text), null, 2); } catch (e) { out = null; }
if (out === null) {
  selecto.showText({ fr: "JSON invalide", en: "Invalid JSON", es: "JSON no válido", ar: "JSON غير صالح" }[selecto.locale] || "Invalid JSON");
} else {
  if (selecto.editable) selecto.pasteText(out); else selecto.copyText(out);
  var done = selecto.editable
    ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
    : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
  selecto.showText(done[selecto.locale] || done.en);
}
