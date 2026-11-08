// ==SelectoExtension==
// @identifier ai.selecto.library.rassembler
// @name Rassembler
// @name:fr Rassembler
// @name:en Gather list
// @name:es Unir en lista
// @name:ar تجميع القائمة
// @description:fr Réunit les lignes sur une seule ligne, séparées par des virgules.
// @description:en Join the lines into one comma-separated line.
// @description:es Une las líneas en una sola línea separada por comas.
// @description:ar اجمع الأسطر في سطر واحد مفصول بفواصل.
// @icon a,b
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var lines = selecto.text.split(/\r?\n/);
var items = [];
for (var i = 0; i < lines.length; i++) {
  var t = lines[i].trim();
  if (t.length > 0) items.push(t);
}
var r = items.join(", ");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
