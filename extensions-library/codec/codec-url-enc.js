// ==SelectoExtension==
// @identifier ai.selecto.library.codec.url-enc
// @parent ai.selecto.library.codec
// @name URL: encode
// @name:fr URL : encoder
// @name:en URL: encode
// @name:es URL: codificar
// @name:ar URL: ترميز
// @description:fr Encode le texte pour l'inclure dans une URL.
// @description:en Encodes text so it can go in a URL.
// @description:es Codifica el texto para incluirlo en una URL.
// @description:ar يرمّز النص ليُدرج في عنوان URL.
// @icon %
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
var b = utf8(selecto.text);
var r = "";
for (var i = 0; i < b.length; i++) {
  var ch = String.fromCharCode(b[i]);
  if (U.indexOf(ch) >= 0 && b[i] < 0x80) r += ch;
  else r += "%" + H.charAt(b[i] >> 4) + H.charAt(b[i] & 15);
}
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
