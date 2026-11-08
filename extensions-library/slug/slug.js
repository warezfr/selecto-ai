// ==SelectoExtension==
// @identifier ai.selecto.library.slug
// @name Slug
// @name:fr Slug
// @name:en Slug
// @name:es Slug
// @name:ar معرّف الرابط
// @description:fr Crée un identifiant propre pour une adresse web.
// @description:en Create a clean slug for a web address.
// @description:es Crea un slug limpio para una dirección web.
// @description:ar أنشئ معرّفًا نظيفًا لعنوان الويب.
// @icon /s
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = selecto.text.normalize("NFD").replace(/[̀-ͯ]/g, "")
  .toLowerCase()
  .replace(/[^a-z0-9]+/g, "-")
  .replace(/^-+|-+$/g, "");
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
