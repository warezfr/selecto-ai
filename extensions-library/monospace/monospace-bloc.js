// ==SelectoExtension==
// @identifier ai.selecto.library.monospace.bloc
// @parent ai.selecto.library.monospace
// @name Code block
// @name:fr Bloc
// @name:en Code block
// @name:es Bloque
// @name:ar كتلة
// @description:fr Met la sélection dans un bloc de code à chasse fixe.
// @description:en Puts the selection in a monospace code block.
// @description:es Pone la selección en un bloque de código monoespaciado.
// @description:ar يضع التحديد في كتلة شيفرة بخط ثابت العرض.
// @icon ```
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var t = selecto.text.replace(/\r\n/g, "\n");
var fence = "```";
while (t.indexOf(fence) >= 0) fence += "`";
var r = fence + "\n" + t + "\n" + fence;
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
