// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.join
// @name Join lines
// @name:fr Joindre les lignes
// @name:en Join lines
// @name:es Unir las líneas
// @name:ar دمج الأسطر
// @description:fr Réunit les lignes sélectionnées sur une seule ligne.
// @description:en Joins the selected lines into a single line.
// @description:es Une las líneas seleccionadas en una sola línea.
// @description:ar يدمج الأسطر المحددة في سطر واحد.
// @parent ai.selecto.builtin.text
// @icon a/b
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.split(/\r?\n/).map(function (s) { return s.trim(); }).filter(function (s) { return s.length > 0; }).join(" ");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
