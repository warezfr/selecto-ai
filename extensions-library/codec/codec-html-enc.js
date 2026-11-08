// ==SelectoExtension==
// @identifier ai.selecto.library.codec.html-enc
// @parent ai.selecto.library.codec
// @name HTML: encode
// @name:fr HTML : encoder
// @name:en HTML: encode
// @name:es HTML: codificar
// @name:ar HTML: ترميز
// @description:fr Encode les caractères spéciaux en entités HTML.
// @description:en Encodes special characters as HTML entities.
// @description:es Codifica los caracteres especiales como entidades HTML.
// @description:ar يرمّز الأحرف الخاصة ككيانات HTML.
// @icon &
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = selecto.text
  .replace(/&/g, "&amp;")
  .replace(/</g, "&lt;")
  .replace(/>/g, "&gt;")
  .replace(/"/g, "&quot;")
  .replace(/'/g, "&#39;");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
