// ==SelectoExtension==
// @identifier ai.selecto.library.codec.b64-dec
// @parent ai.selecto.library.codec
// @name Base64: decode
// @name:fr Base64 : décoder
// @name:en Base64: decode
// @name:es Base64: decodificar
// @name:ar Base64: فك ترميز
// @description:fr Décode un texte encodé en Base64.
// @description:en Decodes Base64-encoded text.
// @description:es Decodifica un texto codificado en Base64.
// @description:ar يفك ترميز نص مُرمّز بصيغة Base64.
// @icon 64
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var A = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
function unb64(s) {
  var t = s.replace(/\s+/g, "");
  if (!/^[A-Za-z0-9+\/]*={0,2}$/.test(t)) return null;
  if (t.indexOf("=") >= 0 && t.length % 4 !== 0) return null;
  var body = t.replace(/=+$/, "");
  if (body.length % 4 === 1) return null;
  var bytes = [];
  var buf = 0;
  var bits = 0;
  for (var i = 0; i < body.length; i++) {
    buf = ((buf << 6) | A.indexOf(body.charAt(i))) & 0xFFFFFF;
    bits += 6;
    if (bits >= 8) {
      bits -= 8;
      bytes.push((buf >> bits) & 255);
    }
  }
  return bytes;
}
function fromUtf8(b) {
  var s = "";
  var i = 0;
  while (i < b.length) {
    var c = b[i];
    var n = 0;
    var cp = 0;
    if (c < 0x80) { cp = c; n = 0; }
    else if (c >= 0xC2 && c <= 0xDF) { cp = c & 0x1F; n = 1; }
    else if (c >= 0xE0 && c <= 0xEF) { cp = c & 0x0F; n = 2; }
    else if (c >= 0xF0 && c <= 0xF4) { cp = c & 0x07; n = 3; }
    else return null;
    if (i + n >= b.length && n > 0) return null;
    for (var k = 1; k <= n; k++) {
      var cc = b[i + k];
      if ((cc & 0xC0) !== 0x80) return null;
      cp = (cp << 6) | (cc & 0x3F);
    }
    if (n === 2 && (cp < 0x800 || (cp >= 0xD800 && cp <= 0xDFFF))) return null;
    if (n === 3 && (cp < 0x10000 || cp > 0x10FFFF)) return null;
    if (cp >= 0x10000) {
      cp -= 0x10000;
      s += String.fromCharCode(0xD800 + (cp >> 10), 0xDC00 + (cp & 0x3FF));
    } else {
      s += String.fromCharCode(cp);
    }
    i += n + 1;
  }
  return s;
}
var bytes = unb64(selecto.text);
var r = bytes === null ? null : fromUtf8(bytes);
if (r === null) {
  selecto.showText({ fr: "Base64 invalide", en: "Invalid Base64", es: "Base64 no válido", ar: "Base64 غير صالح" }[selecto.locale] || "Invalid Base64");
} else {
  if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
  var done = selecto.editable
    ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
    : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
  selecto.showText(done[selecto.locale] || done.en);
}
