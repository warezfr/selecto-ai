// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.sort
// @name Sort lines
// @name:fr Trier les lignes
// @name:en Sort lines
// @name:es Ordenar las líneas
// @name:ar فرز الأسطر
// @description:fr Trie les lignes sélectionnées par ordre alphabétique.
// @description:en Sorts the selected lines alphabetically.
// @description:es Ordena las líneas seleccionadas alfabéticamente.
// @description:ar يرتّب الأسطر المحددة أبجديًا.
// @parent ai.selecto.builtin.text
// @icon A-Z
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.split(/\r?\n/).sort(function (a, b) { return a.localeCompare(b); }).join("\n");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
