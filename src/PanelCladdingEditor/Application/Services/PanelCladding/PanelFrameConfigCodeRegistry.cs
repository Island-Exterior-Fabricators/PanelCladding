using System.Text.Json;
using PanelCladdingEditor.Contracts.Responses;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Frame configuration codes (CW_1.08_FRAME_CONFIG) in the project's Frame-Config token
// (PanelNamingFormat, AA when none is recorded). AA and A1 codes are grouped: configurations
// with the same horizontal/vertical intermediate counts share the first letter and the
// second character distinguishes variants. A, 01 and 001 codes take the next free code.
// Letters I and O are not used. Codes are never reassigned.
public sealed class PanelFrameConfigCodeRegistry(string token = "AA")
{
    public const string Alphabet = PanelNamingFormat.Letters;
    private readonly Dictionary<string, string> _configByCode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _codeByConfig = new(StringComparer.Ordinal);

    public string Token { get; } = PanelNamingFormat.IsToken(token) ? token : "AA";

    public IReadOnlyDictionary<string, string> Entries => _configByCode;

    public bool IsCode(string? value) => PanelNamingFormat.Fits(Token, value);

    public bool TryGetConfig(string code, out string config) => _configByCode.TryGetValue(code, out config!);

    public bool TryGetCode(string config, out string code) => _codeByConfig.TryGetValue(config, out code!);

    // Registers an existing pair. Conflicting pairs are rejected so stored codes never move.
    public OperationResponse Register(string code, string config)
    {
        if (!IsCode(code) || !PanelFrameConfigFormat.IsFormatted(config))
        {
            return OperationResponse.Fail($"PANEL_FRAME_CONFIG_CODE_INVALID: {code}");
        }
        if (_configByCode.TryGetValue(code, out string? known))
        {
            return string.Equals(known, config, StringComparison.Ordinal)
                ? OperationResponse.Ok()
                : OperationResponse.Fail($"PANEL_FRAME_CONFIG_CODE_CONFLICT: {code} is already '{known}'");
        }
        if (_codeByConfig.TryGetValue(config, out string? existing))
        {
            return OperationResponse.Fail($"PANEL_FRAME_CONFIG_CODE_CONFLICT: '{config}' is already {existing}");
        }
        _configByCode[code] = config;
        _codeByConfig[config] = code;
        return OperationResponse.Ok();
    }

    public OperationResponse<string> Resolve(string config, out bool assigned)
    {
        assigned = false;
        if (_codeByConfig.TryGetValue(config, out string? known))
        {
            return OperationResponse<string>.Ok(known);
        }
        if (!PanelFrameConfigFormat.TryReadCounts(config, out int horizontal, out int vertical))
        {
            return OperationResponse<string>.Fail($"PANEL_FRAME_CONFIG_TEXT_INVALID: '{config}'");
        }
        string? code = PanelNamingFormat.IsGrouped(Token)
            ? NextGrouped(horizontal, vertical)
            : PanelNamingFormat.Sequence(Token).FirstOrDefault(candidate => !_configByCode.ContainsKey(candidate));
        if (code is null)
        {
            return OperationResponse<string>.Fail($"PANEL_FRAME_CONFIG_CODES_EXHAUSTED: no free {Token} code");
        }
        _configByCode[code] = config;
        _codeByConfig[config] = code;
        assigned = true;
        return OperationResponse<string>.Ok(code);
    }

    private string? NextGrouped(int horizontal, int vertical)
    {
        string seconds = Token == "AA" ? Alphabet : "123456789";
        IEnumerable<char> classLetters = _configByCode
            .Where(entry => PanelFrameConfigFormat.TryReadCounts(entry.Value, out int h, out int v) && h == horizontal && v == vertical)
            .Select(entry => entry.Key[0]).Distinct().OrderBy(letter => Alphabet.IndexOf(letter));
        var usedFirst = _configByCode.Keys.Select(code => code[0]).ToHashSet();
        foreach (char first in classLetters.Concat(Alphabet.Where(letter => !usedFirst.Contains(letter))))
        {
            char? second = seconds.Cast<char?>().FirstOrDefault(letter => !_configByCode.ContainsKey($"{first}{letter}"));
            if (second is not null) return $"{first}{second}";
        }
        return null;
    }

    public string ToJson() => JsonSerializer.Serialize(_configByCode
        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => new Entry(entry.Key, entry.Value)));

    // Entries whose code does not fit the token were assigned under another format and are skipped.
    public static OperationResponse<PanelFrameConfigCodeRegistry> FromJson(string? json, string token = "AA")
    {
        var registry = new PanelFrameConfigCodeRegistry(token);
        if (string.IsNullOrWhiteSpace(json)) return OperationResponse<PanelFrameConfigCodeRegistry>.Ok(registry);
        try
        {
            foreach (Entry entry in JsonSerializer.Deserialize<Entry[]>(json) ?? [])
            {
                if (!registry.IsCode(entry.Code)) continue;
                OperationResponse registered = registry.Register(entry.Code, entry.Config);
                if (!registered.Success) return OperationResponse<PanelFrameConfigCodeRegistry>.Fail(registered.Message);
            }
            return OperationResponse<PanelFrameConfigCodeRegistry>.Ok(registry);
        }
        catch (JsonException exception)
        {
            return OperationResponse<PanelFrameConfigCodeRegistry>.Fail($"PANEL_FRAME_CONFIG_CODES_INVALID: {exception.Message}");
        }
    }

    private sealed record Entry(string Code, string Config);
}
