// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.lower
// @name lowercase
// @name:fr minuscules
// @name:en lowercase
// @name:es minúsculas
// @name:ar أحرف صغيرة
// @description:fr Passe le texte sélectionné en minuscules.
// @description:en Converts the selected text to lower case.
// @description:es Convierte el texto seleccionado en minúsculas.
// @description:ar يحوّل النص المحدد إلى أحرف صغيرة.
// @parent ai.selecto.builtin.text
// @icon aa
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.toLowerCase();
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
