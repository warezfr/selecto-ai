// ==SelectoExtension==
// @identifier ai.selecto.library.nom-de-code.camel
// @name camelCase
// @name:fr camelCase
// @name:en camelCase
// @name:es camelCase
// @name:ar camelCase
// @description:fr Convertit en camelCase : motsCollésAvecMajuscules.
// @description:en Converts to camelCase: wordsJoinedWithCapitals.
// @description:es Convierte a camelCase: palabrasUnidasConMayúsculas.
// @description:ar يحوّل إلى camelCase: كلمات متصلة بأحرف كبيرة.
// @parent ai.selecto.library.nom-de-code
// @icon aB
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var s = selecto.text.normalize("NFD").replace(/[̀-ͯ]/g, "");
s = s.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
var words = s.split(/[\s_\-.]+/)
  .map(function (w) { return w.replace(/[^A-Za-z0-9؀-ۿ]/g, "").toLowerCase(); })
  .filter(function (w) { return w.length > 0; });
function cap(w) { return w.charAt(0).toUpperCase() + w.slice(1); }
var r = words.map(function (w, i) { return i === 0 ? w : cap(w); }).join("");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
