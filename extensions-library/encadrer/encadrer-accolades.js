// ==SelectoExtension==
// @identifier ai.selecto.library.encadrer.accolades
// @name Accolades
// @name:fr Accolades
// @name:en Curly braces
// @name:es Llaves
// @name:ar أقواس معقوفة
// @description:fr Entoure la sélection d'accolades { }.
// @description:en Wraps the selection in curly braces { }.
// @description:es Rodea la selección con llaves { }.
// @description:ar يحيط التحديد بأقواس معقوفة { }.
// @parent ai.selecto.library.encadrer
// @icon {}
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = "{" + selecto.text + "}";
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
