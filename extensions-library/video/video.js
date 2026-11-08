// ==SelectoExtension==
// @identifier ai.selecto.library.video
// @name Video search
// @name:fr Vidéo
// @name:en Video search
// @name:es Buscar vídeo
// @name:ar بحث عن فيديو
// @description:fr Cherchez une vidéo sur YouTube.
// @description:en Search YouTube for the selection.
// @description:es Busca la selección en YouTube.
// @description:ar ابحث عن التحديد في YouTube.
// @icon ▶
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var H = "0123456789ABCDEF";
var U = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~";
function utf8(s) {
  var out = [];
  for (var i = 0; i < s.length; i++) {
    var c = s.charCodeAt(i);
    if (c >= 0xD800 && c <= 0xDBFF && i + 1 < s.length && s.charCodeAt(i + 1) >= 0xDC00 && s.charCodeAt(i + 1) <= 0xDFFF) {
      c = 0x10000 + ((c - 0xD800) << 10) + (s.charCodeAt(i + 1) - 0xDC00);
      i++;
    } else if (c >= 0xD800 && c <= 0xDFFF) {
      c = 0xFFFD;
    }
    if (c < 0x80) out.push(c);
    else if (c < 0x800) out.push(0xC0 | (c >> 6), 0x80 | (c & 63));
    else if (c < 0x10000) out.push(0xE0 | (c >> 12), 0x80 | ((c >> 6) & 63), 0x80 | (c & 63));
    else out.push(0xF0 | (c >> 18), 0x80 | ((c >> 12) & 63), 0x80 | ((c >> 6) & 63), 0x80 | (c & 63));
  }
  return out;
}
function pct(s) {
  var b = utf8(s);
  var r = "";
  for (var i = 0; i < b.length; i++) {
    var ch = String.fromCharCode(b[i]);
    if (b[i] < 0x80 && U.indexOf(ch) >= 0) r += ch;
    else r += "%" + H.charAt(b[i] >> 4) + H.charAt(b[i] & 15);
  }
  return r;
}
var clean = selecto.text.replace(/\s+/g, " ").trim();
if (clean === "") {
  selecto.showText({ fr: "Aucun texte sélectionné", en: "No text selected", es: "Ningún texto seleccionado", ar: "لم يُحدَّد نص" }[selecto.locale] || "No text selected");
} else {
  selecto.openUrl("https://www.youtube.com/results?search_query=" + pct(clean));
}
