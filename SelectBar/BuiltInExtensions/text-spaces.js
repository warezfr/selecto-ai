// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.spaces
// @name Remove double spaces
// @name:fr Supprimer les doubles espaces
// @name:en Remove double spaces
// @name:es Quitar espacios dobles
// @name:ar إزالة المسافات المزدوجة
// @description:fr Remplace les espaces multiples par une seule espace.
// @description:en Replaces repeated spaces with a single space.
// @description:es Sustituye los espacios múltiples por uno solo.
// @description:ar يستبدل المسافات المتكررة بمسافة واحدة.
// @parent ai.selecto.builtin.text
// @icon a b
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.replace(/[^\S\r\n]{2,}/g, " ");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
