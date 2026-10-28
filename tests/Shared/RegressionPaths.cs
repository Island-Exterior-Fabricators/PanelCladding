#nullable enable

// Compiled into every smoke project by tests/Smoke/Directory.Build.props.
internal static class RegressionPaths
{
    // Located from the build output, so smokes do not depend on the working directory.
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    // Generated renders and summaries go here, never into a tracked folder.
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
