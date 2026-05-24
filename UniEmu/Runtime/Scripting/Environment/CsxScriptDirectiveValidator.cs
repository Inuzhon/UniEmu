using System.Text.RegularExpressions;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Runtime.Scripting.Environment;

/// <summary>
/// Проверяет директивы CSX-скриптов: разрешает <c>#load</c>, запрещает неподдерживаемые директивы и ищет циклы загрузки.
/// </summary>
public sealed class CsxScriptDirectiveValidator(CsxLoadedScriptExpander expander)
{
    /// <summary>
    /// Регулярное выражение для директив, запрещенных в пользовательских скриптах.
    /// </summary>
    private static readonly Regex s_blockedDirective = new(
        @"^\s*#\s*(r|using|line|pragma|nullable)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Создает валидатор директив с экспандером загружаемых скриптов по умолчанию.
    /// </summary>
    public CsxScriptDirectiveValidator()
        : this(new CsxLoadedScriptExpander())
    {
    }

    /// <summary>
    /// Проверяет содержимое одного скрипта и выбрасывает исключение при неподдерживаемой директиве.
    /// </summary>
    /// <param name="content">Текст CSX-скрипта.</param>
    public void ValidateSupportedDirectives(string content)
    {
        ValidateSupportedDirectives("script.csx", content);
    }

    /// <summary>
    /// Проверяет содержимое одного скрипта и выбрасывает диагностическое исключение при неподдерживаемой директиве.
    /// </summary>
    /// <param name="path">Путь CSX-скрипта.</param>
    /// <param name="content">Текст CSX-скрипта.</param>
    public void ValidateSupportedDirectives(string path, string content)
    {
        var match = s_blockedDirective.Match(content);
        if (match.Success)
        {
            throw new CsxScriptValidationException(
                [CreateUnsupportedDirectiveDiagnostic(TagScriptPath.Normalize(path), content, match)]);
        }
    }

    /// <summary>
    /// Возвращает диагностики неподдерживаемых директив во входном скрипте и всех доступных загруженных скриптах.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="content">Текст входного CSX-файла.</param>
    /// <param name="scripts">Словарь доступных скриптов по нормализованному пути.</param>
    /// <returns>Список диагностик неподдерживаемых директив.</returns>
    public IReadOnlyList<CsxDiagnostic> GetUnsupportedDirectiveDiagnostics(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> scripts)
    {
        var diagnostics = new List<CsxDiagnostic>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        VisitForDiagnostics(TagScriptPath.Normalize(entryPath), content, visited, scripts, diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Проверяет граф <c>#load</c>-зависимостей и выбрасывает исключение при циклической загрузке.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла.</param>
    /// <param name="scripts">Словарь доступных скриптов по нормализованному пути.</param>
    public void DetectLoadCycles(string entryPath, IReadOnlyDictionary<string, string> scripts)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Visit(TagScriptPath.Normalize(entryPath), visited, stack, scripts);
    }

    /// <summary>
    /// Обходит граф <c>#load</c>-зависимостей в глубину и отслеживает текущий стек посещения.
    /// </summary>
    /// <param name="path">Текущий нормализованный путь скрипта.</param>
    /// <param name="visited">Уже проверенные скрипты.</param>
    /// <param name="stack">Стек текущего DFS-обхода.</param>
    /// <param name="scripts">Словарь доступных скриптов.</param>
    private void Visit(
        string path,
        HashSet<string> visited,
        HashSet<string> stack,
        IReadOnlyDictionary<string, string> scripts)
    {
        if (stack.Contains(path))
        {
            throw new InvalidOperationException($"Cyclic #load detected at '{path}'.");
        }

        if (!visited.Add(path) || !scripts.TryGetValue(path, out var content))
        {
            return;
        }

        stack.Add(path);
        foreach (var loadPathValue in expander.GetLoadDirectivePaths(content))
        {
            var loadPath = expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is not null)
            {
                Visit(loadPath, visited, stack, scripts);
            }
        }

        stack.Remove(path);
    }

    /// <summary>
    /// Обходит входной и загруженные скрипты, собирая диагностики неподдерживаемых директив.
    /// </summary>
    /// <param name="path">Текущий нормализованный путь скрипта.</param>
    /// <param name="content">Текст текущего скрипта.</param>
    /// <param name="visited">Уже проверенные скрипты.</param>
    /// <param name="scripts">Словарь доступных скриптов.</param>
    /// <param name="diagnostics">Накопитель диагностик.</param>
    private void VisitForDiagnostics(
        string path,
        string content,
        HashSet<string> visited,
        IReadOnlyDictionary<string, string> scripts,
        List<CsxDiagnostic> diagnostics)
    {
        if (!visited.Add(path))
        {
            return;
        }

        foreach (Match match in s_blockedDirective.Matches(content))
        {
            diagnostics.Add(CreateUnsupportedDirectiveDiagnostic(path, content, match));
        }

        foreach (var loadPathValue in expander.GetLoadDirectivePaths(content))
        {
            var loadPath = expander.ResolveLoadPath(loadPathValue, path, scripts);
            if (loadPath is not null && scripts.TryGetValue(loadPath, out var loadedContent))
            {
                VisitForDiagnostics(loadPath, loadedContent, visited, scripts, diagnostics);
            }
        }
    }

    /// <summary>
    /// Создает диагностическое сообщение для найденной неподдерживаемой директивы.
    /// </summary>
    /// <param name="content">Текст скрипта, в котором найдена директива.</param>
    /// <param name="match">Совпадение регулярного выражения директивы.</param>
    /// <returns>Диагностика с позицией директивы в документе.</returns>
    /// <param name="path">Нормализованный путь к скрипту, которому принадлежит директива.</param>
    private static CsxDiagnostic CreateUnsupportedDirectiveDiagnostic(string path, string content, Match match)
    {
        var start = GetLinePosition(content, match.Index);
        var end = GetLinePosition(content, match.Index + match.Length);
        return new CsxDiagnostic(
            "CSX001",
            $"Unsupported script directive '{match.Value.Trim()}'. Use #load for shared scripts.",
            CsxDiagnosticSeverity.Error,
            start.Line,
            start.Character,
            end.Line,
            end.Character,
            path);
    }

    /// <summary>
    /// Преобразует абсолютный offset в позицию строки и колонки.
    /// </summary>
    /// <param name="content">Текст документа.</param>
    /// <param name="offset">Абсолютная позиция в тексте.</param>
    /// <returns>Номер строки и символа в формате Roslyn/LSP.</returns>
    private static (int Line, int Character) GetLinePosition(string content, int offset)
    {
        var line = 0;
        var lineStart = 0;
        var safeOffset = Math.Clamp(offset, 0, content.Length);

        for (var i = 0; i < safeOffset; i++)
        {
            if (content[i] != '\n')
            {
                continue;
            }

            line++;
            lineStart = i + 1;
        }

        return (line, safeOffset - lineStart);
    }
}
