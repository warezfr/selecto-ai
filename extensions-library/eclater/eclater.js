// ==SelectoExtension==
// @identifier ai.selecto.library.eclater
// @name Éclater
// @name:fr Éclater
// @name:en Split list
// @name:es Separar lista
// @name:ar تفكيك القائمة
// @description:fr Sépare une liste écrite en une ligne par élément.
// @description:en Split a written list into one item per line.
// @description:es Separa una lista escrita en un elemento por línea.
// @description:ar حوّل قائمة مكتوبة إلى عنصر في كل سطر.
// @icon a;
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var items = [];
var parts = selecto.text.split(/[,;\r\n]+/);
for (var i = 0; i < parts.length; i++) {
  var t = parts[i].trim();
  if (t.length > 0) items.push(t);
}
var r = items.join(eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
