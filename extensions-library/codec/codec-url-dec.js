// ==SelectoExtension==
// @identifier ai.selecto.library.codec.url-dec
// @parent ai.selecto.library.codec
// @name URL: decode
// @name:fr URL : décoder
// @name:en URL: decode
// @name:es URL: decodificar
// @name:ar URL: فك ترميز
// @description:fr Décode une URL : %20 redevient un espace.
// @description:en Decodes a URL: %20 becomes a space.
// @description:es Decodifica una URL: %20 vuelve a ser un espacio.
// @description:ar يفك ترميز عنوان URL: %20 يصبح مسافة.
// @icon %
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = null;
try { r = decodeURIComponent(selecto.text); } catch (e) { r = null; }
if (r === null) {
  selecto.showText({ fr: "Encodage URL invalide", en: "Invalid URL encoding", es: "Codificación de URL no válida", ar: "ترميز الرابط غير صالح" }[selecto.locale] || "Invalid URL encoding");
} else {
  if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
  var done = selecto.editable
    ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
    : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
  selecto.showText(done[selecto.locale] || done.en);
}
