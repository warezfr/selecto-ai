// ==SelectoExtension==
// @identifier ai.selecto.library.nom-de-code.pascal
// @name PascalCase
// @name:fr PascalCase
// @name:en PascalCase
// @name:es PascalCase
// @name:ar PascalCase
// @description:fr Convertit en PascalCase : chaque mot commence par une majuscule.
// @description:en Converts to PascalCase: each word starts with a capital.
// @description:es Convierte a PascalCase: cada palabra empieza con mayúscula.
// @description:ar يحوّل إلى PascalCase: تبدأ كل كلمة بحرف كبير.
// @parent ai.selecto.library.nom-de-code
// @icon AB
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var s = selecto.text.normalize("NFD").replace(/[̀-ͯ]/g, "");
s = s.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
var words = s.split(/[\s_\-.]+/)
  .map(function (w) { return w.replace(/[^A-Za-z0-9؀-ۿ]/g, "").toLowerCase(); })
  .filter(function (w) { return w.length > 0; });
function cap(w) { return w.charAt(0).toUpperCase() + w.slice(1); }
var r = words.map(cap).join("");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
