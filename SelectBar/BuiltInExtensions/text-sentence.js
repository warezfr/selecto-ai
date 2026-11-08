// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.sentence
// @name Sentence case
// @name:fr Première lettre
// @name:en Sentence case
// @name:es Primera letra
// @name:ar أول حرف
// @description:fr Met en majuscule la première lettre de chaque phrase.
// @description:en Capitalizes the first letter of each sentence.
// @description:es Pone en mayúscula la primera letra de cada frase.
// @description:ar يجعل أول حرف في كل جملة كبيرًا.
// @parent ai.selecto.builtin.text
// @icon A.
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.replace(/(^|[.!?]\s+)(\S)/g, function (m, p, c) { return p + c.toUpperCase(); });
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
