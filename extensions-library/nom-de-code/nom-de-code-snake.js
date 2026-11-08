// ==SelectoExtension==
// @identifier ai.selecto.library.nom-de-code.snake
// @name snake_case
// @name:fr snake_case
// @name:en snake_case
// @name:es snake_case
// @name:ar snake_case
// @description:fr Convertit en snake_case : mots-separés-par-des-tirets-bas.
// @description:en Converts to snake_case: words-joined-by-underscores.
// @description:es Convierte a snake_case: palabras-unidas-por-guiones-bajos.
// @description:ar يحوّل إلى snake_case: كلمات تفصل بينها شرطات سفلية.
// @parent ai.selecto.library.nom-de-code
// @icon a_b
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var s = selecto.text.normalize("NFD").replace(/[̀-ͯ]/g, "");
s = s.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
var words = s.split(/[\s_\-.]+/)
  .map(function (w) { return w.replace(/[^A-Za-z0-9؀-ۿ]/g, "").toLowerCase(); })
  .filter(function (w) { return w.length > 0; });
function cap(w) { return w.charAt(0).toUpperCase() + w.slice(1); }
var r = words.join("_");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
