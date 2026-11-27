using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RepoCheck;

public static class CommentRules
{
    public const string Rule = "comment";

    private static readonly HashSet<SyntaxKind> CSharpCommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    private static readonly Regex QuotedText = new("'[^']*'|\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
    private static readonly Regex HashComment = new(@"(?:^|\s)#", RegexOptions.Compiled);
    private static readonly Regex BatchComment = new(@"^\s*@?(?:rem(?:\s|$)|::)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Fence = new(@"^\s*(```|~~~)", RegexOptions.Compiled);

    public static IEnumerable<Finding> Find(string path, string text, FileKind kind) => kind switch
    {
        FileKind.CSharp => CSharp(path, text),
        FileKind.Xml => Xml(path, text),
        FileKind.Markdown => Markdown(path, text),
        FileKind.Yaml => HashCommentLines(path, text, skipShebang: false),
        FileKind.Shell => HashCommentLines(path, text, skipShebang: true),
        FileKind.HashLines or FileKind.Solution => LinesStartingWithHash(path, text),
        FileKind.Batch => Lines(text).Where(line => BatchComment.IsMatch(line.Text)).Select(line => At(path, line.Number, line.Text)),
        FileKind.Json => Json(path, text),
        FileKind.PlainText or FileKind.Binary or FileKind.PowerShell => [],
        _ => [new Finding(path, 1, Rule, "no comment rule for this file type; add one in tools/RepoCheck/FileKinds.cs and CommentRules.cs")],
    };

    public static IEnumerable<Finding> CSharp(string path, string text) =>
        CSharpSyntaxTree.ParseText(text, path: path).GetRoot()
            .DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => CSharpCommentKinds.Contains(trivia.Kind()))
            .Select(trivia => At(path, trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1, trivia.ToString()));

    public static IEnumerable<Finding> Xml(string path, string text)
    {
        var findings = new List<Finding>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var info = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Comment)
                    findings.Add(At(path, info.LineNumber, "<!--" + reader.Value + "-->"));
            }
        }
        catch (XmlException ex)
        {
            findings.Add(new Finding(path, ex.LineNumber, Rule, "the file is not well-formed XML, so its comments cannot be checked: " + ex.Message));
        }
        return findings;
    }

    public static IEnumerable<Finding> Markdown(string path, string text)
    {
        bool inFence = false;
        foreach ((int number, string line) in Lines(text))
        {
            if (Fence.IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }
            if (!inFence && Regex.Replace(line, "`[^`]*`", string.Empty).Contains("<!--", StringComparison.Ordinal))
                yield return At(path, number, line);
        }
    }

    public static IEnumerable<Finding> HashCommentLines(string path, string text, bool skipShebang) =>
        Lines(text)
            .Where(line => !(skipShebang && line.Number == 1 && line.Text.StartsWith("#!", StringComparison.Ordinal)))
            .Where(line => HashComment.IsMatch(QuotedText.Replace(line.Text, "''")))
            .Select(line => At(path, line.Number, line.Text));

    public static IEnumerable<Finding> LinesStartingWithHash(string path, string text) =>
        Lines(text).Where(line => line.Text.TrimStart().StartsWith('#')).Select(line => At(path, line.Number, line.Text));

    public static IEnumerable<Finding> Json(string path, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text.TrimStart('﻿'));
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var findings = new List<Finding>();
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.Comment)
                    findings.Add(At(path, LineOf(bytes, reader.TokenStartIndex), reader.GetComment()));
            }
        }
        catch (JsonException ex)
        {
            findings.Add(new Finding(path, (int)(ex.LineNumber ?? 0) + 1, Rule, "the file is not valid JSON, so its comments cannot be checked: " + ex.Message));
        }
        return findings;
    }

    public static IReadOnlyList<Finding> PowerShell(IReadOnlyList<(string Path, string FullPath)> files)
    {
        if (files.Count == 0) return [];
        const string script = """
            $ErrorActionPreference = 'Stop'
            $paths = [Console]::In.ReadToEnd() -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
            foreach ($path in $paths) {
                $tokens = $null
                $errors = $null
                [void][System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
                foreach ($token in $tokens) {
                    if ($token.Kind -eq 'Comment') { '{0}|{1}|{2}' -f $path, $token.Extent.StartLineNumber, ($token.Text -split "`n")[0].Trim() }
                }
            }
            """;
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes("[Console]::OutputEncoding = [Text.Encoding]::UTF8\n" + script)) })
            info.ArgumentList.Add(argument);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("powershell.exe could not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(string.Join("\n", files.Select(file => file.FullPath)));
        process.StandardInput.Close();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("The PowerShell comment scan failed: " + error.Result);
        Dictionary<string, string> relative = files.ToDictionary(file => file.FullPath, file => file.Path, StringComparer.OrdinalIgnoreCase);
        var findings = new List<Finding>();
        foreach (string line in output.Result.Split('\n').Select(item => item.TrimEnd('\r')).Where(item => item.Length > 0))
        {
            string[] parts = line.Split('|', 3);
            findings.Add(At(relative.TryGetValue(parts[0], out string? name) ? name : parts[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), parts[2]));
        }
        return findings;
    }

    internal static IEnumerable<(int Number, string Text)> Lines(string text) =>
        text.Split('\n').Select((line, index) => (index + 1, line.TrimEnd('\r')));

    private static int LineOf(byte[] bytes, long index)
    {
        int line = 1;
        for (long position = 0; position < index && position < bytes.Length; position++)
        {
            if (bytes[position] == (byte)'\n') line++;
        }
        return line;
    }

    private static Finding At(string path, int line, string comment)
    {
        string first = comment.Split('\n')[0].Trim();
        return new Finding(path, line, Rule, first.Length > 100 ? first[..100] + "..." : first);
    }
}
