// ==SelectoExtension==
// @identifier ai.selecto.library.encadrer.parentheses
// @name Parenthèses
// @name:fr Parenthèses
// @name:en Parentheses
// @name:es Paréntesis
// @name:ar أقواس
// @description:fr Entoure la sélection de parenthèses ( ).
// @description:en Wraps the selection in parentheses ( ).
// @description:es Rodea la selección con paréntesis ( ).
// @description:ar يحيط التحديد بأقواس ( ).
// @parent ai.selecto.library.encadrer
// @icon ()
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = "(" + selecto.text + ")";
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
