using System.Collections.ObjectModel;
using System.Globalization;
using Calculatrice.Services;
using Microsoft.Maui.Devices;

namespace Calculatrice;

public sealed class HistoryItem
{
    public string Display { get; init; } = string.Empty;
    public string Expression { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
}

public partial class MainPage : ContentPage
{
    const int MaxHistory = 60;

    static readonly Color CCardAlt = Color.FromArgb("#1B2237");
    static readonly Color CAccent = Color.FromArgb("#6C8CFF");
    static readonly Color CTextMuted = Color.FromArgb("#8A93AD");

    readonly ObservableCollection<HistoryItem> _history = new();

    string _expr = string.Empty;
    bool _fresh = true;
    bool _lockDisplay;
    bool _busy;
    int _autoClose;
    double _last;
    bool _hasLast;
    double _memory;
    double _pendingBase;
    string? _pendingOp;
    bool _hasPending;
    int _base = 10;
    CalcMode _mode = CalcMode.Standard;
    AngleMode _angle = AngleMode.Degrees;
    UnitCategory _category = UnitConverter.Categories[0];

    public MainPage()
    {
        InitializeComponent();

        RootLayout.Padding = DeviceInfo.Current.Platform == DevicePlatform.Android
            ? new Thickness(0, 26, 0, 12)
            : new Thickness(0, 10, 0, 8);

        HistoryList.ItemsSource = _history;
        HistoryCountLabel.Text = "Aucun calcul enregistre";

        ConverterInit();
        ApplyMode();
    }

    // ------------------------------------------------------------------ utils

    static bool IsDigit(string s) => s.Length == 1 && char.IsAsciiDigit(s[0]);

    static bool IsHexLetter(char c) => (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    static bool IsHexChar(char c) => char.IsAsciiHexDigit(c);

    static string TrailingToken(string s)
    {
        int i = s.Length;
        while (i > 0 && (char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '.' || s[i - 1] == ',')) i--;
        return s[i..];
    }

    bool TrailingIsIdentifier(string tail)
    {
        if (tail.Length == 0) return false;
        if (tail.All(c => char.IsAsciiDigit(c) || c == '.' || c == ',')) return false;
        if (_mode == CalcMode.Programmer && tail.All(IsHexChar) && tail.Any(IsHexLetter)) return false;
        return true;
    }

    static bool CanStartOperand(char c) =>
        c is '+' or '-' or '*' or '/' or '^' or '%' or '<' or '>' or '(' or ',' or ' ';

    static int MatchOpen(string s, int closeIndex)
    {
        int depth = 0;
        for (int i = closeIndex; i >= 0; i--)
        {
            if (s[i] == ')') depth++;
            else if (s[i] == '(')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    void CloseAuto()
    {
        if (_autoClose <= 0) return;
        _expr += new string(')', _autoClose);
        _autoClose = 0;
    }

    void Unlock() => _lockDisplay = false;

    // ------------------------------------------------------------------ saisie

    void AppendDigit(string d)
    {
        Unlock();
        if (_fresh) { _expr = string.Empty; _fresh = false; }

        if (_expr.Length == 0 || CanStartOperand(_expr[^1])) { _expr += d; return; }
        if (_expr[^1] == ')') { _expr += "*" + d; return; }

        string tail = TrailingToken(_expr);
        if (TrailingIsIdentifier(tail)) { _expr += "*" + d; return; }
        if (tail == "0" && char.IsAsciiDigit(d[0])) { _expr = _expr[..^1] + d; return; }

        _expr += d;
    }

    void AppendComma()
    {
        Unlock();
        if (_fresh) { _expr = "0"; _fresh = false; }

        string tail = TrailingToken(_expr);
        if (tail.Contains('.') || tail.Contains(',')) return;
        if (tail.Length == 0 || CanStartOperand(_expr[^1])) _expr += "0,";
        else _expr += ",";
    }

    void AppendOperator(string op)
    {
        Unlock();
        CloseAuto();

        if (_fresh)
        {
            _expr = _hasLast ? MathEngine.FormatPlain(_last) : "0";
            _fresh = false;
        }

        string t = _expr.TrimEnd();
        if (t.Length == 0) t = "0";

        foreach (string w in new[] { "AND", "OR", "XOR", "MOD", "NOT" })
        {
            string pat = " " + w;
            if (t.Length <= pat.Length) continue;
            if (!t.EndsWith(pat, StringComparison.Ordinal)) continue;
            t = t[..^pat.Length].TrimEnd();
            break;
        }

        string head = t.Length > 0 && CanStartOperand(t[^1]) ? t[..^1].TrimEnd() : t;
        if (head.Length == 0) head = "0";

        _expr = head + " " + op + " ";

        try
        {
            _pendingBase = MathEngine.Evaluate(head);
            _hasPending = true;
        }
        catch
        {
            _hasPending = false;
        }

        _pendingOp = op;
    }

    void ApplyPercent()
    {
        Unlock();
        CloseAuto();

        if (_fresh) { _expr = "0"; _fresh = false; }

        string t = _expr.TrimEnd();
        if (t.Length == 0) return;

        if (CanStartOperand(t[^1]) || !_hasPending || _pendingOp is null)
        {
            WrapLast("pct");
            return;
        }

        double current = CurrentOperandValue(t);
        double term = _pendingOp is "+" or "-"
            ? (current / 100.0) * _pendingBase
            : current / 100.0;

        _expr = $"{MathEngine.FormatPlain(_pendingBase)} {_pendingOp} {MathEngine.FormatPlain(term)}";
        _fresh = false;
        _hasPending = false;
        _pendingOp = null;
    }

    static double CurrentOperandValue(string expr)
    {
        string tail = TrailingToken(expr);
        if (tail.Length == 0) return 0;
        string n = tail.Replace(',', '.');
        return double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
    }

    void AppendRaw(string token)
    {
        Unlock();
        if (_fresh) { _expr = string.Empty; _fresh = false; }
        if (_expr.Length == 0 || CanStartOperand(_expr[^1])) _expr += token;
        else _expr += "*" + token;
    }

    void AppendFunction(string name)
    {
        Unlock();
        if (_fresh) { _expr = string.Empty; _fresh = false; }
        CloseAuto();
        _expr += name + "(";
        _autoClose++;
    }

    void WrapLast(string func)
    {
        Unlock();
        if (_fresh) { _expr = string.Empty; _fresh = false; }
        CloseAuto();

        string s = _expr.TrimEnd();
        if (s.Length == 0) { _expr = func + "("; _autoClose = 1; return; }

        if (s[^1] == ')')
        {
            int open = MatchOpen(s, s.Length - 1);
            if (open >= 0) { _expr = s[..open] + func + "(" + s[open..]; return; }
        }

        int i = s.Length;
        while (i > 0 && (char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '.' || s[i - 1] == ',')) i--;

        if (i == s.Length) { _expr = s + func + "("; _autoClose = 1; return; }

        _expr = s[..i] + func + "(" + s[i..] + ")";
    }

    void ApplyFactorial()
    {
        Unlock();
        CloseAuto();
        if (_expr.Length == 0) return;
        char last = _expr.TrimEnd()[^1];
        if (last == '!' || CanStartOperand(last)) return;
        _expr += "!";
        _fresh = false;
    }

    void AppendParen(string p)
    {
        Unlock();
        CloseAuto();

        if (p == "(")
        {
            if (_fresh) { _expr = string.Empty; _fresh = false; }
            char last = _expr.Length > 0 ? _expr[^1] : '\0';
            if (last == ')' || char.IsAsciiDigit(last) || last == '.') _expr += "*";
            _expr += "(";
            _autoClose++;
        }
        else if (_autoClose > 0)
        {
            _autoClose--;
            _expr += ")";
        }
        else if (_expr.TrimEnd().EndsWith(')'))
        {
            _expr = _expr.TrimEnd() + ")";
        }
    }

    void Backspace()
    {
        Unlock();
        CloseAuto();
        if (_fresh) { _expr = string.Empty; return; }

        string t = _expr.TrimEnd();
        if (t.Length == 0) { _expr = string.Empty; _fresh = true; return; }

        _expr = t[..^1].TrimEnd();
        if (_expr.Length == 0) _fresh = true;
    }

    void ClearAll()
    {
        _expr = string.Empty;
        _fresh = true;
        _autoClose = 0;
        _lockDisplay = false;
        _hasPending = false;
        _pendingOp = null;
    }

    // ------------------------------------------------------------------ calcul

    void DoEvaluate()
    {
        if (_expr.Trim().Length == 0) return;
        CloseAuto();

        string source = _expr.Trim();

        try
        {
            double v = MathEngine.Evaluate(source);
            _last = v;
            _hasLast = true;

            string pretty = MathEngine.ToPretty(source);
            string result = _mode == CalcMode.Programmer
                ? MathEngine.ToBase(v, _base)
                : MathEngine.Format(v);

            ExpressionLabel.Text = pretty + " =";
            SetResult(result);
            AddHistory($"{pretty} = {result}", source);

            _expr = MathEngine.FormatPlain(v);
            _fresh = true;
            _lockDisplay = true;
            _hasPending = false;
            _pendingOp = null;
            UpdateBasePanel();
        }
        catch (Exception ex)
        {
            ExpressionLabel.Text = MathEngine.ToPretty(source);
            SetResult(_mode == CalcMode.Programmer ? "ERREUR" : ex.Message);
        }
    }

    void AddHistory(string display, string expression)
    {
        _history.Insert(0, new HistoryItem
        {
            Display = display,
            Expression = expression,
            Time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
        });
        while (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
        UpdateHistoryCount();
    }

    void UpdateHistoryCount()
    {
        HistoryCountLabel.Text = _history.Count == 0
            ? "Aucun calcul enregistre"
            : $"{_history.Count} calcul{(_history.Count > 1 ? "s" : string.Empty)} " +
              $"enregistre{(_history.Count > 1 ? "s" : string.Empty)}";
    }

    // ------------------------------------------------------------------ affichage

    void SetResult(string text)
    {
        ResultLabel.Text = text;
        ResultLabel.FontSize = text.Length > 10 ? 18 : text.Length > 7 ? 26 : 42;
    }

    void Refresh()
    {
        if (_mode == CalcMode.Converter)
        {
            ExpressionLabel.Text = "Convertisseur d'unites";
            SetResult(ConverterResultLabel.Text);
            return;
        }

        if (_lockDisplay) { UpdateBasePanel(); return; }

        ExpressionLabel.Text = _expr.Length == 0 ? "0" : MathEngine.ToPretty(_expr);

        if (_expr.Trim().Length == 0)
        {
            SetResult("0");
        }
        else
        {
            try
            {
                double v = MathEngine.Evaluate(_expr);
                SetResult(_mode == CalcMode.Programmer
                    ? MathEngine.ToBase(v, _base)
                    : MathEngine.Format(v));
            }
            catch
            {
                SetResult(_hasLast
                    ? (_mode == CalcMode.Programmer ? MathEngine.ToBase(_last, _base) : MathEngine.Format(_last))
                    : "\u2026");
            }
        }

        UpdateBasePanel();
    }

    void UpdateBasePanel()
    {
        if (_mode != CalcMode.Programmer) return;

        bool ok = false;
        double v = _last;

        if (_lockDisplay || _expr.Trim().Length == 0)
        {
            ok = _hasLast;
        }
        else
        {
            try { v = MathEngine.Evaluate(_expr); ok = true; }
            catch { ok = _hasLast; v = _last; }
        }

        DecLabel.Text = ok ? "DEC  " + MathEngine.ToBase(v, 10) : "DEC  \u2014";
        HexLabel.Text = ok ? "HEX  " + MathEngine.ToBase(v, 16) : "HEX  \u2014";
        OctLabel.Text = ok ? "OCT  " + MathEngine.ToBase(v, 8) : "OCT  \u2014";
        BinLabel.Text = ok ? "BIN  " + MathEngine.ToBase(v, 2) : "BIN  \u2014";
    }

    void UpdateBaseSelection()
    {
        BaseDecBox.BackgroundColor = _base == 10 ? CAccent : CCardAlt;
        BaseHexBox.BackgroundColor = _base == 16 ? CAccent : CCardAlt;
        BaseOctBox.BackgroundColor = _base == 8 ? CAccent : CCardAlt;
        BaseBinBox.BackgroundColor = _base == 2 ? CAccent : CCardAlt;
    }

    void UpdateTabs()
    {
        SetTab(TabStandard, TabStandardText, _mode == CalcMode.Standard);
        SetTab(TabScientific, TabScientificText, _mode == CalcMode.Scientific);
        SetTab(TabProgrammer, TabProgrammerText, _mode == CalcMode.Programmer);
        SetTab(TabConverter, TabConverterText, _mode == CalcMode.Converter);
    }

    static void SetTab(Border box, Label label, bool selected)
    {
        box.BackgroundColor = selected ? CAccent : CCardAlt;
        label.TextColor = selected ? Colors.White : CTextMuted;
    }

    // ------------------------------------------------------------------ modes

    void ApplyMode()
    {
        MathEngine.AllowHexInput = _mode == CalcMode.Programmer;

        StandardPad.IsVisible = _mode == CalcMode.Standard;
        ScientificPad.IsVisible = _mode == CalcMode.Scientific;
        ProgrammerPad.IsVisible = _mode == CalcMode.Programmer;
        ConverterPanel.IsVisible = _mode == CalcMode.Converter;
        ProgrammerPanel.IsVisible = _mode == CalcMode.Programmer;

        ModeChip.Text = _mode switch
        {
            CalcMode.Standard => "Standard",
            CalcMode.Scientific => "Scientifique",
            CalcMode.Programmer => "Programmeur",
            _ => "Convertisseur"
        };

        AngleLabel.Text = _angle switch
        {
            AngleMode.Degrees => "DEG",
            AngleMode.Radians => "RAD",
            _ => "GRAD"
        };

        UpdateTabs();
        UpdateBaseSelection();

        if (_mode == CalcMode.Converter) ConverterCompute();
        else Refresh();
    }

    // ------------------------------------------------------------------ evenements clavier

    void OnKeyClicked(object? sender, EventArgs e)
    {
        if (_mode == CalcMode.Converter) return;
        if (sender is not Button b) return;

        string k = b.Text;
        if (string.IsNullOrEmpty(k)) return;

        switch (k)
        {
            case "AC":
                ClearAll();
                break;
            case "RET":
                Backspace();
                break;
            case "=":
                DoEvaluate();
                return;

            case "\u00F7":
                AppendOperator("/");
                break;
            case "\u00D7":
                AppendOperator("*");
                break;
            case "\u2212":
                AppendOperator("-");
                break;
            case "+":
                AppendOperator("+");
                break;
            case "x\u02C6":
                AppendOperator("^");
                break;

            case "%":
                ApplyPercent();
                break;
            case ",":
                AppendComma();
                break;
            case "\u00B1":
                WrapLast("neg");
                break;

            case "(":
                AppendParen("(");
                break;
            case ")":
                AppendParen(")");
                break;

            case "x\u00B2":
                WrapLast("sqr");
                break;
            case "x\u00B3":
                WrapLast("cube");
                break;
            case "1/x":
                WrapLast("inv");
                break;
            case "\u221A":
                WrapLast("sqrt");
                break;
            case "NOT":
                WrapLast("not");
                break;
            case "x!":
                ApplyFactorial();
                break;

            case "sin":
                AppendFunction("sin");
                break;
            case "cos":
                AppendFunction("cos");
                break;
            case "tan":
                AppendFunction("tan");
                break;
            case "ln":
                AppendFunction("ln");
                break;
            case "log":
                AppendFunction("log");
                break;
            case "log\u2082":
                AppendFunction("log2");
                break;

            case "\u03C0":
                AppendRaw("pi");
                break;
            case "e":
                AppendRaw("e");
                break;
            case "Ans":
                if (_hasLast) AppendRaw(MathEngine.FormatPlain(_last));
                else ClearAll();
                break;

            case "AND":
                AppendOperator("AND");
                break;
            case "OR":
                AppendOperator("OR");
                break;
            case "XOR":
                AppendOperator("XOR");
                break;
            case "MOD":
            case "mod":
                AppendOperator("MOD");
                break;
            case "<<":
                AppendOperator("<<");
                break;
            case ">>":
                AppendOperator(">>");
                break;

            default:
                if (IsDigit(k)) AppendDigit(k);
                else if (_mode == CalcMode.Programmer && k.Length == 1 && IsHexChar(k[0])) AppendDigit(k);
                else return;
                break;
        }

        Refresh();
    }

    // ------------------------------------------------------------------ en-tete

    void OnAngleTapped(object? sender, EventArgs e)
    {
        _angle = _angle switch
        {
            AngleMode.Degrees => AngleMode.Radians,
            AngleMode.Radians => AngleMode.Gradians,
            _ => AngleMode.Degrees
        };

        MathEngine.Angle = _angle;
        ApplyMode();
    }

    void OnModeTapped(object? sender, EventArgs e)
    {
        if (e is not TappedEventArgs te) return;
        _mode = te.Parameter switch
        {
            "1" => CalcMode.Scientific,
            "2" => CalcMode.Programmer,
            "3" => CalcMode.Converter,
            _ => CalcMode.Standard
        };
        ApplyMode();
    }

    void OnBaseTapped(object? sender, EventArgs e)
    {
        if (e is not TappedEventArgs te) return;
        if (!int.TryParse(te.Parameter?.ToString(), out int b)) return;

        _base = b;
        UpdateBaseSelection();

        if (_lockDisplay && _hasLast) SetResult(MathEngine.ToBase(_last, _base));
        else Refresh();
    }

    // ------------------------------------------------------------------ memoire

    void OnMemStore(object? sender, EventArgs e)
    {
        if (TryValue(out double v)) _memory = v;
    }

    void OnMemRecall(object? sender, EventArgs e)
    {
        AppendRaw(MathEngine.FormatPlain(_memory));
        Refresh();
    }

    void OnMemAdd(object? sender, EventArgs e)
    {
        if (TryValue(out double v)) _memory += v;
    }

    void OnMemSub(object? sender, EventArgs e)
    {
        if (TryValue(out double v)) _memory -= v;
    }

    void OnMemClear(object? sender, EventArgs e) => _memory = 0;

    bool TryValue(out double value)
    {
        value = 0;
        string s = (_lockDisplay || _expr.Trim().Length == 0) ? MathEngine.FormatPlain(_last) : _expr;
        if (s.Trim().Length == 0) return false;
        try { value = MathEngine.Evaluate(s); return true; }
        catch { return false; }
    }

    // ------------------------------------------------------------------ historique

    void OnHistoryClicked(object? sender, EventArgs e) => HistoryOverlay.IsVisible = true;

    void OnCloseHistory(object? sender, EventArgs e) => HistoryOverlay.IsVisible = false;

    void OnClearHistory(object? sender, EventArgs e)
    {
        _history.Clear();
        UpdateHistoryCount();
    }

    void OnHistoryItemTapped(object? sender, EventArgs e)
    {
        if (sender is not Label l) return;
        if (l.BindingContext is not HistoryItem item) return;

        _expr = item.Expression;
        _fresh = true;
        _lockDisplay = false;
        _autoClose = 0;
        HistoryOverlay.IsVisible = false;
        Refresh();
    }

    // ------------------------------------------------------------------ convertisseur

    void ConverterInit()
    {
        _busy = true;
        CategoryPicker.ItemsSource = UnitConverter.Categories.Select(c => c.Name).ToList();
        CategoryPicker.SelectedIndex = 0;
        _busy = false;

        OnCategoryChanged(null, EventArgs.Empty);
    }

    void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_busy) return;
        int i = CategoryPicker.SelectedIndex;
        if (i < 0 || i >= UnitConverter.Categories.Length) return;

        _category = UnitConverter.Categories[i];

        _busy = true;
        FromPicker.ItemsSource = _category.Units.Select(u => u.Label).ToList();
        ToPicker.ItemsSource = _category.Units.Select(u => u.Label).ToList();
        FromPicker.SelectedIndex = Math.Min(2, _category.Units.Length - 1);
        ToPicker.SelectedIndex = 0;
        _busy = false;

        ConverterCompute();
    }

    void OnUnitChanged(object? sender, EventArgs e)
    {
        if (_busy) return;
        ConverterCompute();
    }

    void OnSwapUnits(object? sender, EventArgs e)
    {
        int a = FromPicker.SelectedIndex;
        int b = ToPicker.SelectedIndex;
        FromPicker.SelectedIndex = b;
        ToPicker.SelectedIndex = a;
        ConverterCompute();
    }

    void OnValueChanged(object? sender, TextChangedEventArgs e) => ConverterCompute();

    void ConverterCompute()
    {
        if (!ConverterPanel.IsVisible) return;

        int fi = FromPicker.SelectedIndex;
        int ti = ToPicker.SelectedIndex;
        if (fi < 0 || ti < 0) return;
        if (fi >= _category.Units.Length || ti >= _category.Units.Length) return;

        string raw = (ValueEntry.Text ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            ConverterResultLabel.Text = "\u2014";
            ConverterResultSubLabel.Text = "Valeur invalide";
            ConverterTableLabel.Text = string.Empty;
            return;
        }

        UnitDefinition from = _category.Units[fi];
        UnitDefinition to = _category.Units[ti];

        double converted = _category.Convert(value, from, to);

        ConverterResultLabel.Text = $"{MathEngine.Format(converted)} {to.Symbol}";
        ConverterResultSubLabel.Text = $"{MathEngine.Format(value)} {from.Symbol} = " +
                                        $"{MathEngine.Format(converted)} {to.Symbol}";

        var lines = new List<string> { $"1 {from.Symbol} vaut :" };
        foreach (UnitDefinition u in _category.Units)
        {
            if (u.Symbol == from.Symbol) continue;
            lines.Add($"    {MathEngine.Format(_category.Convert(value, from, u))} {u.Symbol}");
        }
        ConverterTableLabel.Text = string.Join('\n', lines);

        if (_mode == CalcMode.Converter) Refresh();
    }
}
