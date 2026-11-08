// ==SelectoExtension==
// @identifier ai.selecto.library.codec.b64-enc
// @parent ai.selecto.library.codec
// @name Base64: encode
// @name:fr Base64 : encoder
// @name:en Base64: encode
// @name:es Base64: codificar
// @name:ar Base64: ترميز
// @description:fr Encode le texte en Base64.
// @description:en Encodes text to Base64.
// @description:es Codifica el texto en Base64.
// @description:ar يرمّز النص بصيغة Base64.
// @icon 64
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var A = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
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
for (var i = 0; i < b.length; i += 3) {
  var b0 = b[i];
  var b1 = i + 1 < b.length ? b[i + 1] : 0;
  var b2 = i + 2 < b.length ? b[i + 2] : 0;
  var n = (b0 << 16) | (b1 << 8) | b2;
  r += A.charAt((n >> 18) & 63) + A.charAt((n >> 12) & 63)
    + (i + 1 < b.length ? A.charAt((n >> 6) & 63) : "=")
    + (i + 2 < b.length ? A.charAt(n & 63) : "=");
}
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
