// ==SelectoExtension==
// @identifier ai.selecto.library.puces
// @name Bullets
// @name:fr Puces
// @name:en Bullets
// @name:es Viñetas
// @name:ar نقاط
// @description:fr Transformez vos lignes en liste à puces.
// @description:en Turn lines into a bulleted list.
// @description:es Convierte las líneas en una lista con viñetas.
// @description:ar حوّل الأسطر إلى قائمة نقطية.
// @icon •
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var lines = selecto.text.split(/\r?\n/);
var filled = lines.filter(function (l) { return l.trim() !== ""; });
var bulleted = filled.length > 0 && filled.every(function (l) { return l.indexOf("- ") === 0; });
var r = lines.map(function (l) {
  if (l.trim() === "") return l;
  return bulleted ? l.slice(2) : "- " + l;
}).join("\n");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
