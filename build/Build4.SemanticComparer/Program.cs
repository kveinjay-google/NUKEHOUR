using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

if (args.Length != 2)
{
	Console.Error.WriteLine("usage: Build4.SemanticComparer REFERENCE.dll REBUILT.dll");
	return 2;
}

var reference = AssemblySnapshot.Read(args[0]);
var rebuilt = AssemblySnapshot.Read(args[1]);

Console.WriteLine($"REFERENCE={reference.Path}");
Console.WriteLine($"REBUILT={rebuilt.Path}");
Console.WriteLine($"REFERENCE_SHA256={reference.Sha256}");
Console.WriteLine($"REBUILT_SHA256={rebuilt.Sha256}");
Console.WriteLine($"REFERENCE_PE_TIMESTAMP=0x{reference.PeTimestamp:x8}");
Console.WriteLine($"REBUILT_PE_TIMESTAMP=0x{rebuilt.PeTimestamp:x8}");
Console.WriteLine($"REFERENCE_MVID={reference.Mvid}");
Console.WriteLine($"REBUILT_MVID={rebuilt.Mvid}");
Console.WriteLine($"REFERENCE_CODEVIEW={reference.CodeView}");
Console.WriteLine($"REBUILT_CODEVIEW={rebuilt.CodeView}");

var comparisons = new[]
{
	Compare("ASSEMBLY_IDENTITY", reference.AssemblyIdentity, rebuilt.AssemblyIdentity),
	Compare("METADATA_TABLE_COUNTS", reference.TableCounts, rebuilt.TableCounts),
	Compare("ASSEMBLY_REFERENCES", reference.AssemblyReferences, rebuilt.AssemblyReferences),
	Compare("TYPE_INVENTORY", reference.Types, rebuilt.Types),
	Compare("METHOD_INVENTORY", reference.MethodInventory, rebuilt.MethodInventory),
	Compare("METHOD_IL", reference.MethodIl, rebuilt.MethodIl),
	Compare("EMBEDDED_RESOURCES", reference.Resources, rebuilt.Resources),
};

foreach (var comparison in comparisons)
	Console.WriteLine($"{comparison.Name}_EQUAL={(comparison.Equal ? "YES" : "NO")}");

foreach (var comparison in comparisons.Where(result => !result.Equal))
{
	Console.WriteLine($"{comparison.Name}_ONLY_REFERENCE_COUNT={comparison.OnlyReference.Count}");
	foreach (var value in comparison.OnlyReference.Take(20))
		Console.WriteLine($"{comparison.Name}_ONLY_REFERENCE={value}");
	Console.WriteLine($"{comparison.Name}_ONLY_REBUILT_COUNT={comparison.OnlyRebuilt.Count}");
	foreach (var value in comparison.OnlyRebuilt.Take(20))
		Console.WriteLine($"{comparison.Name}_ONLY_REBUILT={value}");
}

var semanticEqual = comparisons.All(result => result.Equal);
Console.WriteLine($"SEMANTIC_EQUAL={(semanticEqual ? "YES" : "NO")}");
Console.WriteLine($"ONLY_MVID_TIMESTAMP_CODEVIEW_DIFFER={(semanticEqual && reference.Sha256 != rebuilt.Sha256 ? "YES" : "NO")}");
return semanticEqual ? 0 : 1;

static Comparison Compare(string name, IEnumerable<string> reference, IEnumerable<string> rebuilt)
{
	var referenceValues = reference.Order(StringComparer.Ordinal).ToArray();
	var rebuiltValues = rebuilt.Order(StringComparer.Ordinal).ToArray();
	return new Comparison(
		name,
		referenceValues.SequenceEqual(rebuiltValues, StringComparer.Ordinal),
		referenceValues.Except(rebuiltValues, StringComparer.Ordinal).ToArray(),
		rebuiltValues.Except(referenceValues, StringComparer.Ordinal).ToArray());
}

sealed record Comparison(string Name, bool Equal, IReadOnlyList<string> OnlyReference, IReadOnlyList<string> OnlyRebuilt);

sealed record AssemblySnapshot(
	string Path,
	string Sha256,
	int PeTimestamp,
	string Mvid,
	string CodeView,
	IReadOnlyList<string> AssemblyIdentity,
	IReadOnlyList<string> TableCounts,
	IReadOnlyList<string> AssemblyReferences,
	IReadOnlyList<string> Types,
	IReadOnlyList<string> MethodInventory,
	IReadOnlyList<string> MethodIl,
	IReadOnlyList<string> Resources)
{
	public static AssemblySnapshot Read(string path)
	{
		using var stream = File.OpenRead(path);
		using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
		var metadata = pe.GetMetadataReader();
		var module = metadata.GetModuleDefinition();
		var mvid = metadata.GetGuid(module.Mvid).ToString("D");
		var assembly = metadata.GetAssemblyDefinition();
		var assemblyIdentity = new[]
		{
			$"{metadata.GetString(assembly.Name)}|{assembly.Version}|{assembly.Flags}|{Blob(metadata, assembly.PublicKey)}|{String(metadata, assembly.Culture)}"
		};

		var tables = Enum.GetValues<TableIndex>()
			.Select(table => $"{table}={metadata.GetTableRowCount(table)}")
			.Where(value => !value.EndsWith("=0", StringComparison.Ordinal))
			.ToArray();

		var references = metadata.AssemblyReferences
			.Select(handle => metadata.GetAssemblyReference(handle))
			.Select(item => $"{metadata.GetString(item.Name)}|{item.Version}|{item.Flags}|{Blob(metadata, item.PublicKeyOrToken)}|{String(metadata, item.Culture)}")
			.ToArray();

		var types = new List<string>();
		var methodInventory = new List<string>();
		var methodIl = new List<string>();
		foreach (var typeHandle in metadata.TypeDefinitions)
		{
			var type = metadata.GetTypeDefinition(typeHandle);
			var typeName = $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
			types.Add($"{typeName}|{type.Attributes}|base={EntityName(metadata, type.BaseType)}");
			foreach (var methodHandle in type.GetMethods())
			{
				var method = metadata.GetMethodDefinition(methodHandle);
				var key = $"{typeName}::{metadata.GetString(method.Name)}|{Blob(metadata, method.Signature)}|{method.Attributes}|{method.ImplAttributes}";
				methodInventory.Add(key);
				var ilHash = "NO_BODY";
				if (method.RelativeVirtualAddress != 0)
				{
					var body = pe.GetMethodBody(method.RelativeVirtualAddress);
					ilHash = Convert.ToHexString(SHA256.HashData(body.GetILBytes() ?? Array.Empty<byte>())).ToLowerInvariant();
				}
				methodIl.Add($"{key}|{ilHash}");
			}
		}

		var resources = new List<string>();
		var resourceDirectory = pe.PEHeaders.CorHeader?.ResourcesDirectory;
		var resourceBytes = resourceDirectory is { Size: > 0 }
			? pe.GetSectionData(resourceDirectory.Value.RelativeVirtualAddress).GetContent(0, resourceDirectory.Value.Size).ToArray()
			: Array.Empty<byte>();
		var resourceDirectoryHash = Convert.ToHexString(SHA256.HashData(resourceBytes)).ToLowerInvariant();
		resources.Add($"DIRECTORY|{resourceBytes.Length}|{resourceDirectoryHash}");
		foreach (var handle in metadata.ManifestResources)
		{
			var resource = metadata.GetManifestResource(handle);
			resources.Add($"{metadata.GetString(resource.Name)}|{resource.Attributes}|{resource.Offset}|{resource.Implementation.Kind}");
		}

		var codeView = string.Join(",", pe.ReadDebugDirectory()
			.Where(entry => entry.Type == DebugDirectoryEntryType.CodeView)
			.Select(entry =>
			{
				var data = pe.ReadCodeViewDebugDirectoryData(entry);
				return $"{data.Guid:D}/{data.Age}/{data.Path}";
			}));

		stream.Position = 0;
		var sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
		return new AssemblySnapshot(
			System.IO.Path.GetFullPath(path), sha256, pe.PEHeaders.CoffHeader.TimeDateStamp,
			mvid, codeView, assemblyIdentity, tables, references, types, methodInventory,
			methodIl, resources);
	}

	static string String(MetadataReader metadata, StringHandle handle) =>
		handle.IsNil ? "" : metadata.GetString(handle);

	static string Blob(MetadataReader metadata, BlobHandle handle) =>
		handle.IsNil ? "" : Convert.ToHexString(metadata.GetBlobBytes(handle)).ToLowerInvariant();

	static string EntityName(MetadataReader metadata, EntityHandle handle)
	{
		if (handle.IsNil)
			return "";
		return handle.Kind switch
		{
			HandleKind.TypeDefinition => TypeName(metadata, metadata.GetTypeDefinition((TypeDefinitionHandle)handle)),
			HandleKind.TypeReference => TypeName(metadata, metadata.GetTypeReference((TypeReferenceHandle)handle)),
			HandleKind.TypeSpecification => $"spec:{Blob(metadata, metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature)}",
			_ => handle.Kind.ToString(),
		};
	}

	static string TypeName(MetadataReader metadata, TypeDefinition type) =>
		$"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";

	static string TypeName(MetadataReader metadata, TypeReference type) =>
		$"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
}
