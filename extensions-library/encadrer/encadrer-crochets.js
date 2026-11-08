// ==SelectoExtension==
// @identifier ai.selecto.library.encadrer.crochets
// @name Crochets
// @name:fr Crochets
// @name:en Square brackets
// @name:es Corchetes
// @name:ar أقواس مربعة
// @description:fr Entoure la sélection de crochets [ ].
// @description:en Wraps the selection in square brackets [ ].
// @description:es Rodea la selección con corchetes [ ].
// @description:ar يحيط التحديد بأقواس مربعة [ ].
// @parent ai.selecto.library.encadrer
// @icon []
// @requirements text
// @enabled-by-default true
// ==/SelectoExtension==
var r = "[" + selecto.text + "]";
if (selecto.editable) selecto.pasteText(r); else selecto.copyText(r);
var done = selecto.editable
  ? { fr: "Remplacé", en: "Replaced", es: "Reemplazado", ar: "تم الاستبدال" }
  : { fr: "Copié", en: "Copied", es: "Copiado", ar: "تم النسخ" };
selecto.showText(done[selecto.locale] || done.en);
