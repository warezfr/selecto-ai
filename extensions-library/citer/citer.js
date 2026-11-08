// ==SelectoExtension==
// @identifier ai.selecto.library.citer
// @name Quote block
// @name:fr Citer
// @name:en Quote block
// @name:es Citar bloque
// @name:ar اقتباس
// @description:fr Mettez vos lignes en citation, en un clic.
// @description:en Turn lines into a quote in one click.
// @description:es Convierte las líneas en una cita con un clic.
// @description:ar حوّل الأسطر إلى اقتباس بنقرة واحدة.
// @icon >
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var lines = selecto.text.split(/\r?\n/);
var quoted = lines.every(function (l) { return l.charAt(0) === ">"; });
var r = quoted
  ? lines.map(function (l) { return l.replace(/^>( )?/, ""); }).join("\n")
  : lines.map(function (l) { return l === "" ? ">" : "> " + l; }).join("\n");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
