// ==SelectoExtension==
// @identifier ai.selecto.library.chrono-lecture
// @name Reading time
// @name:fr Chrono lecture
// @name:en Reading time
// @name:es Tiempo de lectura
// @name:ar مدة القراءة
// @description:fr Combien de temps pour lire ce texte ?
// @description:en How long will this take to read?
// @description:es ¿Cuánto tarda en leerse este texto?
// @description:ar كم يستغرق قراءة هذا النص؟
// @icon min
// @requirements text
// @enabled-by-default true
// @inline true
// ==/SelectoExtension==
var t = selecto.text.trim();
var words = t === "" ? 0 : t.split(/\s+/).length;
var names = {
  fr: { w1: "mot", wn: "mots", read: "min de lecture", less: "< 1 min", sep: " " },
  en: { w1: "word", wn: "words", read: "min read", less: "< 1 min", sep: "," },
  es: { w1: "palabra", wn: "palabras", read: "min de lectura", less: "< 1 min", sep: "." },
  ar: { w1: "كلمة", wn: "كلمات", read: "د قراءة", less: "< 1 د", sep: "," }
};
var l = names[selecto.locale] || names.en;
var digits = String(words);
var grouped = "";
while (digits.length > 3) {
  grouped = l.sep + digits.slice(-3) + grouped;
  digits = digits.slice(0, -3);
}
grouped = digits + grouped;
var time = words / 230 < 1 ? l.less : Math.ceil(words / 230) + " " + l.read;
var s = grouped + " " + (words === 1 ? l.w1 : l.wn) + " · " + time;
if (selecto.inline) selecto.showText(s); else selecto.showResult(s);
