// ==SelectoExtension==
// @identifier ai.selecto.builtin.count
// @name Count
// @name:fr Compter
// @name:en Count
// @name:es Contar
// @name:ar عدّ
// @description:fr Compte les mots et les caractères de la sélection.
// @description:en Counts the words and characters in the selection.
// @description:es Cuenta las palabras y los caracteres de la selección.
// @description:ar تعدّ كلمات وأحرف التحديد.
// @icon 123
// @requirements text
// @enabled-by-default true
// @inline true
// ==/SelectoExtension==
var t = selecto.text;
var words = t.trim().split(/\s+/).filter(function (w) { return w.length > 0; }).length;
var chars = t.length;
var names = {
  fr: { w1: "mot", wn: "mots", c1: "caractère", cn: "caractères", min: "min" },
  en: { w1: "word", wn: "words", c1: "character", cn: "characters", min: "min" },
  es: { w1: "palabra", wn: "palabras", c1: "carácter", cn: "caracteres", min: "min" },
  ar: { w1: "كلمة", wn: "كلمات", c1: "حرف", cn: "أحرف", min: "د" }
};
var l = names[selecto.locale] || names.en;
var time = words / 200 < 1 ? "< 1 " + l.min : Math.ceil(words / 200) + " " + l.min;
var s = words + " " + (words === 1 ? l.w1 : l.wn) + " · " + chars + " " + (chars === 1 ? l.c1 : l.cn) + " · " + time;
if (selecto.inline) selecto.showText(s); else selecto.showResult(s);
