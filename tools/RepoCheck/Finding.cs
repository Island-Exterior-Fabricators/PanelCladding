namespace RepoCheck;

public sealed record Finding(string Path, int Line, string Rule, string Detail)
{
    public override string ToString() => $"{Path}:{Line}: {Rule}: {Detail}";
}
