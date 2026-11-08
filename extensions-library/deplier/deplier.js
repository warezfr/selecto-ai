// ==SelectoExtension==
// @identifier ai.selecto.library.deplier
// @name Déplier
// @name:fr Déplier
// @name:en Unwrap
// @name:es Desplegar
// @name:ar فك الفقرات
// @description:fr Retire les retours à la ligne forcés dans les paragraphes.
// @description:en Remove forced line breaks inside paragraphs.
// @description:es Quita los saltos de línea forzados dentro de los párrafos.
// @description:ar أزل كسر الأسطر داخل الفقرات.
// @icon ¶
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var paragraphs = selecto.text.split(/\n\s*\n/);
var out = [];
for (var i = 0; i < paragraphs.length; i++) {
  out.push(paragraphs[i].replace(/\s+/g, " ").trim());
}
var r = out.join(eol + eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
