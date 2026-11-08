// ==SelectoExtension==
// @identifier ai.selecto.library.monospace.ligne
// @parent ai.selecto.library.monospace
// @name Inline code
// @name:fr En ligne
// @name:en Inline code
// @name:es En línea
// @name:ar في السطر
// @description:fr Met la sélection en chasse fixe, sur une seule ligne.
// @description:en Sets the selection in monospace, on a single line.
// @description:es Pone la selección en monoespaciado, en una sola línea.
// @description:ar يضع التحديد بخط ثابت العرض في سطر واحد.
// @icon `
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var t = selecto.text.replace(/\r?\n/g, " ");
var r = t.indexOf("`") < 0 ? "`" + t + "`" : "`` " + t + " ``";
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
