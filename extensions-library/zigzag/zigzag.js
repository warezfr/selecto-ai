// ==SelectoExtension==
// @identifier ai.selecto.library.zigzag
// @name Zigzag
// @name:fr Zigzag
// @name:en Zigzag
// @name:es Zigzag
// @name:ar متعرّج
// @description:fr Alterne majuscules et minuscules, lettre par lettre.
// @description:en Alternate upper and lower case, letter by letter.
// @description:es Alterna mayúsculas y minúsculas, letra a letra.
// @description:ar بدّل بين الأحرف الكبيرة والصغيرة حرفًا بعد حرف.
// @icon Zz
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var out = "";
var n = 0;
// Découpe en caractères sans casser les emoji (paires de substitution UTF-16).
var chars = selecto.text.match(/[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]/g) || [];
for (var i = 0; i < chars.length; i++) {
  var c = chars[i];
  var up = c.toUpperCase();
  var low = c.toLowerCase();
  if (up === low) { out += c; continue; }
  out += (n % 2 === 0) ? up : low;
  n++;
}
var r = out;
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
