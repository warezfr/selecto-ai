// ==SelectoExtension==
// @identifier ai.selecto.library.encadrer.chevrons
// @name Guillemets français
// @name:fr Guillemets français
// @name:en French quotes
// @name:es Comillas angulares
// @name:ar علامات تنصيص مزدوجة
// @description:fr Entoure la sélection de guillemets français « ».
// @description:en Wraps the selection in French guillemets « ».
// @description:es Rodea la selección con comillas angulares « ».
// @description:ar يحيط التحديد بعلامات اقتباس فرنسية « ».
// @parent ai.selecto.library.encadrer
// @icon «»
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = "« " + selecto.text + " »";
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
