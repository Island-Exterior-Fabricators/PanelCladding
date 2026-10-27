using System.Text.Json;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingKeyService
{
    // The readable configuration is what planners write (CW_9.02_FRAME_CONFIG). Live write
    // paths derive the two-letter CW_1.08_FRAME_CONFIG code from it per document.
    public OperationResponse<string> EncodeFrameConfiguration(
        PanelCladdingTopologyState topology, int horizontalTrackCount, int verticalTrackCount)
    {
        OperationResponse<PanelCladdingTopologyPayloads> validated = EncodeTopology(
            topology, horizontalTrackCount, verticalTrackCount);
        return !validated.Success
            ? OperationResponse<string>.Fail(validated.Message)
            : OperationResponse<string>.Ok(PanelFrameConfigFormat.Format(topology, horizontalTrackCount, verticalTrackCount));
    }

    public OperationResponse<IReadOnlyDictionary<string, string>> EncodeFrameConfigurationUserText(
        PanelCladdingTopologyState topology, int horizontalTrackCount, int verticalTrackCount)
    {
        OperationResponse<string> encoded = EncodeFrameConfiguration(topology, horizontalTrackCount, verticalTrackCount);
        return !encoded.Success || encoded.Data is null
            ? OperationResponse<IReadOnlyDictionary<string, string>>.Fail(encoded.Message)
            : OperationResponse<IReadOnlyDictionary<string, string>>.Ok(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [FrameConfigBackupKey] = encoded.Data });
    }

    // Canonical readable configuration for a panel's stored attributes, converting the former
    // JSON or mask keys. Returns an empty string when the panel has no frame configuration.
    public OperationResponse<string> ReadCanonicalFrameConfiguration(IReadOnlyDictionary<string, string> userText)
    {
        bool hasConfig = new[] { FrameConfigBackupKey, FrameConfigKey, SegmentMaskKey, MergeMaskKey, HideMaskKey }
            .Any(key => userText.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.Value) &&
                !(key == FrameConfigKey && !LooksLikeFrameConfiguration(item.Value))));
        if (!hasConfig) return OperationResponse<string>.Ok(string.Empty);
        int horizontal = userText.Keys.Count(key => HorizontalOffsetRegex().IsMatch(key));
        int vertical = userText.Keys.Count(key => VerticalOffsetRegex().IsMatch(key));
        OperationResponse<PanelCladdingTopologyState> topology = DecodeTopology(userText, horizontal, vertical);
        return !topology.Success || topology.Data is null
            ? OperationResponse<string>.Fail(topology.Message)
            : EncodeFrameConfiguration(topology.Data, horizontal, vertical);
    }

    private static bool LooksLikeFrameConfiguration(string? value) =>
        PanelFrameConfigFormat.IsFormatted(value) || (value?.TrimStart().StartsWith('{') ?? false);

    public static bool IsRetiredFrameKey(string key) =>
        string.Equals(key, LegacyFrameTypologyKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, LegacyFrameAssignmentsKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, SegmentMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, MergeMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, HideMaskKey, StringComparison.OrdinalIgnoreCase);

    internal static OperationResponse<string> ReadFrameAttribute(
        IReadOnlyDictionary<string, string> userText, string key)
    {
        string[] values = userText.Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Value ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
        return values.Length > 1
            ? OperationResponse<string>.Fail($"PANEL_FRAME_ATTRIBUTE_CONFLICT: {key}")
            : OperationResponse<string>.Ok(values.FirstOrDefault() ?? string.Empty);
    }

    public static OperationResponse ValidateFrameAttributeNames(IReadOnlyDictionary<string, string> userText)
    {
        foreach ((string current, string[] legacy) in new[]
                 {
                     (FrameConfigBackupKey, new[] { FrameConfigKey, SegmentMaskKey, MergeMaskKey, HideMaskKey }),
                     (FrameTypeKey, new[] { LegacyFrameAssignmentsKey })
                 })
        {
            OperationResponse<string> value = ReadFrameAttribute(userText, current);
            if (!value.Success) return OperationResponse.Fail(value.Message);
            if (!string.IsNullOrWhiteSpace(value.Data)) continue;
            foreach (string key in legacy)
            {
                value = ReadFrameAttribute(userText, key);
                if (!value.Success) return OperationResponse.Fail(value.Message);
            }
        }
        return OperationResponse.Ok();
    }

    // Readable configuration from CW_9.02 (preferred) or CW_1.08; null when neither holds one.
    private static OperationResponse<PanelFrameConfigFormat.Parsed?> ReadReadableFrameConfiguration(
        IReadOnlyDictionary<string, string> userText)
    {
        OperationResponse<string> backup = ReadFrameAttribute(userText, FrameConfigBackupKey);
        OperationResponse<string> code = ReadFrameAttribute(userText, FrameConfigKey);
        if (!backup.Success) return OperationResponse<PanelFrameConfigFormat.Parsed?>.Fail(backup.Message);
        if (!code.Success) return OperationResponse<PanelFrameConfigFormat.Parsed?>.Fail(code.Message);
        string? text = PanelFrameConfigFormat.IsFormatted(backup.Data) ? backup.Data
            : PanelFrameConfigFormat.IsFormatted(code.Data) ? code.Data : null;
        if (text is null)
        {
            return PanelNamingFormat.LooksLikeCode(code.Data?.Trim()) && string.IsNullOrWhiteSpace(backup.Data)
                ? OperationResponse<PanelFrameConfigFormat.Parsed?>.Fail(
                    $"PANEL_FRAME_CONFIG_BACKUP_MISSING: code {code.Data!.Trim()} needs {FrameConfigBackupKey}.")
                : OperationResponse<PanelFrameConfigFormat.Parsed?>.Ok(null);
        }
        OperationResponse<PanelFrameConfigFormat.Parsed> parsed = PanelFrameConfigFormat.Parse(text);
        return parsed.Success
            ? OperationResponse<PanelFrameConfigFormat.Parsed?>.Ok(parsed.Data)
            : OperationResponse<PanelFrameConfigFormat.Parsed?>.Fail(parsed.Message);
    }

    private static OperationResponse<PanelCladdingTopologyPayloads> ReadFrameConfiguration(
        IReadOnlyDictionary<string, string> userText)
    {
        OperationResponse<string> backup = ReadFrameAttribute(userText, FrameConfigBackupKey);
        OperationResponse<string> config = backup.Success && (backup.Data?.TrimStart().StartsWith('{') ?? false)
            ? backup
            : ReadFrameAttribute(userText, FrameConfigKey);
        if (!config.Success)
        {
            return OperationResponse<PanelCladdingTopologyPayloads>.Fail(config.Message);
        }
        if (!string.IsNullOrWhiteSpace(config.Data))
        {
            if (config.Data.Length > 8_000_000)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail("PANEL_FRAME_CONFIG_PAYLOAD_TOO_LARGE");
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(config.Data);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    root.EnumerateObject().Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != 4 ||
                    root.EnumerateObject().Count() != 4 || root.GetProperty("v").GetInt32() != 1)
                {
                    throw new FormatException("Expected version 1 and delete, merge, hide masks.");
                }
                string Mask(string name)
                {
                    string? value = root.GetProperty(name).GetString();
                    return !string.IsNullOrWhiteSpace(value)
                        ? value : throw new FormatException($"Missing {name} mask.");
                }
                return OperationResponse<PanelCladdingTopologyPayloads>.Ok(new()
                {
                    SegmentMask = Mask("delete"), MergeMask = Mask("merge"), HideMask = Mask("hide")
                });
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or
                                                KeyNotFoundException or FormatException or OverflowException)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail($"PANEL_FRAME_CONFIG_INVALID: {error.Message}");
            }
        }

        OperationResponse<string> delete = ReadFrameAttribute(userText, SegmentMaskKey);
        OperationResponse<string> merge = ReadFrameAttribute(userText, MergeMaskKey);
        OperationResponse<string> hide = ReadFrameAttribute(userText, HideMaskKey);
        foreach (OperationResponse<string> mask in new[] { delete, merge, hide })
        {
            if (!mask.Success) return OperationResponse<PanelCladdingTopologyPayloads>.Fail(mask.Message);
        }
        return OperationResponse<PanelCladdingTopologyPayloads>.Ok(new()
        {
            SegmentMask = delete.Data ?? string.Empty,
            MergeMask = merge.Data ?? string.Empty,
            HideMask = hide.Data ?? string.Empty
        });
    }
}
