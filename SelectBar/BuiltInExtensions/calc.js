// ==SelectoExtension==
// @identifier ai.selecto.builtin.calc
// @name Calculate
// @name:fr Calculer
// @name:en Calculate
// @name:es Calcular
// @name:ar احسب
// @description:fr Calcule l'expression sélectionnée, par exemple 12 × (3 + 4).
// @description:en Calculates the selected expression, for example 12 × (3 + 4).
// @description:es Calcula la expresión seleccionada, por ejemplo 12 × (3 + 4).
// @description:ar يحسب التعبير الرياضي المحدد ويعرض الناتج.
// @icon =
// @regex ^(?=[^\n]*\d)[\d \t\u00A0\u202F.,+\-*/^%()×xX€$£¥A-Za-zÀ-ÿ°\u2212]{1,100}$
// @requirements text
// @enabled-by-default false
// @inline true
// ==/SelectoExtension==
var r = selecto.calc(selecto.text);
if (r) {
  if (selecto.inline) selecto.showText("= " + r);
  else selecto.showResult(r);
}
