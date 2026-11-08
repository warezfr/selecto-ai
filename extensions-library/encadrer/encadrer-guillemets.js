// ==SelectoExtension==
// @identifier ai.selecto.library.encadrer.guillemets
// @name Guillemets droits
// @name:fr Guillemets droits
// @name:en Straight quotes
// @name:es Comillas rectas
// @name:ar علامات تنصيص عادية
// @description:fr Entoure la sélection de guillemets droits.
// @description:en Wraps the selection in straight double quotes.
// @description:es Rodea la selección con comillas rectas.
// @description:ar يحيط التحديد بعلامات اقتباس مستقيمة.
// @parent ai.selecto.library.encadrer
// @icon "
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = '"' + selecto.text + '"';
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
