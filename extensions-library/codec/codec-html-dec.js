// ==SelectoExtension==
// @identifier ai.selecto.library.codec.html-dec
// @parent ai.selecto.library.codec
// @name HTML: decode
// @name:fr HTML : décoder
// @name:en HTML: decode
// @name:es HTML: decodificar
// @name:ar HTML: فك ترميز
// @description:fr Décode les entités HTML, comme &amp; ou &eacute;.
// @description:en Decodes HTML entities, such as &amp; or &eacute;.
// @description:es Decodifica las entidades HTML, como &amp; o &eacute;.
// @description:ar يفك ترميز كيانات HTML، مثل &amp; أو &eacute;.
// @icon &
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var named = { amp: "&", lt: "<", gt: ">", quot: "\"", apos: "'", nbsp: " " };
function fromCp(cp) {
  if (cp < 0x10000) return String.fromCharCode(cp);
  cp -= 0x10000;
  return String.fromCharCode(0xD800 + (cp >> 10), 0xDC00 + (cp & 0x3FF));
}
var r = selecto.text.replace(/&(#[xX][0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);/g, function (m, e) {
  if (e.charAt(0) === "#") {
    var cp = (e.charAt(1) === "x" || e.charAt(1) === "X") ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10);
    if (!(cp > 0 && cp <= 0x10FFFF) || (cp >= 0xD800 && cp <= 0xDFFF)) return m;
    return fromCp(cp);
  }
  return named.hasOwnProperty(e) ? named[e] : m;
});
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
