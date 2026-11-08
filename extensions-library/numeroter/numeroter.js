// ==SelectoExtension==
// @identifier ai.selecto.library.numeroter
// @name Numéroter
// @name:fr Numéroter
// @name:en Number lines
// @name:es Numerar líneas
// @name:ar ترقيم الأسطر
// @description:fr Numérote les lignes non vides.
// @description:en Number the non-empty lines.
// @description:es Numera las líneas no vacías.
// @description:ar رقّم الأسطر غير الفارغة.
// @icon 1.
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var eol = selecto.text.indexOf("\r\n") >= 0 ? "\r\n" : "\n";
var lines = selecto.text.split(/\r?\n/);
var n = 0;
var out = [];
for (var i = 0; i < lines.length; i++) {
  if (lines[i].trim() === "") { out.push(lines[i]); continue; }
  n++;
  out.push(n + ". " + lines[i]);
}
var r = out.join(eol);
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
