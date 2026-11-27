using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: PluginAssemblyIdentityProbe <plugin-file> [<plugin-file> ...]");
    return 2;
}

foreach (string argument in args)
{
    string path = Path.GetFullPath(argument);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Plug-in file not found: {path}");
        return 2;
    }

    using FileStream stream = File.OpenRead(path);
    using var peReader = new PEReader(stream);
    if (!peReader.HasMetadata)
    {
        Console.Error.WriteLine($"File does not contain managed metadata: {path}");
        return 2;
    }

    MetadataReader reader = peReader.GetMetadataReader();
    AssemblyDefinition assembly = reader.GetAssemblyDefinition();
    string assemblyName = reader.GetString(assembly.Name);
    string? declaredGuid = ReadGuidAttribute(reader, assembly.GetCustomAttributes());
    Guid pluginId = declaredGuid is null ? Guid.Empty : new Guid(declaredGuid);
    var pluginTypes = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Where(type =>
        reader.GetString(type.Namespace) == "PanelCladdingEditor" &&
        reader.GetString(type.Name) == "PanelCladdingEditorPlugin").ToArray();
    string? classGuid = pluginTypes.Length == 1
        ? ReadGuidAttribute(reader, pluginTypes[0].GetCustomAttributes()) : null;
    Guid classId = classGuid is null ? Guid.Empty : new Guid(classGuid);

    Console.WriteLine(
        $"PLUGIN_ASSEMBLY_IDENTITY|path={path}|assembly={assemblyName}" +
        $"|pluginId={pluginId:D}|declared={declaredGuid is not null}" +
        $"|classId={classId:D}|version={assembly.Version}");
}

return 0;

static string? ReadGuidAttribute(MetadataReader reader, CustomAttributeHandleCollection attributes)
{
    foreach (CustomAttributeHandle handle in attributes)
    {
        CustomAttribute attribute = reader.GetCustomAttribute(handle);
        if (!IsGuidAttribute(reader, attribute))
        {
            continue;
        }

        BlobReader blob = reader.GetBlobReader(attribute.Value);
        if (blob.Length < 2 || blob.ReadUInt16() != 1)
        {
            continue;
        }

        return blob.ReadSerializedString();
    }

    return null;
}

static bool IsGuidAttribute(MetadataReader reader, CustomAttribute attribute)
{
    if (attribute.Constructor.Kind != HandleKind.MemberReference)
    {
        return false;
    }

    MemberReference constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
    if (constructor.Parent.Kind != HandleKind.TypeReference)
    {
        return false;
    }

    TypeReference declaringType = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
    return reader.GetString(declaringType.Namespace) == "System.Runtime.InteropServices"
        && reader.GetString(declaringType.Name) == "GuidAttribute";
}
