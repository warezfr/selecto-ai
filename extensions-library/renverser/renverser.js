// ==SelectoExtension==
// @identifier ai.selecto.library.renverser
// @name Renverser
// @name:fr Renverser
// @name:en Flip lines
// @name:es Invertir líneas
// @name:ar عكس الأسطر
// @description:fr Inverse l'ordre des lignes.
// @description:en Reverse the order of lines.
// @description:es Invierte el orden de las líneas.
// @description:ar اعكس ترتيب الأسطر.
// @icon <>
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var lines = selecto.text.split(/\r?\n/);
var r = lines.reverse().join(eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
