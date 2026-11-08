// ==SelectoExtension==
// @identifier ai.selecto.library.nom-de-code.kebab
// @name kebab-case
// @name:fr kebab-case
// @name:en kebab-case
// @name:es kebab-case
// @name:ar kebab-case
// @description:fr Convertit en kebab-case : mots-separés-par-des-tirets.
// @description:en Converts to kebab-case: words-joined-by-hyphens.
// @description:es Convierte a kebab-case: palabras-unidas-por-guiones.
// @description:ar يحوّل إلى kebab-case: كلمات تفصل بينها شرطات.
// @parent ai.selecto.library.nom-de-code
// @icon a-b
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var s = selecto.text.normalize("NFD").replace(/[̀-ͯ]/g, "");
s = s.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
var words = s.split(/[\s_\-.]+/)
  .map(function (w) { return w.replace(/[^A-Za-z0-9؀-ۿ]/g, "").toLowerCase(); })
  .filter(function (w) { return w.length > 0; });
function cap(w) { return w.charAt(0).toUpperCase() + w.slice(1); }
var r = words.join("-");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
