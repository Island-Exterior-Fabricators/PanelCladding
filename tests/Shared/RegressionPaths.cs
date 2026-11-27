#nullable enable

internal static class RegressionPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string Output(string name)
    {
        string path = System.IO.Path.Combine(RepositoryRoot, "artifacts", "tests", name);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        System.IO.DirectoryInfo? directory = new(System.AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "AGENTS.md")) &&
                System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new System.IO.DirectoryNotFoundException("Could not locate the PanelCladding repository root.");
    }
}
