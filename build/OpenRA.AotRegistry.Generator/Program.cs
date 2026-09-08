using System.ComponentModel;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

const int DescriptorSchemaVersion = 1;
const int DescriptorCount = 296;
const string DescriptorShapeMagic = "OpenRA.RuntimeContractDescriptorShape";
const string DescriptorShapeDigest = "4fe820475ee4d6ef843320f406b5db3fd414fd106c953b4a7b4a05020fb3614d";
const string DescriptorGoldenFileSha256 = "6c0d12b5a985bcf87fc255a03a958dbc09be00de9238b239354c8d34be1bd8f6";
const string AotRegistryMagic = "OpenRA.AotRegistry";

if (args.Length < 5)
	throw new ArgumentException("Usage: OpenRA.AotRegistry.Generator <output.cs> <descriptor-items.tsv> " +
		"<descriptor-shape.json> <expected-descriptor-shape-file-sha256> <assembly.dll>...");

var output = Path.GetFullPath(args[0]);
var descriptorItemsPath = Path.GetFullPath(args[1]);
var descriptorShapePath = Path.GetFullPath(args[2]);
if (args[3] != DescriptorGoldenFileSha256)
	throw new InvalidDataException("The descriptor-shape SHA-256 argument is not the reviewed schema-1 value.");
var assemblyPaths = args.Skip(4).Select(Path.GetFullPath).ToArray();
if (assemblyPaths.Length == 0 ||
	assemblyPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != assemblyPaths.Length)
	throw new InvalidDataException("Managed assembly paths are empty, duplicated, or case-aliased.");

var searchDirectories = assemblyPaths.Select(path => Path.GetDirectoryName(path) ??
	throw new InvalidDataException($"Managed assembly path has no parent directory: {path}"))
	.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
if (searchDirectories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != searchDirectories.Length)
	throw new InvalidDataException("Managed assembly directories are duplicated or case-aliased.");

var goldenDescriptors = ReadAuthenticatedGolden(descriptorShapePath);
var evaluatedDescriptors = ReadEvaluatedDescriptors(descriptorItemsPath);
RequireEvaluatedProjection(goldenDescriptors, evaluatedDescriptors);
var runtimeDescriptors = HashDescriptorSources(goldenDescriptors);

var candidatePaths = searchDirectories.SelectMany(directory =>
	Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly)
		.Where(path => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase)))
	.Select(Path.GetFullPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
if (candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != candidatePaths.Length)
	throw new InvalidDataException("Managed assembly candidates are duplicated or case-aliased.");

var candidateAssemblies = candidatePaths.Select(path => ManagedAssemblyInput.Read(path,
	ReadRegularFile(path, $"managed assembly candidate {path}"))).ToArray();
var candidatesByPath = candidateAssemblies.ToDictionary(input => input.Path, StringComparer.Ordinal);
var managedAssemblies = assemblyPaths.Select(path => candidatesByPath.TryGetValue(path, out var input) ? input :
	throw new InvalidDataException($"Explicit managed assembly is absent from the preclosed candidate set: {path}"))
	.ToArray();
var candidatesByIdentity = new Dictionary<ManagedAssemblyIdentity, ManagedAssemblyInput>();
var identityAliases = new Dictionary<string, ManagedAssemblyInput>(StringComparer.OrdinalIgnoreCase);
var candidatesBySimpleName = new Dictionary<string, List<ManagedAssemblyInput>>(StringComparer.Ordinal);
var canonicalSimpleNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var input in candidateAssemblies)
{
	if (!candidatesByIdentity.TryAdd(input.Identity, input))
		throw new InvalidDataException($"Duplicate managed assembly identity {input.Identity} in the preclosed set.");
	if (identityAliases.TryGetValue(input.Identity.ToString(), out var alias) &&
		!alias.Identity.Equals(input.Identity))
		throw new InvalidDataException($"Case-aliased managed assembly identities {alias.Identity} and " +
			$"{input.Identity} are not canonical.");
	identityAliases[input.Identity.ToString()] = input;
	if (canonicalSimpleNames.TryGetValue(input.Identity.Name, out var canonicalName) &&
		!string.Equals(canonicalName, input.Identity.Name, StringComparison.Ordinal))
		throw new InvalidDataException($"Case-aliased managed assembly simple names {canonicalName} and " +
			$"{input.Identity.Name} are not canonical.");
	canonicalSimpleNames[input.Identity.Name] = input.Identity.Name;
	if (!candidatesBySimpleName.TryGetValue(input.Identity.Name, out var sameName))
		candidatesBySimpleName.Add(input.Identity.Name, sameName = new List<ManagedAssemblyInput>());
	sameName.Add(input);
}

foreach (var pair in candidatesBySimpleName.OrderBy(pair => pair.Key, StringComparer.Ordinal))
	if (pair.Value.Count > 1)
		throw new InvalidDataException($"Conflicting managed assembly identities share simple name {pair.Key}: " +
			string.Join(" | ", pair.Value.Select(candidate => candidate.Identity.ToString())
				.OrderBy(identity => identity, StringComparer.Ordinal)) + ".");

foreach (var input in candidateAssemblies)
	foreach (var reference in input.References)
		if (candidatesBySimpleName.TryGetValue(reference.Name, out var sameName) &&
			!candidatesByIdentity.ContainsKey(reference))
			throw new InvalidDataException($"Managed assembly {input.Identity} references {reference}, but the " +
				$"preclosed candidate set only has {string.Join(", ", sameName.Select(candidate => candidate.Identity))}.");

var loadedAssemblies = new Dictionary<ManagedAssemblyIdentity, Assembly>();
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
	if (!ManagedAssemblyIdentity.TryCreate(name, out var identity))
		return null;
	if (loadedAssemblies.TryGetValue(identity, out var loaded))
		return loaded;
	if (candidatesByIdentity.TryGetValue(identity, out var input))
		return LoadManagedAssembly(input);
	return null;
};

var registrations = new List<AotRegistration>();
foreach (var input in managedAssemblies)
{
	var assembly = LoadManagedAssembly(input);
	foreach (var type in LoadableTypes(assembly))
	{
		if (!CanName(type) || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters ||
			type.Namespace == "Microsoft.CodeAnalysis" || typeof(Attribute).IsAssignableFrom(type))
			continue;

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var useConstructors = type.GetConstructors(flags)
			.Where(c => c.CustomAttributes.Any(a => a.AttributeType.FullName == "OpenRA.ObjectCreator+UseCtorAttribute"))
			.ToArray();
		if (useConstructors.Length > 1)
			throw new InvalidOperationException($"Multiple UseCtor constructors on {type.FullName}.");

		if (useConstructors.Length == 1)
		{
			if (CanCall(useConstructors[0]))
				registrations.Add(CreateUseCtorRegistration(type, useConstructors[0]));
			continue;
		}

		var basic = type.GetConstructor(flags, null, Type.EmptyTypes, null);
		if (basic != null && CanCall(basic))
			registrations.Add(CreateBasicRegistration(type));
	}
}

Assembly LoadManagedAssembly(ManagedAssemblyInput input)
{
	if (loadedAssemblies.TryGetValue(input.Identity, out var loaded))
		return loaded;

	using var stream = new MemoryStream(input.Bytes, false);
	var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
	if (!ManagedAssemblyIdentity.TryCreate(assembly.GetName(), out var actualIdentity) ||
		!actualIdentity.Equals(input.Identity))
		throw new InvalidDataException($"Loaded managed assembly identity {actualIdentity?.ToString() ?? "<invalid>"} " +
			$"does not match stable identity {input.Identity} from {input.Path}.");
	loadedAssemblies.Add(input.Identity, assembly);
	return assembly;
}

registrations.Sort((left, right) => string.CompareOrdinal(left.TypeFullName, right.TypeFullName));
var registrationNames = new HashSet<string>(StringComparer.Ordinal);
var registrationAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var registration in registrations)
	if (!registrationNames.Add(registration.TypeFullName) ||
		!registrationAliases.Add(registration.TypeFullName))
		throw new InvalidDataException($"Duplicate or case-aliased AOT registration: {registration.TypeFullName}");
var aotRecordPayloads = registrations.Select(FrameAotRecord).ToArray();
var aotIdentity = Hex(SHA256.HashData(FrameAotRegistry(aotRecordPayloads)));

var source = new StringBuilder();
source.AppendLine("// <auto-generated />");
source.AppendLine("using System.Collections.Immutable;");
source.AppendLine("using OpenRA;");
source.AppendLine("using OpenRA.Mods.RA2.Content;");
source.AppendLine();
source.AppendLine("namespace OpenRA.iOS");
source.AppendLine("{");
source.AppendLine("\tinternal static class GeneratedAotObjectRegistryRegistration");
source.AppendLine("\t{");
source.AppendLine(Invariant($"\t\tinternal const int RuntimeContractDescriptorSchemaVersion = {DescriptorSchemaVersion};"));
source.AppendLine(Invariant($"\t\tinternal const int RuntimeContractDescriptorCount = {DescriptorCount};"));
source.AppendLine(Invariant($"\t\tinternal const string RuntimeContractDescriptorShapeDigest = \"{DescriptorShapeDigest}\";"));
source.AppendLine(Invariant($"\t\tinternal const string AotRegistryIdentity = \"{aotIdentity}\";"));
source.AppendLine();
source.AppendLine("\t\tinternal static readonly string[] RegisteredTypeNames =");
source.AppendLine("\t\t{");
foreach (var registration in registrations)
	source.AppendLine(Invariant($"\t\t\t\"{EscapeString(registration.TypeFullName)}\","));
source.AppendLine("\t\t};");
source.AppendLine();
source.AppendLine("\t\t// BEGIN AOT IDENTITY RECORD PAYLOADS");
source.AppendLine("\t\tinternal static readonly string[] AotRegistryIdentityRecordPayloadsBase64 =");
source.AppendLine("\t\t{");
foreach (var payload in aotRecordPayloads)
	source.AppendLine(Invariant($"\t\t\t\"{Convert.ToBase64String(payload)}\","));
source.AppendLine("\t\t};");
source.AppendLine("\t\t// END AOT IDENTITY RECORD PAYLOADS");
source.AppendLine();
source.AppendLine("\t\tinternal static RuntimeContractDescriptorInventory RuntimeContractDescriptors { get; } =");
source.AppendLine("\t\t\tnew RuntimeContractDescriptorInventory(");
source.AppendLine("\t\t\t\tRuntimeContractDescriptorSchemaVersion,");
source.AppendLine("\t\t\t\tRuntimeContractDescriptorCount,");
source.AppendLine("\t\t\t\tRuntimeContractDescriptorShapeDigest,");
source.AppendLine("\t\t\t\tImmutableArray.Create(");
for (var i = 0; i < runtimeDescriptors.Count; i++)
{
	var descriptor = runtimeDescriptors[i];
	var profiles = string.Join(", ", descriptor.Profiles.Select(p => $"Ra2RuntimeProfileId.{p}"));
	source.AppendLine("\t\t\t\t\tnew RuntimeContractResourceDescriptor(");
	source.AppendLine(Invariant($"\t\t\t\t\t\t\"{EscapeString(descriptor.LogicalName)}\","));
	source.AppendLine(Invariant($"\t\t\t\t\t\t\"{EscapeString(descriptor.BundleRelativePath)}\","));
	source.AppendLine(Invariant($"\t\t\t\t\t\t{descriptor.Length}L,"));
	source.AppendLine(Invariant($"\t\t\t\t\t\t\"{descriptor.Sha256}\","));
	source.AppendLine(Invariant($"\t\t\t\t\t\tImmutableArray.Create({profiles}),"));
	source.AppendLine(Invariant(
		$"\t\t\t\t\t\tRuntimeContractResourcePurpose.{descriptor.Purpose}){(i == runtimeDescriptors.Count - 1 ? "));" : ",")}"));
}
source.AppendLine();
source.AppendLine("\t\tinternal static void RegisterAll()");
source.AppendLine("\t\t{");
foreach (var registration in registrations)
{
	if (registration.Kind == AotRegistrationKind.Basic)
		source.AppendLine(Invariant(
			$"\t\t\tGeneratedAotObjectRegistry.RegisterBasic(typeof({registration.TypeExpression}), static () => new {registration.TypeExpression}());"));
	else
	{
		var arguments = string.Join(", ", registration.Parameters.Select(parameter =>
			$"({parameter.TypeExpression})args[\"{EscapeString(parameter.Name)}\"]"));
		source.AppendLine(Invariant(
			$"\t\t\tGeneratedAotObjectRegistry.RegisterWithArgs(typeof({registration.TypeExpression}), static args => new {registration.TypeExpression}({arguments}));"));
	}
}
source.AppendLine("\t\t}");
source.AppendLine("\t}");
source.AppendLine("}");

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, source.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Generated {registrations.Count} direct AOT object factories and " +
	$"{runtimeDescriptors.Count} authenticated runtime descriptors at {output}.");

IReadOnlyList<GoldenDescriptor> ReadAuthenticatedGolden(string path)
{
	var bytes = ReadRegularFile(path, "descriptor-shape golden");
	if (Hex(SHA256.HashData(bytes)) != DescriptorGoldenFileSha256)
		throw new InvalidDataException("Descriptor-shape golden SHA-256 does not match the reviewed schema-1 file.");

	using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
	{
		AllowTrailingCommas = false,
		CommentHandling = JsonCommentHandling.Disallow,
		MaxDepth = 32,
	});
	var root = document.RootElement;
	var rootNames = root.EnumerateObject().Select(p => p.Name).ToArray();
	if (!rootNames.SequenceEqual(new[]
	{
		"schemaVersion", "descriptorCount", "purposeCounts", "shapeDigest",
		"shapeFraming", "sourcePathRules", "descriptors"
	}, StringComparer.Ordinal))
		throw new InvalidDataException("Descriptor-shape root has missing, extra, or reordered properties.");
	if (root.GetProperty("schemaVersion").GetInt32() != DescriptorSchemaVersion ||
		root.GetProperty("descriptorCount").GetInt32() != DescriptorCount ||
		ReadExactString(root.GetProperty("shapeDigest"), "shapeDigest") != DescriptorShapeDigest)
		throw new InvalidDataException("Descriptor-shape schema, count, or digest is not the reviewed baseline.");

	var purposeCounts = root.GetProperty("purposeCounts");
	if (purposeCounts.GetProperty("Contract").GetInt32() != 165 ||
		purposeCounts.GetProperty("PreflightOnly").GetInt32() != 131)
		throw new InvalidDataException("Descriptor purpose counts do not match the reviewed baseline.");

	var descriptors = root.GetProperty("descriptors").EnumerateArray().Select((element, ordinal) =>
	{
		var names = element.EnumerateObject().Select(p => p.Name).ToArray();
		if (!names.SequenceEqual(new[]
		{
			"logicalName", "bundleRelativePath", "profiles", "purpose", "sourcePath"
		}, StringComparer.Ordinal))
			throw new InvalidDataException($"Descriptor row {ordinal} has missing, extra, or reordered properties.");
		return new GoldenDescriptor(
			ReadExactString(element.GetProperty("sourcePath"), $"descriptor[{ordinal}].sourcePath"),
			ReadExactString(element.GetProperty("logicalName"), $"descriptor[{ordinal}].logicalName"),
			ReadExactString(element.GetProperty("bundleRelativePath"),
				$"descriptor[{ordinal}].bundleRelativePath"),
			element.GetProperty("profiles").EnumerateArray().Select((profile, index) =>
				ReadExactString(profile, $"descriptor[{ordinal}].profiles[{index}]")).ToArray(),
			ReadExactString(element.GetProperty("purpose"), $"descriptor[{ordinal}].purpose"));
	}).ToArray();
	if (descriptors.Length != DescriptorCount)
		throw new InvalidDataException($"Expected exactly {DescriptorCount} descriptor rows.");
	ValidateGoldenDescriptors(descriptors);
	if (Hex(SHA256.HashData(FrameDescriptorShape(descriptors))) != DescriptorShapeDigest)
		throw new InvalidDataException("Descriptor-shape canonical framing does not match its reviewed digest.");
	return descriptors;
}

void ValidateGoldenDescriptors(IReadOnlyList<GoldenDescriptor> descriptors)
{
	var names = new HashSet<string>(StringComparer.Ordinal);
	var nameAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	var paths = new HashSet<string>(StringComparer.Ordinal);
	var pathAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	var sources = new HashSet<string>(StringComparer.Ordinal);
	var sourceAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	for (var i = 0; i < descriptors.Count; i++)
	{
		var descriptor = descriptors[i];
		ValidateField(descriptor.SourcePath, $"descriptor[{i}].sourcePath");
		ValidateRelativePath(descriptor.LogicalName, $"descriptor[{i}].logicalName");
		ValidateRelativePath(descriptor.BundleRelativePath, $"descriptor[{i}].bundleRelativePath");
		if (descriptor.LogicalName != descriptor.BundleRelativePath ||
			descriptor.SourcePath != ExpectedSourcePath(descriptor.LogicalName))
			throw new InvalidDataException($"Descriptor row {i} violates the reviewed path mapping.");
		if (!IsCanonicalProfiles(descriptor.Profiles) ||
			descriptor.Purpose is not "Contract" and not "PreflightOnly")
			throw new InvalidDataException($"Descriptor row {i} has invalid profiles or purpose.");
		if (!names.Add(descriptor.LogicalName) || !nameAliases.Add(descriptor.LogicalName) ||
			!paths.Add(descriptor.BundleRelativePath) || !pathAliases.Add(descriptor.BundleRelativePath) ||
			!sources.Add(descriptor.SourcePath) || !sourceAliases.Add(descriptor.SourcePath))
			throw new InvalidDataException($"Descriptor row {i} duplicates or case-aliases another row.");
		if (i > 0 && CompareUtf8(descriptors[i - 1].LogicalName, descriptor.LogicalName) >= 0)
			throw new InvalidDataException($"Descriptor row {i} is not in canonical UTF-8 byte order.");
	}

	if (descriptors.Count(d => d.Purpose == "Contract") != 165 ||
		descriptors.Count(d => d.Purpose == "PreflightOnly") != 131 ||
		descriptors.Count(d => d.Profiles.Contains("Base", StringComparer.Ordinal)) != 277 ||
		descriptors.Count(d => d.Profiles.Contains("Yuri", StringComparer.Ordinal)) != 284)
		throw new InvalidDataException("Descriptor profile/purpose projections do not match the reviewed baseline.");
}

IReadOnlyList<EvaluatedDescriptor> ReadEvaluatedDescriptors(string path)
{
	var bytes = ReadRegularFile(path, "evaluated descriptor input");
	if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
		throw new InvalidDataException("Evaluated descriptor input must be UTF-8 without a BOM.");
	string text;
	try
	{
		text = new UTF8Encoding(false, true).GetString(bytes);
	}
	catch (DecoderFallbackException e)
	{
		throw new InvalidDataException("Evaluated descriptor input is not strict UTF-8.", e);
	}

	var lines = SplitStrictLines(text);
	if (lines.Count != DescriptorCount)
		throw new InvalidDataException($"Evaluated descriptor input must contain exactly {DescriptorCount} rows.");
	return lines.Select((line, ordinal) =>
	{
		if (line.Length == 0)
			throw new InvalidDataException($"Evaluated descriptor row {ordinal} is blank.");
		var fields = line.Split('\t');
		if (fields.Length != 5)
			throw new InvalidDataException($"Evaluated descriptor row {ordinal} must have five TAB-separated fields.");
		for (var i = 0; i < fields.Length; i++)
			ValidateField(fields[i], $"evaluated descriptor[{ordinal}].field[{i}]");
		return new EvaluatedDescriptor(fields[0], fields[1], fields[2], fields[3], fields[4]);
	}).ToArray();
}

IReadOnlyList<string> SplitStrictLines(string text)
{
	var lines = new List<string>();
	var start = 0;
	for (var i = 0; i < text.Length; i++)
	{
		if (text[i] == '\r')
		{
			if (i + 1 >= text.Length || text[i + 1] != '\n')
				throw new InvalidDataException("A carriage return is valid only in a CRLF record separator.");
			lines.Add(text[start..i]);
			i++;
			start = i + 1;
		}
		else if (text[i] == '\n')
		{
			lines.Add(text[start..i]);
			start = i + 1;
		}
	}
	if (start < text.Length)
		lines.Add(text[start..]);
	return lines;
}

void RequireEvaluatedProjection(IReadOnlyList<GoldenDescriptor> expected,
	IReadOnlyList<EvaluatedDescriptor> actual)
{
	var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	for (var i = 0; i < expected.Count; i++)
	{
		var golden = expected[i];
		var evaluated = actual[i];
		if (evaluated.SourceIdentity != golden.SourcePath || evaluated.LogicalName != golden.LogicalName ||
			evaluated.BundleRelativePath != golden.BundleRelativePath ||
			evaluated.Profiles != string.Join(',', golden.Profiles) || evaluated.Purpose != golden.Purpose)
			throw new InvalidDataException($"Evaluated descriptor row {i} differs from the authenticated golden.");
		if (!sources.Add(evaluated.SourceIdentity) || !names.Add(evaluated.LogicalName) ||
			!paths.Add(evaluated.BundleRelativePath))
			throw new InvalidDataException($"Evaluated descriptor row {i} duplicates or case-aliases another row.");
	}
}

IReadOnlyList<HashedDescriptor> HashDescriptorSources(IReadOnlyList<GoldenDescriptor> descriptors)
{
	var projectDirectory = Path.GetFullPath(Environment.CurrentDirectory);
	return descriptors.Select((descriptor, ordinal) =>
	{
		var path = Path.GetFullPath(Path.Combine(projectDirectory, descriptor.SourcePath));
		var bytes = ReadRegularFile(path, $"descriptor[{ordinal}] source");
		return new HashedDescriptor(descriptor.LogicalName, descriptor.BundleRelativePath,
			bytes.LongLength, Hex(SHA256.HashData(bytes)), descriptor.Profiles, descriptor.Purpose);
	}).ToArray();
}

AotRegistration CreateBasicRegistration(Type type)
{
	var fullName = RequireTypeFullName(type);
	return new AotRegistration(fullName, TypeName(type), AotRegistrationKind.Basic,
		Array.Empty<AotParameter>());
}

AotRegistration CreateUseCtorRegistration(Type type, ConstructorInfo constructor)
{
	var fullName = RequireTypeFullName(type);
	var parameters = constructor.GetParameters().Select((parameter, ordinal) =>
	{
		if (string.IsNullOrEmpty(parameter.Name))
			throw new InvalidDataException($"UseCtor parameter {ordinal} on {fullName} has no metadata name.");
		ValidateField(parameter.Name, $"UseCtor parameter {ordinal} on {fullName}");
		return new AotParameter(parameter.Name, TypeName(parameter.ParameterType));
	}).ToArray();
	return new AotRegistration(fullName, TypeName(type), AotRegistrationKind.UseCtor, parameters);
}

string RequireTypeFullName(Type type)
{
	var fullName = type.FullName;
	if (string.IsNullOrEmpty(fullName))
		throw new InvalidDataException("A selected AOT registration has no full type name.");
	ValidateField(fullName, "AOT registration type name");
	return fullName;
}

byte[] FrameDescriptorShape(IReadOnlyList<GoldenDescriptor> descriptors)
{
	using var output = new MemoryStream();
	WriteLengthPrefixed(output, DescriptorShapeMagic);
	WriteUInt32(output, DescriptorSchemaVersion);
	WriteUInt32(output, checked((uint)descriptors.Count));
	for (var i = 0; i < descriptors.Count; i++)
	{
		using var payload = new MemoryStream();
		var descriptor = descriptors[i];
		WriteUInt32(payload, checked((uint)i));
		WriteLengthPrefixed(payload, descriptor.LogicalName);
		WriteLengthPrefixed(payload, descriptor.BundleRelativePath);
		WriteUInt32(payload, checked((uint)descriptor.Profiles.Length));
		foreach (var profile in descriptor.Profiles)
			WriteLengthPrefixed(payload, profile);
		WriteLengthPrefixed(payload, descriptor.Purpose);
		WriteUInt32(output, checked((uint)payload.Length));
		payload.WriteTo(output);
	}
	return output.ToArray();
}

byte[] FrameAotRecord(AotRegistration registration)
{
	using var output = new MemoryStream();
	WriteLengthPrefixed(output, registration.TypeFullName);
	WriteUInt32(output, (uint)registration.Kind);
	WriteUInt32(output, checked((uint)registration.Parameters.Length));
	foreach (var parameter in registration.Parameters)
	{
		WriteLengthPrefixed(output, parameter.Name);
		WriteLengthPrefixed(output, parameter.TypeExpression);
	}
	return output.ToArray();
}

byte[] FrameAotRegistry(IReadOnlyList<byte[]> payloads)
{
	using var output = new MemoryStream();
	WriteLengthPrefixed(output, AotRegistryMagic);
	WriteUInt32(output, 1);
	WriteUInt32(output, checked((uint)payloads.Count));
	foreach (var payload in payloads)
		output.Write(payload, 0, payload.Length);
	return output.ToArray();
}

void WriteLengthPrefixed(Stream output, string value)
{
	ValidateField(value, "framed string");
	var bytes = new UTF8Encoding(false, true).GetBytes(value);
	WriteUInt32(output, checked((uint)bytes.Length));
	output.Write(bytes, 0, bytes.Length);
}

static void WriteUInt32(Stream output, uint value)
{
	output.WriteByte((byte)value);
	output.WriteByte((byte)(value >> 8));
	output.WriteByte((byte)(value >> 16));
	output.WriteByte((byte)(value >> 24));
}

byte[] ReadRegularFile(string path, string label)
{
	var fullPath = Path.GetFullPath(path);
	using var handle = SecureFile.OpenReadNoFollow(fullPath, label);
	var before = StableFileSnapshot.Capture(handle, label);
	if (!before.IsRegular || before.Length < 0 || before.Length > int.MaxValue)
		throw new InvalidDataException($"{label} must be a bounded regular file.");

	var bytes = new byte[checked((int)before.Length)];
	var offset = 0;
	while (offset < bytes.Length)
	{
		var read = RandomAccess.Read(handle, bytes.AsSpan(offset), offset);
		if (read == 0)
			throw new InvalidDataException($"{label} changed length while being read.");
		offset += read;
	}

	Span<byte> extra = stackalloc byte[1];
	if (RandomAccess.Read(handle, extra, before.Length) != 0)
		throw new InvalidDataException($"{label} changed length while being read.");
	var after = StableFileSnapshot.Capture(handle, label);
	if (!before.HasStableContents(after))
		throw new InvalidDataException($"{label} changed identity or contents while being read.");

	using var currentHandle = SecureFile.OpenReadNoFollow(fullPath, label);
	var current = StableFileSnapshot.Capture(currentHandle, label);
	if (!current.IsRegular || !before.HasStableContents(current))
		throw new InvalidDataException($"{label} path identity or contents changed while being read.");
	return bytes;
}

string ExpectedSourcePath(string logicalName)
{
	if (logicalName is "AUTHORS" or "VERSION" or "global mix database.dat" ||
		logicalName.StartsWith("glsl/", StringComparison.Ordinal) ||
		logicalName.StartsWith("mods/common/", StringComparison.Ordinal) ||
		logicalName.StartsWith("mods/ts/", StringComparison.Ordinal))
		return "../../engine/" + logicalName;
	if (logicalName.StartsWith("mods/ra2/", StringComparison.Ordinal))
		return "../../" + logicalName;
	throw new InvalidDataException($"Descriptor logical name has no reviewed source mapping: {logicalName}");
}

static bool IsCanonicalProfiles(IReadOnlyList<string> profiles) =>
	profiles.Count > 0 && profiles.Count <= 2 &&
	(profiles.Count == 1 && profiles[0] is "Base" or "Yuri" ||
		profiles.Count == 2 && profiles[0] == "Base" && profiles[1] == "Yuri");

void ValidateRelativePath(string path, string label)
{
	ValidateField(path, label);
	if (Path.IsPathRooted(path) || path.Contains('\\') || path.IndexOfAny(new[] { '*', '?', '[', ']' }) >= 0 ||
		path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
		throw new InvalidDataException($"{label} is not a canonical relative path.");
}

void ValidateField(string value, string label)
{
	if (string.IsNullOrEmpty(value) || !value.IsNormalized(NormalizationForm.FormC) ||
		value.IndexOfAny(new[] { '\0', '\t', '\r', '\n' }) >= 0)
		throw new InvalidDataException($"{label} is empty, non-NFC, or contains a forbidden separator.");
	try
	{
		_ = new UTF8Encoding(false, true).GetBytes(value);
	}
	catch (EncoderFallbackException e)
	{
		throw new InvalidDataException($"{label} is not strict UTF-8.", e);
	}
}

string ReadExactString(JsonElement element, string label)
{
	if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } value)
		throw new InvalidDataException($"{label} must be a non-null JSON string.");
	ValidateField(value, label);
	return value;
}

static int CompareUtf8(string left, string right)
{
	var leftBytes = Encoding.UTF8.GetBytes(left);
	var rightBytes = Encoding.UTF8.GetBytes(right);
	for (var i = 0; i < Math.Min(leftBytes.Length, rightBytes.Length); i++)
		if (leftBytes[i] != rightBytes[i])
			return leftBytes[i].CompareTo(rightBytes[i]);
	return leftBytes.Length.CompareTo(rightBytes.Length);
}

static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

static string Invariant(FormattableString value) => FormattableString.Invariant(value);

static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

static IEnumerable<Type> LoadableTypes(Assembly assembly)
{
	try
	{
		return assembly.GetTypes();
	}
	catch (ReflectionTypeLoadException e)
	{
		throw new InvalidOperationException($"Could not load every type from {assembly.FullName}: " +
			string.Join(" | ", e.LoaderExceptions.Where(exception => exception != null)
				.Select(exception => exception!.GetType().Name)), e);
	}
}

static bool CanCall(ConstructorInfo constructor) =>
	constructor.IsPublic || constructor.IsAssembly || constructor.IsFamilyOrAssembly;

static bool CanName(Type type)
{
	for (var current = type; current != null; current = current.DeclaringType)
		if (current.IsNestedPrivate || current.IsNestedFamily || current.IsNestedFamANDAssem)
			return false;

	return true;
}

static string TypeName(Type type)
{
	if (type.IsByRef || type.IsPointer || type.IsFunctionPointer || type.IsGenericParameter)
		throw new InvalidDataException($"Unsupported AOT constructor type shape: {type}.");
	if (type.IsArray)
		return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
	if (type.IsGenericType)
	{
		if (type.IsNested)
			throw new InvalidDataException($"Nested generic AOT constructor type is unsupported: {type}.");
		var definition = type.GetGenericTypeDefinition();
		var name = definition.FullName ?? throw new InvalidDataException(
			$"Generic AOT constructor type has no full name: {type}.");
		var tick = name.IndexOf('`');
		if (tick < 1)
			throw new InvalidDataException($"Generic AOT constructor type has an invalid name: {type}.");
		name = name[..tick];
		return $"global::{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
	}

	return "global::" + (type.FullName ?? throw new InvalidDataException(
		$"AOT constructor type has no full name: {type}.")).Replace('+', '.');
}

sealed class ManagedAssemblyInput
{
	public string Path { get; }
	public byte[] Bytes { get; }
	public ManagedAssemblyIdentity Identity { get; }
	public IReadOnlyList<ManagedAssemblyIdentity> References { get; }

	ManagedAssemblyInput(string path, byte[] bytes, ManagedAssemblyIdentity identity,
		IReadOnlyList<ManagedAssemblyIdentity> references)
	{
		Path = path;
		Bytes = bytes;
		Identity = identity;
		References = references;
	}

	public static ManagedAssemblyInput Read(string path, byte[] bytes)
	{
		try
		{
			using var stream = new MemoryStream(bytes, false);
			using var portableExecutable = new PEReader(stream, PEStreamOptions.LeaveOpen);
			if (!portableExecutable.HasMetadata)
				throw new InvalidDataException("The PE image has no managed metadata.");
			var metadata = portableExecutable.GetMetadataReader();
			if (!metadata.IsAssembly)
				throw new InvalidDataException("The managed metadata is not an assembly.");
			var identity = ManagedAssemblyIdentity.Create(metadata, metadata.GetAssemblyDefinition());
			var references = metadata.AssemblyReferences.Select(handle =>
				ManagedAssemblyIdentity.Create(metadata, metadata.GetAssemblyReference(handle))).ToArray();
			return new ManagedAssemblyInput(path, bytes, identity, references);
		}
		catch (Exception e) when (e is BadImageFormatException or InvalidOperationException)
		{
			throw new InvalidDataException($"Managed assembly candidate {path} has invalid metadata.", e);
		}
	}
}

sealed class ManagedAssemblyIdentity : IEquatable<ManagedAssemblyIdentity>
{
	public string Name { get; }
	public Version Version { get; }
	public string Culture { get; }
	public string PublicKeyToken { get; }

	ManagedAssemblyIdentity(string name, Version version, string culture, string publicKeyToken)
	{
		Name = name;
		Version = version;
		Culture = culture;
		PublicKeyToken = publicKeyToken;
	}

	public static ManagedAssemblyIdentity Create(MetadataReader metadata, AssemblyDefinition definition)
	{
		var name = new AssemblyName
		{
			Name = metadata.GetString(definition.Name),
			Version = definition.Version,
			CultureName = definition.Culture.IsNil ? null : metadata.GetString(definition.Culture),
		};
		var publicKey = metadata.GetBlobBytes(definition.PublicKey);
		if (publicKey.Length != 0)
			name.SetPublicKey(publicKey);
		return Create(name);
	}

	public static ManagedAssemblyIdentity Create(MetadataReader metadata, AssemblyReference reference)
	{
		var name = new AssemblyName
		{
			Name = metadata.GetString(reference.Name),
			Version = reference.Version,
			CultureName = reference.Culture.IsNil ? null : metadata.GetString(reference.Culture),
		};
		var keyOrToken = metadata.GetBlobBytes(reference.PublicKeyOrToken);
		if (keyOrToken.Length != 0)
		{
			if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
				name.SetPublicKey(keyOrToken);
			else
				name.SetPublicKeyToken(keyOrToken);
		}
		return Create(name);
	}

	public static bool TryCreate(AssemblyName name, out ManagedAssemblyIdentity identity)
	{
		try
		{
			identity = Create(name);
			return true;
		}
		catch (InvalidDataException)
		{
			identity = null!;
			return false;
		}
	}

	static ManagedAssemblyIdentity Create(AssemblyName name)
	{
		if (string.IsNullOrEmpty(name.Name) || name.Version == null ||
			!name.Name.IsNormalized(NormalizationForm.FormC))
			throw new InvalidDataException("Managed assembly identity has no canonical name or version.");
		var culture = name.CultureName ?? string.Empty;
		if (!culture.IsNormalized(NormalizationForm.FormC))
			throw new InvalidDataException("Managed assembly identity culture is not canonical.");
		var publicKeyToken = name.GetPublicKeyToken() ?? Array.Empty<byte>();
		if (publicKeyToken.Length is not 0 and not 8)
			throw new InvalidDataException("Managed assembly identity public-key token is malformed.");
		return new ManagedAssemblyIdentity(name.Name, name.Version, culture,
			Convert.ToHexString(publicKeyToken).ToLowerInvariant());
	}

	public bool Equals(ManagedAssemblyIdentity? other) => other != null &&
		string.Equals(Name, other.Name, StringComparison.Ordinal) && Version.Equals(other.Version) &&
		string.Equals(Culture, other.Culture, StringComparison.Ordinal) &&
		string.Equals(PublicKeyToken, other.PublicKeyToken, StringComparison.Ordinal);

	public override bool Equals(object? obj) => Equals(obj as ManagedAssemblyIdentity);

	public override int GetHashCode() => HashCode.Combine(Name, Version, Culture, PublicKeyToken);

	public override string ToString() => $"{Name}, Version={Version}, " +
		$"Culture={(Culture.Length == 0 ? "neutral" : Culture)}, " +
		$"PublicKeyToken={(PublicKeyToken.Length == 0 ? "null" : PublicKeyToken)}";
}

sealed class StableFileSnapshot
{
	const ushort FileTypeMask = 0xF000;
	const ushort RegularFileType = 0x8000;

	readonly ulong identityHigh;
	readonly ulong identityLow;
	readonly ushort mode;
	readonly long writeSeconds;
	readonly long writeNanoseconds;
	readonly long changeSeconds;
	readonly long changeNanoseconds;

	public bool IsRegular { get; }
	public long Length { get; }

	StableFileSnapshot(ulong identityHigh, ulong identityLow, ushort mode, bool isRegular,
		long length, long writeSeconds, long writeNanoseconds,
		long changeSeconds, long changeNanoseconds)
	{
		this.identityHigh = identityHigh;
		this.identityLow = identityLow;
		this.mode = mode;
		IsRegular = isRegular;
		Length = length;
		this.writeSeconds = writeSeconds;
		this.writeNanoseconds = writeNanoseconds;
		this.changeSeconds = changeSeconds;
		this.changeNanoseconds = changeNanoseconds;
	}

	public static StableFileSnapshot Capture(SafeFileHandle handle, string label)
	{
		if (OperatingSystem.IsMacOS())
		{
			if (NativeFile.DarwinFStat(handle.DangerousGetHandle().ToInt32(), out var status) != 0)
				throw NativeFile.Error($"Could not inspect {label} by its open handle.");
			return new StableFileSnapshot(unchecked((uint)status.Device), status.Inode, status.Mode,
				(status.Mode & FileTypeMask) == RegularFileType, status.Size,
				status.ModificationSeconds, status.ModificationNanoseconds,
				status.ChangeSeconds, status.ChangeNanoseconds);
		}

		if (OperatingSystem.IsLinux())
		{
			const uint RequiredMask = 0x3C1;
			if (NativeFile.LinuxStatx(handle.DangerousGetHandle().ToInt32(), string.Empty,
				NativeFile.AtEmptyPath, NativeFile.StatxBasicStats, out var status) != 0)
				throw NativeFile.Error($"Could not inspect {label} by its open handle.");
			if ((status.Mask & RequiredMask) != RequiredMask || status.Size > long.MaxValue)
				throw new InvalidDataException($"{label} has incomplete or unbounded regular-file metadata.");
			var device = ((ulong)status.DeviceMajor << 32) | status.DeviceMinor;
			return new StableFileSnapshot(device, status.Inode, status.Mode,
				(status.Mode & FileTypeMask) == RegularFileType, (long)status.Size,
				status.ModificationSeconds, status.ModificationNanoseconds,
				status.ChangeSeconds, status.ChangeNanoseconds);
		}

		if (OperatingSystem.IsWindows())
		{
			if (!NativeFile.GetFileInformationByHandle(handle, out var information))
				throw NativeFile.Error($"Could not inspect {label} by its open handle.");
			var unsignedLength = ((ulong)information.FileSizeHigh << 32) | information.FileSizeLow;
			var index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
			var attributes = (FileAttributes)information.FileAttributes;
			var isRegular = NativeFile.GetFileType(handle) == NativeFile.FileTypeDisk &&
				(attributes & (FileAttributes.Directory | FileAttributes.Device |
					FileAttributes.ReparsePoint)) == 0;
			return new StableFileSnapshot(information.VolumeSerialNumber, index, 0, isRegular,
				unsignedLength <= long.MaxValue ? (long)unsignedLength : -1,
				unchecked((long)(((ulong)information.LastWriteTime.High << 32) |
					information.LastWriteTime.Low)), 0,
				unchecked((long)(((ulong)information.CreationTime.High << 32) |
					information.CreationTime.Low)), 0);
		}

		throw new PlatformNotSupportedException("Secure regular-file identity checks are unsupported on this OS.");
	}

	public bool HasSameIdentity(StableFileSnapshot other) =>
		identityHigh == other.identityHigh && identityLow == other.identityLow;

	public bool HasStableContents(StableFileSnapshot other) =>
		HasSameIdentity(other) && mode == other.mode && IsRegular == other.IsRegular &&
		Length == other.Length && writeSeconds == other.writeSeconds &&
		writeNanoseconds == other.writeNanoseconds && changeSeconds == other.changeSeconds &&
		changeNanoseconds == other.changeNanoseconds;
}

static class SecureFile
{
	public static SafeFileHandle OpenReadNoFollow(string fullPath, string label)
	{
		if (OperatingSystem.IsWindows())
			return OpenWindows(fullPath, label);
		if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
			return OpenUnix(fullPath, label);
		throw new PlatformNotSupportedException("Secure no-follow file opens are unsupported on this OS.");
	}

	static SafeFileHandle OpenUnix(string fullPath, string label)
	{
		if (!Path.IsPathFullyQualified(fullPath))
			throw new InvalidDataException($"{label} path must be absolute.");
		var components = fullPath.Split(Path.DirectorySeparatorChar,
			StringSplitOptions.RemoveEmptyEntries);
		if (components.Length == 0)
			throw new InvalidDataException($"{label} path must name a file.");

		var noFollow = OperatingSystem.IsMacOS() ? 0x00000100 : 0x00020000;
		var directory = OperatingSystem.IsMacOS() ? 0x00100000 : 0x00010000;
		var closeOnExec = OperatingSystem.IsMacOS() ? 0x01000000 : 0x00080000;
		var nonBlocking = OperatingSystem.IsMacOS() ? 0x00000004 : 0x00000800;
		using var root = OpenUnixPath("/", directory | noFollow | closeOnExec, label);
		SafeFileHandle parent = root;
		var ownedParent = false;
		try
		{
			for (var i = 0; i < components.Length - 1; i++)
			{
				var next = OpenUnixAt(parent, components[i], directory | noFollow | closeOnExec, label);
				if (ownedParent)
					parent.Dispose();
				parent = next;
				ownedParent = true;
			}

			return OpenUnixAt(parent, components[^1], noFollow | closeOnExec | nonBlocking, label);
		}
		finally
		{
			if (ownedParent)
				parent.Dispose();
		}
	}

	static SafeFileHandle OpenUnixPath(string path, int flags, string label)
	{
		var descriptor = NativeFile.UnixOpen(path, flags);
		if (descriptor < 0)
			throw NativeFile.Error($"Could not securely open {label} without following symlinks.");
		return new SafeFileHandle((IntPtr)descriptor, true);
	}

	static SafeFileHandle OpenUnixAt(SafeFileHandle parent, string name, int flags, string label)
	{
		var descriptor = NativeFile.UnixOpenAt(parent.DangerousGetHandle().ToInt32(), name, flags);
		if (descriptor < 0)
			throw NativeFile.Error($"Could not securely open {label} without following symlinks.");
		return new SafeFileHandle((IntPtr)descriptor, true);
	}

	static SafeFileHandle OpenWindows(string fullPath, string label)
	{
		var rootPath = Path.GetPathRoot(fullPath);
		if (string.IsNullOrEmpty(rootPath))
			throw new InvalidDataException($"{label} path must be absolute.");
		var components = fullPath[rootPath.Length..].Split(Path.DirectorySeparatorChar,
			StringSplitOptions.RemoveEmptyEntries);
		if (components.Length == 0)
			throw new InvalidDataException($"{label} path must name a file.");

		var parents = new List<SafeFileHandle>();
		try
		{
			var current = rootPath;
			for (var i = 0; i < components.Length - 1; i++)
			{
				current = Path.Combine(current, components[i]);
				var parent = NativeFile.CreateFile(current, NativeFile.FileReadAttributes,
					(uint)(FileShare.Read | FileShare.Write), IntPtr.Zero, FileMode.Open,
					NativeFile.FileFlagBackupSemantics | NativeFile.FileFlagOpenReparsePoint, IntPtr.Zero);
				if (parent.IsInvalid)
				{
					var error = Marshal.GetLastPInvokeError();
					parent.Dispose();
					throw NativeFile.Error($"Could not securely open a parent directory of {label}.", error);
				}
				var attributes = File.GetAttributes(parent);
				if ((attributes & FileAttributes.Directory) == 0 ||
					(attributes & FileAttributes.ReparsePoint) != 0)
				{
					parent.Dispose();
					throw new InvalidDataException($"{label} has a non-directory or symlink parent.");
				}
				parents.Add(parent);
			}

			var leaf = NativeFile.CreateFile(fullPath, NativeFile.GenericRead,
				(uint)(FileShare.Read | FileShare.Delete), IntPtr.Zero, FileMode.Open,
				NativeFile.FileFlagSequentialScan | NativeFile.FileFlagOpenReparsePoint, IntPtr.Zero);
			if (leaf.IsInvalid)
			{
				var error = Marshal.GetLastPInvokeError();
				leaf.Dispose();
				throw NativeFile.Error($"Could not securely open {label} without following symlinks.", error);
			}
			if ((File.GetAttributes(leaf) & FileAttributes.ReparsePoint) != 0)
			{
				leaf.Dispose();
				throw new InvalidDataException($"{label} must not be a symlink or reparse point.");
			}
			return leaf;
		}
		finally
		{
			foreach (var parent in parents)
				parent.Dispose();
		}
	}
}

static class NativeFile
{
	public const int AtEmptyPath = 0x1000;
	public const uint StatxBasicStats = 0x07FF;
	public const uint FileTypeDisk = 0x0001;
	public const uint GenericRead = 0x80000000;
	public const uint FileReadAttributes = 0x00000080;
	public const uint FileFlagOpenReparsePoint = 0x00200000;
	public const uint FileFlagBackupSemantics = 0x02000000;
	public const uint FileFlagSequentialScan = 0x08000000;

	public static InvalidDataException Error(string message) => Error(message, Marshal.GetLastPInvokeError());

	public static InvalidDataException Error(string message, int error) =>
		new(message, new Win32Exception(error));

	[DllImport("libc", EntryPoint = "open", SetLastError = true)]
	public static extern int UnixOpen(string path, int flags);

	[DllImport("libc", EntryPoint = "openat", SetLastError = true)]
	public static extern int UnixOpenAt(int directory, string path, int flags);

	[DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
	public static extern int DarwinFStat(int descriptor, out DarwinStat status);

	[DllImport("libc", EntryPoint = "statx", SetLastError = true)]
	public static extern int LinuxStatx(int directory, string path, int flags, uint mask,
		out LinuxStatxBuffer status);

	[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
		SetLastError = true)]
	public static extern SafeFileHandle CreateFile(string path, uint desiredAccess, uint shareMode,
		IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes,
		IntPtr templateFile);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetFileInformationByHandle(SafeFileHandle handle,
		out ByHandleFileInformation information);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern uint GetFileType(SafeFileHandle handle);

	[StructLayout(LayoutKind.Explicit, Size = 144)]
	public struct DarwinStat
	{
		[FieldOffset(0)] public int Device;
		[FieldOffset(4)] public ushort Mode;
		[FieldOffset(8)] public ulong Inode;
		[FieldOffset(48)] public long ModificationSeconds;
		[FieldOffset(56)] public long ModificationNanoseconds;
		[FieldOffset(64)] public long ChangeSeconds;
		[FieldOffset(72)] public long ChangeNanoseconds;
		[FieldOffset(96)] public long Size;
	}

	[StructLayout(LayoutKind.Explicit, Size = 256)]
	public struct LinuxStatxBuffer
	{
		[FieldOffset(0)] public uint Mask;
		[FieldOffset(28)] public ushort Mode;
		[FieldOffset(32)] public ulong Inode;
		[FieldOffset(40)] public ulong Size;
		[FieldOffset(96)] public long ChangeSeconds;
		[FieldOffset(104)] public uint ChangeNanoseconds;
		[FieldOffset(112)] public long ModificationSeconds;
		[FieldOffset(120)] public uint ModificationNanoseconds;
		[FieldOffset(136)] public uint DeviceMajor;
		[FieldOffset(140)] public uint DeviceMinor;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct FileTime
	{
		public uint Low;
		public uint High;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ByHandleFileInformation
	{
		public uint FileAttributes;
		public FileTime CreationTime;
		public FileTime LastAccessTime;
		public FileTime LastWriteTime;
		public uint VolumeSerialNumber;
		public uint FileSizeHigh;
		public uint FileSizeLow;
		public uint NumberOfLinks;
		public uint FileIndexHigh;
		public uint FileIndexLow;
	}
}

sealed class GoldenDescriptor
{
	public string SourcePath { get; }
	public string LogicalName { get; }
	public string BundleRelativePath { get; }
	public string[] Profiles { get; }
	public string Purpose { get; }

	public GoldenDescriptor(string sourcePath, string logicalName, string bundleRelativePath,
		string[] profiles, string purpose)
	{
		SourcePath = sourcePath;
		LogicalName = logicalName;
		BundleRelativePath = bundleRelativePath;
		Profiles = profiles;
		Purpose = purpose;
	}
}

sealed class EvaluatedDescriptor
{
	public string SourceIdentity { get; }
	public string LogicalName { get; }
	public string BundleRelativePath { get; }
	public string Profiles { get; }
	public string Purpose { get; }

	public EvaluatedDescriptor(string sourceIdentity, string logicalName, string bundleRelativePath,
		string profiles, string purpose)
	{
		SourceIdentity = sourceIdentity;
		LogicalName = logicalName;
		BundleRelativePath = bundleRelativePath;
		Profiles = profiles;
		Purpose = purpose;
	}
}

sealed class HashedDescriptor
{
	public string LogicalName { get; }
	public string BundleRelativePath { get; }
	public long Length { get; }
	public string Sha256 { get; }
	public string[] Profiles { get; }
	public string Purpose { get; }

	public HashedDescriptor(string logicalName, string bundleRelativePath, long length,
		string sha256, string[] profiles, string purpose)
	{
		LogicalName = logicalName;
		BundleRelativePath = bundleRelativePath;
		Length = length;
		Sha256 = sha256;
		Profiles = profiles;
		Purpose = purpose;
	}
}

enum AotRegistrationKind : uint
{
	Basic = 1,
	UseCtor = 2,
}

sealed class AotRegistration
{
	public string TypeFullName { get; }
	public string TypeExpression { get; }
	public AotRegistrationKind Kind { get; }
	public AotParameter[] Parameters { get; }

	public AotRegistration(string typeFullName, string typeExpression, AotRegistrationKind kind,
		AotParameter[] parameters)
	{
		TypeFullName = typeFullName;
		TypeExpression = typeExpression;
		Kind = kind;
		Parameters = parameters;
	}
}

sealed class AotParameter
{
	public string Name { get; }
	public string TypeExpression { get; }

	public AotParameter(string name, string typeExpression)
	{
		Name = name;
		TypeExpression = typeExpression;
	}
}
