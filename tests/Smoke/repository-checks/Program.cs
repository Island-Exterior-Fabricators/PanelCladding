using System.Diagnostics;
using RepoCheck;

namespace RepositoryChecksSmoke;

internal static class Program
{
    private static int Main()
    {
        try
        {
            VerifyCSharpComments();
            VerifyOtherComments();
            VerifyPowerShellComments();
            VerifyProse();
            VerifyTiming();
            VerifyWholeRepository();
            Console.WriteLine("PASS: repository checks");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Repository checks smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyCSharpComments()
    {
        string source = string.Join("\n",
            "namespace X;",
            "/" + "// <summary>Doc.</summary>",
            "public static class Y",
            "{",
            "    /" + "/ line",
            "    public static string Z = \"http://example.invalid // not a comment\";",
            "    public static int W = 1; /" + "* block *" + "/",
            "}");
        Require(Lines(CommentRules.Find("x.cs", source, FileKind.CSharp)).SequenceEqual([2, 5, 7]),
            "C#: line, block and documentation comments are found; text inside strings is not.");
        Require(!CommentRules.Find("x.cs", "class A { string s = \"/" + "* x *" + "/\"; }", FileKind.CSharp).Any(),
            "C#: a string that looks like a block comment is not one.");
        Console.WriteLine("[OK] C# comments come from the Roslyn syntax tree");
    }

    private static void VerifyOtherComments()
    {
        Expect("a.csproj", FileKind.Xml, "<Project>\n  <!" + "-- note -->\n  <A>&lt;!--</A>\n</Project>", [2]);
        Expect("a.xaml", FileKind.Xml, "<Grid>\n  <TextBlock Text=\"#1\" />\n</Grid>", []);
        Expect("a.yml", FileKind.Yaml, "name: x\n" + "# note\n" + "key: 'a # b'\n" + "url: http://x/#y\n" + "run: echo 1 " + "# trailing", [2, 5]);
        Expect("pre-push", FileKind.Shell, "#!/bin/sh\n" + "# note\n" + "echo \"#1\"\n" + "exec x", [2]);
        Expect(".gitignore", FileKind.HashLines, "bin/\n" + "# note\n" + "obj/", [2]);
        Expect("a.sln", FileKind.Solution, "Microsoft Visual Studio Solution File\n" + "# Visual Studio Version 17", [2]);
        Expect("a.cmd", FileKind.Batch, "@echo off\nREM note\n:: note\n@rem note\necho remark\n:label", [2, 3, 4]);
        Expect("a.json", FileKind.Json, "{\n  \"a\": \"// no\",\n  /" + "/ yes\n  \"b\": 1\n}", [3]);
        Expect("a.md", FileKind.Markdown, "# T\n\n<!" + "-- note -->\n\n`<!" + "--` in code\n\n```html\n<!" + "-- sample -->\n```", [3]);
        Expect("notes.txt", FileKind.PlainText, "# not a comment in plain text", []);
        Require(FileKinds.Classify("pre-push", "#!/bin/sh\nexit 0") == FileKind.Shell, "A sh script without an extension is a shell file.");
        Require(FileKinds.Classify("script.py", "print(1)") == FileKind.Unknown &&
                CommentRules.Find("script.py", "print(1)", FileKind.Unknown).Single().Detail.Contains("no comment rule", StringComparison.Ordinal),
            "A file type without a comment rule is reported, so the list of file types stays closed.");
        Require(FileKinds.Classify("Design/a.zip", "") == FileKind.Binary, "Archives are binary.");
        Console.WriteLine("[OK] XML, YAML, sh, git, solution, batch, JSON and Markdown comments; unknown file types are reported");
    }

    private static void VerifyPowerShellComments()
    {
        string folder = Path.Combine(RegressionPaths.Output("repository-checks"), "powershell-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        string commented = Path.Combine(folder, "commented.ps1");
        string clean = Path.Combine(folder, "clean.ps1");
        File.WriteAllText(commented, string.Join("\n",
            "<" + "#",
            ".SYNOPSIS",
            "#" + ">",
            "$a = 1 " + "# trailing",
            "Write-Output 'done'"));
        File.WriteAllText(clean, string.Join("\n",
            "$text = 'a # b'",
            "$other = \"#!/bin/sh\"",
            "$here = @'",
            "# not a comment",
            "'@",
            "Write-Output $text$other$here"));
        IReadOnlyList<Finding> findings = CommentRules.PowerShell([("commented.ps1", commented), ("clean.ps1", clean)]);
        Require(findings.All(finding => finding.Path == "commented.ps1") && Lines(findings).SequenceEqual([1, 4]),
            "PowerShell: block and trailing comments are found by the PowerShell parser; strings and here-strings are not comments.\n" +
            string.Join("\n", findings));
        Console.WriteLine("[OK] PowerShell comments come from the PowerShell parser");
    }

    private static void VerifyProse()
    {
        ExpectProse("a.md", FileKind.Markdown, "One sentence. Another one.", [(1, ProseRules.TwoSentencesRule)]);
        ExpectProse("a.md", FileKind.Markdown, "Use e.g. this one. It works.", [(1, ProseRules.TwoSentencesRule)]);
        ExpectProse("a.md", FileKind.Markdown, "Use e.g. this one, i.e. that one.\nRun `a. B` and see [x](a.md). Done", [(2, ProseRules.TwoSentencesRule)]);
        ExpectProse("a.md", FileKind.Markdown, "A sentence that is\nwrapped onto two lines.", [(1, ProseRules.WrappedRule)]);
        ExpectProse("a.md", FileKind.Markdown, "- a list item that is\n  wrapped onto two lines.\n- next item", [(1, ProseRules.WrappedRule)]);
        ExpectProse("a.md", FileKind.Markdown, "- `docs/` - design\n- `tools/` - tools\n\nIntro:\n\n1. Step one.\n   Its second sentence.", []);
        ExpectProse("a.md", FileKind.Markdown, "# Heading. With dots\n\n| a. B | c |\n| --- | --- |\n\n```text\nOne. Two\nthree\n```", []);
        ExpectProse("a.txt", FileKind.PlainText, "INSTALL\n1. Download it.\nNo wrap here,\nbut here.", [(3, ProseRules.WrappedRule)]);
        ExpectProse("a.txt", FileKind.PlainText, "Run these:\n    tool -Mode Repair\n    tool -Mode Validate\nThen close it.", []);
        Require(ProseRules.SentenceBreaks("First one. Second `x. Y` one. Third.").Count == 2, "Sentence breaks skip inline code.");
        Console.WriteLine("[OK] prose: two sentences on a line and wrapped sentences are found; headings, tables and code are not prose");
    }

    private static void VerifyTiming()
    {
        Require(TimingRules.Applies("tests/Run.ps1", FileKind.PowerShell) && !TimingRules.Applies("tests/Live/Watch.ps1", FileKind.PowerShell) &&
                !TimingRules.Applies("tools/check.ps1", FileKind.PowerShell) && !TimingRules.Applies("tests/a.cs", FileKind.CSharp),
            "The timing rule covers test PowerShell scripts outside the listed exclusions; C# tests are covered by tests/BannedSymbols.txt.");
        string script = string.Join("\n",
            "Start-Sleep -Seconds 1",
            "$process.WaitForExit()",
            "$process.WaitForExit(1000)",
            "$event.WaitOne()",
            "$event.WaitOne($limit)",
            "Wait-Process -Id 1 -Timeout 5");
        Require(Lines(TimingRules.Find("tests/a.ps1", script)).SequenceEqual([1, 3, 5, 6]),
            "Sleeps and waits with a time limit are found; waits without one are not.");
        Console.WriteLine("[OK] timing: test scripts neither sleep nor wait with a time limit");
    }

    private static void VerifyWholeRepository()
    {
        string root = Path.Combine(RegressionPaths.Output("repository-checks"), "repo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        Require(Run(root, "git", "init", "-q").Code == 0, "git init failed.");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "ignored/\n");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Readme\n\nOne sentence per line.\nAnother one.\n");
        File.WriteAllText(Path.Combine(root, "A.cs"), "namespace A;\n\npublic static class B\n{\n}\n");
        File.WriteAllText(Path.Combine(root, "tests", "Run.ps1"), "Write-Output 'ok'\n");
        Directory.CreateDirectory(Path.Combine(root, "ignored"));
        File.WriteAllText(Path.Combine(root, "ignored", "Skipped.cs"), "/" + "/ ignored by git\n");
        (int cleanCode, string cleanOutput) = RunCheck(root);
        Require(cleanCode == 0, "A clean repository passes:\n" + cleanOutput);

        File.WriteAllText(Path.Combine(root, "A.cs"), "namespace A;\n\n/" + "/ why\npublic static class B\n{\n}\n");
        File.WriteAllText(Path.Combine(root, "README.md"), "# Readme\n\nOne sentence. Two sentences.\n");
        File.WriteAllText(Path.Combine(root, "tests", "Run.ps1"), "Start-Sleep -Seconds 1 " + "# wait\n");
        (int code, string output) = RunCheck(root);
        Require(code == 1 &&
                output.Contains("A.cs:3: comment: /" + "/ why", StringComparison.Ordinal) &&
                output.Contains("README.md:3: two sentences on one line", StringComparison.Ordinal) &&
                output.Contains("tests/Run.ps1:1: comment:", StringComparison.Ordinal) &&
                output.Contains("tests/Run.ps1:1: test waits for a duration", StringComparison.Ordinal) &&
                !output.Contains("Skipped.cs", StringComparison.Ordinal),
            "Findings name the file and line and the check exits 1; git-ignored files are not scanned:\n" + output);
        Console.WriteLine("[OK] whole repository: git-listed files only, findings as file:line, exit 1 on any finding");
    }

    private static (int Code, string Output) RunCheck(string root)
    {
        TextWriter original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            int code = RepoCheck.Program.Main([root]);
            return (code, captured.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static void Expect(string path, FileKind kind, string text, int[] lines) =>
        Require(Lines(CommentRules.Find(path, text, kind)).SequenceEqual(lines),
            $"{kind} comments in {path}: expected lines [{string.Join(", ", lines)}], found [{string.Join(", ", Lines(CommentRules.Find(path, text, kind)))}].");

    private static void ExpectProse(string path, FileKind kind, string text, (int Line, string Rule)[] expected)
    {
        (int Line, string Rule)[] found = ProseRules.Find(path, text, kind).Select(finding => (finding.Line, finding.Rule)).ToArray();
        Require(found.SequenceEqual(expected),
            $"Prose of {text.Replace("\n", "\\n")}: expected [{string.Join(", ", expected)}], found [{string.Join(", ", found)}].");
    }

    private static IEnumerable<int> Lines(IEnumerable<Finding> findings) => findings.Select(finding => finding.Line);

    private static (int Code, string Output) Run(string directory, string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException(file + " could not start.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
