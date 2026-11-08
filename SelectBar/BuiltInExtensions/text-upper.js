// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.upper
// @name UPPERCASE
// @name:fr MAJUSCULES
// @name:en UPPERCASE
// @name:es MAYÚSCULAS
// @name:ar أحرف كبيرة
// @description:fr Passe le texte sélectionné en majuscules.
// @description:en Converts the selected text to upper case.
// @description:es Convierte el texto seleccionado en mayúsculas.
// @description:ar يحوّل النص المحدد إلى أحرف كبيرة.
// @parent ai.selecto.builtin.text
// @icon AA
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.toUpperCase();
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
