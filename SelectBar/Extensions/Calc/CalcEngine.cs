using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SelectBar.Extensions;

/// <summary>
/// Évalue une expression arithmétique saisie dans une sélection : + - * / ^ %, parenthèses, « 20 % de 80 », « 2 x 3 », « 2 × 3 ».
/// Pur C# (pas d'eval JavaScript). Renvoie null si la sélection n'est pas une expression : dates, téléphones,
/// fractions en toutes lettres (« 1/2 tasse »), texte, nombre seul, division par zéro, nombre trop grand.
/// </summary>
public static class CalcEngine
{
    private const int MaxInputLength = 200;
    private const int MaxNumberLength = 20;
    private const int MaxDepth = 50;
    private const decimal MaxAbs = 1_000_000_000_000_000m;

    /// <summary>Dates entières seulement (« 2024-10-24 », « 10/24/2024 ») : « 10.5-3.25 » reste un calcul.</summary>
    private static readonly Regex DateLike = new(
        @"^(\d{4}-\d{1,2}(-\d{1,2})?|\d{1,2}[/.-]\d{1,2}[/.-]\d{2,4})$", RegexOptions.CultureInvariant);

    private enum Kind { Number, Plus, Minus, Star, Slash, Caret, Percent, Times, LParen, RParen, De }

    private readonly record struct Token(Kind Kind, decimal Value = 0);

    private sealed class CalcException : Exception { }

    /// <summary>Résultat formaté dans la langue de la culture (« 1 500 » fr, « 1,024 » en), ou null si la sélection n'est pas calculable.</summary>
    public static string? Evaluate(string selection, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(selection) || selection.Length > MaxInputLength) return null;
        var text = selection.Trim();
        if (DateLike.IsMatch(text)) return null;
        try
        {
            var tokens = Tokenize(text);
            if (tokens == null || !HasOperator(tokens)) return null;
            var value = new Parser(tokens).ParseAll();
            if (Math.Abs(value) > MaxAbs) return null;
            return FormatResult(value, culture);
        }
        catch (Exception ex) when (ex is CalcException or ArithmeticException)
        {
            return null;
        }
    }

    /// <summary>Séparateurs de la langue : fr « 1 500,5 » (espace insécable), es « 1.500,5 », autres « 1,500.5 ».</summary>
    public static (string Group, char Decimal) Separators(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "fr" => (" ", ','),
        "es" => (".", ','),
        _ => (",", '.'),
    };

    /// <summary>Nombre arrondi à <paramref name="decimals"/> décimales, avec les séparateurs de la langue.</summary>
    public static string FormatNumber(decimal value, int decimals, CultureInfo culture)
    {
        var (group, dec) = Separators(culture);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var digits = Math.Abs(rounded).ToString("F" + decimals, CultureInfo.InvariantCulture);
        var parts = digits.Split('.');
        var integer = parts[0];
        var sb = new StringBuilder();
        if (rounded < 0) sb.Append('-');
        for (var i = 0; i < integer.Length; i++)
        {
            if (i > 0 && (integer.Length - i) % 3 == 0) sb.Append(group);
            sb.Append(integer[i]);
        }
        if (parts.Length > 1) sb.Append(dec).Append(parts[1]);
        return sb.ToString();
    }

    /// <summary>Résultat d'un calcul : jusqu'à 6 décimales, zéros inutiles retirés (« 1 500 », « 0,3333 »).</summary>
    public static string FormatResult(decimal value, CultureInfo culture)
    {
        var text = FormatNumber(value, 6, culture);
        var dec = Separators(culture).Decimal;
        if (text.IndexOf(dec) >= 0) text = text.TrimEnd('0').TrimEnd(dec);
        return text;
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsGroupSeparator(char c) => c is ' ' or ' ' or ' ';

    private static Kind? SymbolKind(char c) => c switch
    {
        '+' => Kind.Plus,
        '-' or '−' => Kind.Minus,
        '*' or '×' => Kind.Star,
        '/' => Kind.Slash,
        '^' => Kind.Caret,
        '%' => Kind.Percent,
        '(' => Kind.LParen,
        ')' => Kind.RParen,
        _ => (Kind?)null,
    };

    private static List<Token>? Tokenize(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || IsGroupSeparator(c)) { i++; continue; }
            if (IsDigit(c))
            {
                if (!TryReadNumber(s, ref i, out var number)) return null;
                tokens.Add(new Token(Kind.Number, number));
                continue;
            }
            if (char.IsLetter(c))
            {
                var start = i;
                while (i < s.Length && char.IsLetter(s[i])) i++;
                var word = s.Substring(start, i - start).ToLowerInvariant();
                if (word == "x") tokens.Add(new Token(Kind.Times));
                else if (word == "de") tokens.Add(new Token(Kind.De));
                else return null;
                continue;
            }
            var kind = SymbolKind(c);
            if (kind == null) return null;
            tokens.Add(new Token(kind.Value));
            i++;
        }
        return tokens;
    }

    /// <summary>
    /// Nombre : chiffres, groupes de milliers séparés par un espace (normal, insécable ou fine) suivi de exactement trois chiffres,
    /// décimale « , » ou « . » suivie de chiffres.
    /// </summary>
    private static bool TryReadNumber(string s, ref int i, out decimal value)
    {
        value = 0;
        var digits = new StringBuilder();
        while (i < s.Length && IsDigit(s[i])) digits.Append(s[i++]);
        while (i + 3 < s.Length && IsGroupSeparator(s[i]) && IsDigit(s[i + 1]) && IsDigit(s[i + 2]) && IsDigit(s[i + 3])
               && (i + 4 >= s.Length || !IsDigit(s[i + 4])))
        {
            digits.Append(s, i + 1, 3);
            i += 4;
        }
        if (i + 1 < s.Length && (s[i] == ',' || s[i] == '.') && IsDigit(s[i + 1]))
        {
            digits.Append('.');
            i++;
            while (i < s.Length && IsDigit(s[i])) digits.Append(s[i++]);
        }
        if (digits.Length == 0 || digits.Length > MaxNumberLength) return false;
        return decimal.TryParse(digits.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool HasOperator(List<Token> tokens)
    {
        for (var i = 1; i < tokens.Count; i++)
            if (tokens[i].Kind is Kind.Plus or Kind.Minus or Kind.Star or Kind.Slash or Kind.Caret or Kind.Percent or Kind.Times)
                return true;
        return false;
    }

    private static decimal Pow(decimal numberBase, decimal exponent)
    {
        if (numberBase == 0 && exponent <= 0) throw new CalcException();
        var result = Math.Pow((double)numberBase, (double)exponent);
        if (double.IsNaN(result) || double.IsInfinity(result) || Math.Abs(result) > (double)MaxAbs) throw new CalcException();
        return (decimal)result;
    }

    /// <summary>Analyseur récursif descendant : expr = terme (+|- terme)* ; terme = unaire ((*|/|x|×) unaire | (…))* ; etc.</summary>
    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;
        private int _depth;

        public Parser(List<Token> tokens) => _tokens = tokens;

        public decimal ParseAll()
        {
            var value = Expr();
            if (_pos != _tokens.Count) throw new CalcException();
            return value;
        }

        private bool Is(Kind kind) => _pos < _tokens.Count && _tokens[_pos].Kind == kind;

        private decimal Expr()
        {
            var value = Term();
            while (Is(Kind.Plus) || Is(Kind.Minus))
            {
                var minus = _tokens[_pos++].Kind == Kind.Minus;
                var right = Term();
                value = minus ? value - right : value + right;
            }
            return value;
        }

        private decimal Term()
        {
            var value = Unary();
            while (true)
            {
                if (Is(Kind.Star) || Is(Kind.Times))
                {
                    _pos++;
                    value *= Unary();
                }
                else if (Is(Kind.Slash))
                {
                    _pos++;
                    var divisor = Unary();
                    if (divisor == 0) throw new CalcException();
                    value /= divisor;
                }
                else if (Is(Kind.LParen))
                {
                    value *= Unary();   // multiplication implicite : 2(3+1)
                }
                else
                {
                    return value;
                }
            }
        }

        private decimal Unary()
        {
            if (Is(Kind.Minus))
            {
                _pos++;
                return -Unary();
            }
            return Power();
        }

        private decimal Power()
        {
            var value = Postfix();
            if (!Is(Kind.Caret)) return value;
            _pos++;
            return Pow(value, Unary());
        }

        /// <summary>« 20 % » = 0,2 ; « 20 % de 80 » = 16.</summary>
        private decimal Postfix()
        {
            var value = Primary();
            while (Is(Kind.Percent))
            {
                _pos++;
                if (Is(Kind.De))
                {
                    _pos++;
                    return value / 100m * Power();
                }
                value /= 100m;
            }
            return value;
        }

        private decimal Primary()
        {
            if (Is(Kind.Number)) return _tokens[_pos++].Value;
            if (!Is(Kind.LParen)) throw new CalcException();
            if (++_depth > MaxDepth) throw new CalcException();
            _pos++;
            var value = Expr();
            if (!Is(Kind.RParen)) throw new CalcException();
            _pos++;
            _depth--;
            return value;
        }
    }
}
