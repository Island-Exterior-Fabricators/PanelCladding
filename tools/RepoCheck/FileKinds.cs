namespace RepoCheck;

public enum FileKind
{
    CSharp,
    PowerShell,
    Xml,
    Markdown,
    PlainText,
    Yaml,
    Shell,
    HashLines,
    Batch,
    Solution,
    Json,
    Binary,
    Unknown,
}

public static class FileKinds
{
    private static readonly Dictionary<string, FileKind> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = FileKind.CSharp,
        [".ps1"] = FileKind.PowerShell,
        [".psm1"] = FileKind.PowerShell,
        [".psd1"] = FileKind.PowerShell,
        [".csproj"] = FileKind.Xml,
        [".props"] = FileKind.Xml,
        [".targets"] = FileKind.Xml,
        [".xaml"] = FileKind.Xml,
        [".config"] = FileKind.Xml,
        [".resx"] = FileKind.Xml,
        [".xml"] = FileKind.Xml,
        [".md"] = FileKind.Markdown,
        [".txt"] = FileKind.PlainText,
        [".yml"] = FileKind.Yaml,
        [".yaml"] = FileKind.Yaml,
        [".gitignore"] = FileKind.HashLines,
        [".gitattributes"] = FileKind.HashLines,
        [".cmd"] = FileKind.Batch,
        [".bat"] = FileKind.Batch,
        [".sln"] = FileKind.Solution,
        [".json"] = FileKind.Json,
        [".zip"] = FileKind.Binary,
        [".png"] = FileKind.Binary,
        [".jpg"] = FileKind.Binary,
        [".jpeg"] = FileKind.Binary,
        [".gif"] = FileKind.Binary,
        [".ico"] = FileKind.Binary,
        [".dll"] = FileKind.Binary,
        [".exe"] = FileKind.Binary,
        [".rhp"] = FileKind.Binary,
    };

    public static FileKind Classify(string path, string text)
    {
        string extension = Path.GetExtension(path);
        if (extension.Length > 0)
            return ByExtension.TryGetValue(extension, out FileKind kind) ? kind : FileKind.Unknown;
        return text.StartsWith("#!/bin/sh", StringComparison.Ordinal) || text.StartsWith("#!/usr/bin/env sh", StringComparison.Ordinal) ||
               text.StartsWith("#!/bin/bash", StringComparison.Ordinal) || text.StartsWith("#!/usr/bin/env bash", StringComparison.Ordinal)
            ? FileKind.Shell
            : FileKind.Unknown;
    }

    public static bool IsProse(FileKind kind) => kind is FileKind.Markdown or FileKind.PlainText;
}
