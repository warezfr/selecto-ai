// ==SelectoExtension==
// @identifier ai.selecto.library.sans-doublons
// @name Sans doublons
// @name:fr Sans doublons
// @name:en Unique lines
// @name:es Sin duplicados
// @name:ar بلا تكرار
// @description:fr Retire les lignes en double, en gardant l'ordre.
// @description:en Remove duplicate lines, keeping the order.
// @description:es Quita las líneas duplicadas y conserva el orden.
// @description:ar احذف الأسطر المكررة مع الحفاظ على الترتيب.
// @icon ==
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var lines = selecto.text.split(/\r?\n/);
var seen = new Set();
var kept = [];
for (var i = 0; i < lines.length; i++) {
  if (!seen.has(lines[i])) {
    seen.add(lines[i]);
    kept.push(lines[i]);
  }
}
var r = kept.join(eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
