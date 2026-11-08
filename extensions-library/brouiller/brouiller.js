// ==SelectoExtension==
// @identifier ai.selecto.library.brouiller
// @name Brouiller
// @name:fr Brouiller
// @name:en Scramble
// @name:es Ofuscar
// @name:ar تشويش النص
// @description:fr Brouille le texte en ROT13 ; une seconde application le rétablit.
// @description:en Scramble the text with ROT13; applying it twice restores it.
// @description:es Oculta el texto con ROT13; aplicarlo dos veces lo restaura.
// @description:ar شوّش النص بترميز ROT13، وتطبيقه مرتين يعيده كما كان.
// @icon Rx
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = selecto.text.replace(/[a-zA-Z]/g, function (c) {
  var base = c <= "Z" ? 65 : 97;
  return String.fromCharCode((c.charCodeAt(0) - base + 13) % 26 + base);
});
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
