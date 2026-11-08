// ==SelectoExtension==
// @identifier ai.selecto.library.melanger
// @name Mélanger
// @name:fr Mélanger
// @name:en Shuffle lines
// @name:es Mezclar líneas
// @name:ar خلط الأسطر
// @description:fr Mélange les lignes au hasard.
// @description:en Shuffle the lines at random.
// @description:es Mezcla las líneas al azar.
// @description:ar رتّب الأسطر عشوائيًا.
// @icon ?
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var lines = selecto.text.split(/\r?\n/);
for (var i = lines.length - 1; i > 0; i--) {
  var j = Math.floor(Math.random() * (i + 1));
  var tmp = lines[i];
  lines[i] = lines[j];
  lines[j] = tmp;
}
var r = lines.join(eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
