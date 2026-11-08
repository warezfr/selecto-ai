// ==SelectoExtension==
// @identifier ai.selecto.library.caviarder
// @name Caviarder
// @name:fr Caviarder
// @name:en Redact
// @name:es Censurar
// @name:ar حجب النص
// @description:fr Masque chaque caractère par un point, en gardant la mise en page.
// @description:en Hide each character with a dot, keeping the layout.
// @description:es Oculta cada carácter con un punto, manteniendo el formato.
// @description:ar أخفِ كل حرف بنقطة مع الحفاظ على التنسيق.
// @icon ##
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
// Découpe en caractères sans casser les emoji (paires de substitution UTF-16).
var chars = selecto.text.match(/[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]/g) || [];
var r = chars.map(function (c) { return /\s/.test(c) ? c : "•"; }).join("");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
