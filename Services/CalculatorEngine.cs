using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Calculatrice.Services;

public enum AngleMode
{
    Degrees,
    Radians,
    Gradians
}

public enum CalcMode
{
    Standard,
    Scientific,
    Programmer,
    Converter
}

public sealed class CalcException : Exception
{
    public CalcException(string message) : base(message) { }
}

public static class MathEngine
{
    public static AngleMode Angle { get; set; } = AngleMode.Degrees;

    public static bool AllowHexInput { get; set; }

    public static double Evaluate(string expression) => new Parser(expression).Parse();

    static double ToRad(double v) => Angle switch
    {
        AngleMode.Degrees => v * Math.PI / 180.0,
        AngleMode.Gradians => v * Math.PI / 200.0,
        _ => v
    };

    static double FromRad(double r) => Angle switch
    {
        AngleMode.Degrees => r * 180.0 / Math.PI,
        AngleMode.Gradians => r * 200.0 / Math.PI,
        _ => r
    };

    sealed class Parser
    {
        readonly string _s;
        int _i;

        public Parser(string expression)
        {
            _s = expression ?? string.Empty;
        }

        public double Parse()
        {
            if (string.IsNullOrWhiteSpace(_s))
                throw new CalcException("Expression vide");

            double v = BitOr();
            SkipWs();
            if (_i < _s.Length)
                throw new CalcException($"Caractere inattendu : '{_s[_i]}'");
            return v;
        }

        void SkipWs()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }

        bool Match(string token)
        {
            SkipWs();
            if (_i + token.Length > _s.Length) return false;
            if (string.Compare(_s, _i, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;
            _i += token.Length;
            return true;
        }

        bool TryKeyword(string kw)
        {
            SkipWs();
            int save = _i;
            if (_i >= _s.Length || !char.IsLetter(_s[_i])) return false;
            int start = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_')) _i++;
            if (string.Equals(_s[start.._i], kw, StringComparison.OrdinalIgnoreCase)) return true;
            _i = save;
            return false;
        }

        static bool IsHexChar(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        static bool IsHexLetter(char c) =>
            (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        static bool StartsValue(char c) =>
            char.IsAsciiDigit(c) || c == '.' || c == ',' || c == '(' || char.IsLetter(c) || c == '_';

        double BitOr()
        {
            double v = BitXor();
            while (true)
            {
                if (!TryKeyword("OR")) break;
                double r = BitXor();
                v = unchecked((long)v | (long)r);
            }
            return v;
        }

        double BitXor()
        {
            double v = BitAnd();
            while (true)
            {
                if (!TryKeyword("XOR")) break;
                double r = BitAnd();
                v = unchecked((long)v ^ (long)r);
            }
            return v;
        }

        double BitAnd()
        {
            double v = Shift();
            while (true)
            {
                if (!TryKeyword("AND")) break;
                double r = Shift();
                v = unchecked((long)v & (long)r);
            }
            return v;
        }

        double Shift()
        {
            double v = AddSub();
            while (true)
            {
                SkipWs();
                if (_i + 1 < _s.Length && _s[_i] == '<' && _s[_i + 1] == '<')
                {
                    _i += 2;
                    v = unchecked((long)v << (int)AddSub());
                    continue;
                }
                if (_i + 1 < _s.Length && _s[_i] == '>' && _s[_i + 1] == '>')
                {
                    _i += 2;
                    v = unchecked((long)v >> (int)AddSub());
                    continue;
                }
                break;
            }
            return v;
        }

        double AddSub()
        {
            double v = MulDiv();
            while (true)
            {
                SkipWs();
                if (_i >= _s.Length) break;
                char c = _s[_i];
                if (c == '+') { _i++; v += MulDiv(); }
                else if (c == '-') { _i++; v -= MulDiv(); }
                else break;
            }
            return v;
        }

        double MulDiv()
        {
            double v = Unary();
            while (true)
            {
                SkipWs();
                if (_i >= _s.Length) break;
                char c = _s[_i];
                if (c == '*') { _i++; v *= Unary(); }
                else if (c == '/')
                {
                    _i++;
                    double d = Unary();
                    if (d == 0) throw new CalcException("Division par zero");
                    v /= d;
                }
                else if (c == '%')
                {
                    int save = _i;
                    _i++;
                    SkipWs();
                    if (_i >= _s.Length || !StartsValue(_s[_i])) { _i = save; v /= 100.0; }
                    else v %= Unary();
                }
                else if (TryKeyword("MOD")) { v %= Unary(); }
                else break;
            }
            return v;
        }

        double Unary()
        {
            SkipWs();
            if (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == '-') { _i++; return -Unary(); }
                if (c == '+') { _i++; return Unary(); }
                if (c == '%') { _i++; return Unary() / 100.0; }
            }
            return Power();
        }

        double Power()
        {
            double b = Postfix();
            SkipWs();
            if (_i < _s.Length && _s[_i] == '^')
            {
                _i++;
                return Math.Pow(b, Unary());
            }
            return b;
        }

        double Postfix()
        {
            double v = Primary();
            while (true)
            {
                SkipWs();
                if (_i < _s.Length && _s[_i] == '!')
                {
                    _i++;
                    v = Factorial(v);
                }
                else break;
            }
            return v;
        }

        double Primary()
        {
            SkipWs();
            if (_i >= _s.Length) throw new CalcException("Expression incomplete");

            char c = _s[_i];

            if (c == '(')
            {
                _i++;
                double v = BitOr();
                SkipWs();
                if (_i >= _s.Length || _s[_i] != ')')
                    throw new CalcException("Parenthese manquante");
                _i++;
                return v;
            }

            if (char.IsDigit(c) || c == '.' || c == ',')
                return ParseNumber();

            if (char.IsLetter(c) || c == '_')
                return ParseIdentifier();

            throw new CalcException($"Caractere invalide : '{c}'");
        }

        double ParseNumber()
        {
            int start = _i;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsAsciiDigit(c) || c == '.' || c == ',') { _i++; continue; }
                if (AllowHexInput && c != 'e' && c != 'E' && IsHexChar(c)) { _i++; continue; }
                break;
            }

            string num = _s[start.._i];
            if (num.Length == 0) throw new CalcException("Nombre incomplet");

            int save = _i;
            if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
            {
                int j = _i + 1;
                if (j < _s.Length && (_s[j] == '+' || _s[j] == '-')) j++;
                if (j < _s.Length && char.IsAsciiDigit(_s[j]))
                {
                    while (j < _s.Length && char.IsAsciiDigit(_s[j])) j++;
                    _i = j;
                    string full = _s[start.._i].Replace(',', '.');
                    if (double.TryParse(full, NumberStyles.Float, CultureInfo.InvariantCulture, out double ev))
                        return ev;
                    _i = save;
                }
            }

            if (num.Contains('.') || num.Contains(','))
            {
                if (double.TryParse(num.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return d;
                throw new CalcException($"Nombre invalide : {num}");
            }

            if (AllowHexInput && num.Any(IsHexLetter))
            {
                if (long.TryParse(num, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long h))
                    return h;
            }

            if (double.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out double i2))
                return i2;

            throw new CalcException($"Nombre invalide : {num}");
        }

        double ParseIdentifier()
        {
            int start = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_')) _i++;
            string id = _s[start.._i];

            int afterId = _i;
            SkipWs();
            if (_i < _s.Length && _s[_i] == '(')
            {
                _i++;
                var args = new List<double>();
                SkipWs();
                if (_i < _s.Length && _s[_i] == ')')
                {
                    _i++;
                    return ApplyFunction(id.ToLowerInvariant(), args.ToArray());
                }
                while (true)
                {
                    args.Add(BitOr());
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ';') { _i++; continue; }
                    if (_i < _s.Length && _s[_i] == ')') { _i++; break; }
                    throw new CalcException("Parenthese manquante");
                }
                return ApplyFunction(id.ToLowerInvariant(), args.ToArray());
            }

            _i = afterId;

            if (AllowHexInput && id.Length > 0 && id.Any(IsHexLetter) && id.All(IsHexChar) &&
                long.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv))
            {
                return hv;
            }

            return Constant(id);
        }

        static double Constant(string id) => id.ToLowerInvariant() switch
        {
            "pi" or "\u03c0" => Math.PI,
            "e" => Math.E,
            "tau" => Math.Tau,
            _ => throw new CalcException($"Constante inconnue : {id}")
        };

        static double Factorial(double v)
        {
            if (v < 0 || Math.Abs(v - Math.Round(v)) > 1e-9)
                throw new CalcException("Factorielle : entier positif requis");
            int n = (int)Math.Round(v);
            if (n > 170) throw new CalcException("Factorielle trop grande");
            double r = 1;
            for (int k = 2; k <= n; k++) r *= k;
            return r;
        }

        static double ApplyFunction(string name, double[] a)
        {
            double X(int i) => i < a.Length ? a[i] : throw new CalcException($"Argument manquant pour {name}");

            switch (name)
            {
                case "sin": return Math.Sin(ToRad(X(0)));
                case "cos": return Math.Cos(ToRad(X(0)));
                case "tan": return Math.Tan(ToRad(X(0)));
                case "asin": return FromRad(Math.Asin(X(0)));
                case "acos": return FromRad(Math.Acos(X(0)));
                case "atan": return FromRad(Math.Atan(X(0)));
                case "sinh": return Math.Sinh(X(0));
                case "cosh": return Math.Cosh(X(0));
                case "tanh": return Math.Tanh(X(0));
                case "ln": return Math.Log(X(0));
                case "log": return Math.Log10(X(0));
                case "log2": return Math.Log2(X(0));
                case "exp": return Math.Exp(X(0));
                case "sqrt": return Math.Sqrt(X(0));
                case "cbrt": return Math.Cbrt(X(0));
                case "sqr": return X(0) * X(0);
                case "pct": return X(0) / 100.0;
                case "cube": return X(0) * X(0) * X(0);
                case "inv": return 1.0 / X(0);
                case "neg": return -X(0);
                case "abs": return Math.Abs(X(0));
                case "fact": case "factorial": return Factorial(X(0));
                case "floor": return Math.Floor(X(0));
                case "ceil": return Math.Ceiling(X(0));
                case "round": return Math.Round(X(0), MidpointRounding.AwayFromZero);
                case "trunc": return Math.Truncate(X(0));
                case "sign": return Math.Sign(X(0));
                case "not": return unchecked((long)~(long)X(0));
                case "and": return unchecked((long)X(0) & (long)X(1));
                case "or": return unchecked((long)X(0) | (long)X(1));
                case "xor": return unchecked((long)X(0) ^ (long)X(1));
                case "mod": return X(0) % X(1);
                case "pow": return Math.Pow(X(0), X(1));
                case "root": return Math.Pow(X(0), 1.0 / X(1));
                case "min": return Math.Min(X(0), X(1));
                case "max": return Math.Max(X(0), X(1));
                case "hypot": return Math.Sqrt((X(0) * X(0)) + (X(1) * X(1)));
                default: throw new CalcException($"Fonction inconnue : {name}");
            }
        }
    }

    public static string Format(double value, int maxDigits = 12)
    {
        if (double.IsNaN(value)) return "Erreur";
        if (double.IsInfinity(value)) return value > 0 ? "\u221E" : "-\u221E";
        if (value == 0) return "0";

        double rounded = Math.Round(value, maxDigits - 1, MidpointRounding.AwayFromZero);
        string s;

        if (Math.Abs(rounded) >= 1e11 && Math.Abs(rounded) < 9.0e18 &&
            Math.Abs(rounded - Math.Truncate(rounded)) < 1e-6)
            s = ((long)rounded).ToString(CultureInfo.InvariantCulture);
        else
            s = rounded.ToString("G" + maxDigits, CultureInfo.InvariantCulture);

        int e = s.IndexOfAny(new[] { 'E', 'e' });
        string mant = e >= 0 ? s[..e] : s;
        string exp = e >= 0 ? "E" + s[(e + 1)..] : string.Empty;

        mant = mant.Replace('.', ',');
        int dot = mant.IndexOf(',');
        string ip = dot >= 0 ? mant[..dot] : mant;
        string fp = dot >= 0 ? mant[(dot + 1)..] : string.Empty;

        bool neg = ip.StartsWith('-');
        if (neg) ip = ip[1..];

        return (neg ? "-" : string.Empty) + Group(ip) + (fp.Length > 0 ? "," + fp : string.Empty) + exp;
    }

    public static string FormatPlain(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
        if (v == Math.Truncate(v) && Math.Abs(v) < 1e15)
            return ((long)v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("G15", CultureInfo.InvariantCulture).Replace("E", "e");
    }

    static string Group(string digits)
    {
        if (digits.Length <= 3) return digits;
        var sb = new StringBuilder();
        int lead = digits.Length % 3;
        if (lead > 0) sb.Append(digits, 0, lead);
        for (int i = lead; i < digits.Length; i += 3)
        {
            if (sb.Length > 0) sb.Append('\u00A0');
            sb.Append(digits, i, 3);
        }
        return sb.ToString();
    }

    public static string ToBase(double value, int numberBase)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "\u2014";

        long l = (long)Math.Round(value, MidpointRounding.AwayFromZero);
        ulong u = l < 0 ? unchecked((ulong)l) : (ulong)l;
        string s = Convert.ToString(unchecked((long)u), numberBase).ToUpperInvariant();

        if (numberBase == 2)
        {
            int pad = (4 - (s.Length % 4)) % 4;
            if (pad > 0) s = new string('0', pad) + s;
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i += 4)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(s, i, 4);
            }
            s = sb.ToString();
        }
        else if (numberBase == 16)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i += 8)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(s, i, Math.Min(8, s.Length - i));
            }
            s = sb.ToString();
        }

        return s;
    }

    static readonly (string Token, string Pretty)[] PrettyMap =
    {
        ("AND", "AND"), ("XOR", "XOR"), ("NOT", "NOT"), ("MOD", "MOD"), ("OR", "OR"),
        ("inv", "1/"), ("sqrt", "\u221A"), ("cbrt", "\u221B"), ("sqr", "x\u00B2"), ("cube", "x\u00B3"),
        ("neg", "\u2212"), ("pct", "%"), ("pi", "\u03C0"), ("log2", "log\u2082"), ("log", "log"), ("ln", "ln")
    };

    public static string ToPretty(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return string.Empty;

        string s = expression
            .Replace("*", "\u00D7")
            .Replace("/", "\u00F7")
            .Replace("-", "\u2212")
            .Replace("<<", "\u21E4")
            .Replace(">>", "\u21E5");

        foreach (var (token, pretty) in PrettyMap)
        {
            string replacement = pretty.Replace("$", "$$");
            s = Regex.Replace(s, $@"\b{Regex.Escape(token)}\b", replacement, RegexOptions.IgnoreCase);
        }

        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}

public sealed class UnitDefinition
{
    public string Symbol { get; }
    public string Name { get; }
    public double Factor { get; }
    public double Offset { get; }

    public UnitDefinition(string symbol, string name, double factor, double offset = 0)
    {
        Symbol = symbol;
        Name = name;
        Factor = factor;
        Offset = offset;
    }

    public string Label => $"{Symbol} \u2014 {Name}";
}

public sealed class UnitCategory
{
    public string Name { get; }
    public UnitDefinition[] Units { get; }

    public UnitCategory(string name, params UnitDefinition[] units)
    {
        Name = name;
        Units = units;
    }

    public double ToBase(double value, UnitDefinition unit) => (value * unit.Factor) + unit.Offset;

    public double FromBase(double baseValue, UnitDefinition unit) => (baseValue - unit.Offset) / unit.Factor;

    public double Convert(double value, UnitDefinition from, UnitDefinition to) =>
        FromBase(ToBase(value, from), to);
}

public static class UnitConverter
{
    public static readonly UnitCategory[] Categories =
    {
        new("Longueur",
            new UnitDefinition("mm", "millimetre", 0.001),
            new UnitDefinition("cm", "centimetre", 0.01),
            new UnitDefinition("m", "metre", 1),
            new UnitDefinition("km", "kilometre", 1000),
            new UnitDefinition("in", "pouce", 0.0254),
            new UnitDefinition("ft", "pied", 0.3048),
            new UnitDefinition("yd", "yard", 0.9144),
            new UnitDefinition("mi", "mile", 1609.344),
            new UnitDefinition("nmi", "mille marin", 1852)),

        new("Masse",
            new UnitDefinition("mg", "milligramme", 0.000001),
            new UnitDefinition("g", "gramme", 0.001),
            new UnitDefinition("kg", "kilogramme", 1),
            new UnitDefinition("t", "tonne", 1000),
            new UnitDefinition("oz", "once", 0.028349523125),
            new UnitDefinition("lb", "livre", 0.45359237),
            new UnitDefinition("st", "stone", 6.35029318)),

        new("Temperature",
            new UnitDefinition("\u00B0C", "Celsius", 1),
            new UnitDefinition("K", "Kelvin", 1, 273.15),
            new UnitDefinition("\u00B0F", "Fahrenheit", 5.0 / 9.0, -17.77777777777778),
            new UnitDefinition("\u00B0R", "Rankine", 5.0 / 9.0, -273.15)),

        new("Aire",
            new UnitDefinition("mm\u00B2", "millimetre carre", 0.000001),
            new UnitDefinition("cm\u00B2", "centimetre carre", 0.0001),
            new UnitDefinition("m\u00B2", "metre carre", 1),
            new UnitDefinition("ha", "hectare", 10000),
            new UnitDefinition("km\u00B2", "kilometre carre", 1000000),
            new UnitDefinition("in\u00B2", "pouce carre", 0.00064516),
            new UnitDefinition("ft\u00B2", "pied carre", 0.09290304),
            new UnitDefinition("acre", "acre", 4046.8564224)),

        new("Volume",
            new UnitDefinition("mL", "millilitre", 0.001),
            new UnitDefinition("cL", "centilitre", 0.01),
            new UnitDefinition("L", "litre", 1),
            new UnitDefinition("m\u00B3", "metre cube", 1000),
            new UnitDefinition("in\u00B3", "pouce cube", 0.016387064),
            new UnitDefinition("gal", "gallon US", 3.785411784),
            new UnitDefinition("pt", "pinte US", 0.473176473),
            new UnitDefinition("floz", "once liquide US", 0.0295735295625)),

        new("Vitesse",
            new UnitDefinition("m/s", "metre par seconde", 1),
            new UnitDefinition("km/h", "kilometre par heure", 0.277777777777778),
            new UnitDefinition("mph", "mille par heure", 0.44704),
            new UnitDefinition("kn", "noeud", 0.514444444444444),
            new UnitDefinition("ft/s", "pied par seconde", 0.3048)),

        new("Temps",
            new UnitDefinition("ns", "nanoseconde", 1e-9),
            new UnitDefinition("\u00B5s", "microseconde", 1e-6),
            new UnitDefinition("ms", "milliseconde", 0.001),
            new UnitDefinition("s", "seconde", 1),
            new UnitDefinition("min", "minute", 60),
            new UnitDefinition("h", "heure", 3600),
            new UnitDefinition("j", "jour", 86400),
            new UnitDefinition("sem", "semaine", 604800)),

        new("Donnees",
            new UnitDefinition("bit", "bit", 0.125),
            new UnitDefinition("o", "octet", 1),
            new UnitDefinition("Ko", "kilooctet", 1024),
            new UnitDefinition("Mo", "megaoctet", 1048576),
            new UnitDefinition("Go", "gigaoctet", 1073741824),
            new UnitDefinition("To", "teraoctet", 1099511627776)),

        new("Angle",
            new UnitDefinition("\u00B0", "degre", 1),
            new UnitDefinition("rad", "radian", 57.29577951308232),
            new UnitDefinition("grad", "gradian", 0.9),
            new UnitDefinition("'", "minute d'arc", 1.0 / 60.0),
            new UnitDefinition("\"", "seconde d'arc", 1.0 / 3600.0)),

        new("Energie",
            new UnitDefinition("J", "joule", 1),
            new UnitDefinition("kJ", "kilojoule", 1000),
            new UnitDefinition("cal", "calorie", 4.184),
            new UnitDefinition("kcal", "kilocalorie", 4184),
            new UnitDefinition("Wh", "wattheure", 3600),
            new UnitDefinition("kWh", "kilowattheure", 3600000),
            new UnitDefinition("eV", "electronvolt", 1.602176634e-19),
            new UnitDefinition("BTU", "BTU", 1055.05585262)),

        new("Puissance",
            new UnitDefinition("W", "watt", 1),
            new UnitDefinition("kW", "kilowatt", 1000),
            new UnitDefinition("MW", "megawatt", 1000000),
            new UnitDefinition("hp", "cheval-vapeur", 745.699871582270),
            new UnitDefinition("cal/s", "calorie par seconde", 4.184))
    };
}
