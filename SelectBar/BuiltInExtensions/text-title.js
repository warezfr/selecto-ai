// ==SelectoExtension==
// @identifier ai.selecto.builtin.text.title
// @name Title Case
// @name:fr Titre
// @name:en Title Case
// @name:es Título
// @name:ar حالة العنوان
// @description:fr Met chaque mot en majuscule, le reste en minuscules.
// @description:en Capitalizes each word, the rest in lower case.
// @description:es Pone en mayúscula cada palabra, el resto en minúsculas.
// @description:ar يجعل أول حرف من كل كلمة كبيرًا، والباقي صغيرًا.
// @parent ai.selecto.builtin.text
// @icon Aa
// @requirements text
// @enabled-by-default false
// ==/SelectoExtension==
var r = selecto.text.replace(/\S+/g, function (w) { return w.charAt(0).toUpperCase() + w.slice(1).toLowerCase(); });
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
