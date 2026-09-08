using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenRA.Mods.RA2.Content;
using OpenRA.Test;

const string CrashRootVariable = "RETAIL_IMPORT_CRASH_ROOT";
const string ScenarioNonceVariable = "RETAIL_IMPORT_SCENARIO_NONCE";
const string RecoveryOrdinalVariable = "RETAIL_IMPORT_RECOVERY_ORDINAL";
const string SyntheticRootLeaf = "crash-root";
const string OwnerMarkerMagic = "NHRO1";

try
{
	if (!TryParseArguments(args, out var verb, out var root, out var failpoint))
		return 64;
	if (!VerifyRoot(root, verb) || !VerifyAssemblySupply())
		return 65;

	if (verb == "crash")
	{
		RetailImportCrashHarnessDriver.Crash(root, failpoint);
		return 0;
	}

	RetailImportCrashHarnessDriver.Recover(root);
	return 0;
}
catch
{
	return 70;
}

static bool TryParseArguments(
	string[] arguments,
	out string verb,
	out string root,
	out string failpoint)
{
	verb = null;
	root = null;
	failpoint = null;
	if (arguments.Length == 3 && StringComparer.Ordinal.Equals(arguments[0], "crash"))
	{
		if (!Enum.TryParse<RetailImportFailpoint>(arguments[2], ignoreCase: false, out var parsed) ||
			!StringComparer.Ordinal.Equals(Enum.GetName(parsed), arguments[2]))
			return false;

		verb = "crash";
		root = arguments[1];
		failpoint = arguments[2];
		return true;
	}

	if (arguments.Length == 2 && StringComparer.Ordinal.Equals(arguments[0], "recover"))
	{
		verb = "recover";
		root = arguments[1];
		return true;
	}

	return false;
}

static bool VerifyRoot(string suppliedRoot, string verb)
{
	var expectedRoot = Environment.GetEnvironmentVariable(CrashRootVariable);
	var scenarioNonce = Environment.GetEnvironmentVariable(ScenarioNonceVariable);
	var recoveryOrdinal = Environment.GetEnvironmentVariable(RecoveryOrdinalVariable);
	if (string.IsNullOrEmpty(expectedRoot) || string.IsNullOrEmpty(suppliedRoot) ||
		!Path.IsPathFullyQualified(expectedRoot) || !Path.IsPathFullyQualified(suppliedRoot) ||
		!IsLowercaseNonce(scenarioNonce) ||
		(verb == "crash" && recoveryOrdinal != null) ||
		(verb == "recover" && recoveryOrdinal is not ("1" or "2")))
		return false;

	try
	{
		var parent = Path.GetDirectoryName(expectedRoot);
		var marker = expectedRoot + ".owner";
		if (!StringComparer.Ordinal.Equals(Path.GetFullPath(expectedRoot), expectedRoot) ||
			!StringComparer.Ordinal.Equals(Path.GetFullPath(suppliedRoot), suppliedRoot) ||
			!StringComparer.Ordinal.Equals(expectedRoot, suppliedRoot) ||
			!StringComparer.Ordinal.Equals(Path.GetFileName(expectedRoot), SyntheticRootLeaf) ||
			string.IsNullOrEmpty(parent) || !Directory.Exists(parent) ||
			!IsDedicatedTemporaryParent(parent) || ContainsSymbolicLink(parent) ||
			!IsOwnerOnly(parent) || File.Exists(expectedRoot) ||
			(Directory.Exists(expectedRoot) && ContainsSymbolicLink(expectedRoot)) ||
			!IsOwnerOnlyRegularFile(marker))
			return false;

		return File.ReadAllLines(marker).SequenceEqual(new[]
		{
			OwnerMarkerMagic,
			scenarioNonce,
			SyntheticRootLeaf,
		}, StringComparer.Ordinal);
	}
	catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
		ArgumentException or NotSupportedException)
	{
		return false;
	}
}

static bool IsDedicatedTemporaryParent(string path)
{
	var candidate = Path.GetFullPath(path);
	var basename = Path.GetFileName(candidate);
	if (string.IsNullOrEmpty(basename) ||
		!basename.StartsWith("nuke-hour-retail-task7.", StringComparison.Ordinal))
		return false;

	var roots = new[] { Path.GetFullPath(Path.GetTempPath()), "/private/tmp" }
		.Distinct(StringComparer.Ordinal);
	foreach (var root in roots)
	{
		var prefix = root.EndsWith(Path.DirectorySeparatorChar) ?
			root : root + Path.DirectorySeparatorChar;
		if (candidate.StartsWith(prefix, StringComparison.Ordinal) &&
			!StringComparer.Ordinal.Equals(candidate, root))
			return true;
	}

	return false;
}

static bool IsLowercaseNonce(string value) => value != null && value.Length == 32 &&
	value.Any(character => character is >= '1' and <= '9' or >= 'a' and <= 'f') &&
	value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

static bool VerifyAssemblySupply()
{
	var harness = RequireAssembly(
		"RETAIL_IMPORT_CRASH_HARNESS_DLL",
		"RETAIL_IMPORT_CRASH_HARNESS_SHA256",
		"OpenRA.RetailImport.CrashHarness.dll");
	var test = RequireAssembly(
		"RETAIL_IMPORT_OPENRA_TEST_DLL",
		"RETAIL_IMPORT_OPENRA_TEST_SHA256",
		"OpenRA.Test.dll");
	var ra2 = RequireAssembly(
		"RETAIL_IMPORT_RA2_DLL",
		"RETAIL_IMPORT_RA2_SHA256",
		"OpenRA.Mods.RA2.dll");
	if (harness == null || test == null || ra2 == null)
		return false;

	var directory = Path.GetDirectoryName(harness);
	return StringComparer.Ordinal.Equals(Path.GetDirectoryName(test), directory) &&
		StringComparer.Ordinal.Equals(Path.GetDirectoryName(ra2), directory) &&
		StringComparer.Ordinal.Equals(CanonicalLocation(Assembly.GetExecutingAssembly()), harness) &&
		StringComparer.Ordinal.Equals(CanonicalLocation(typeof(RetailImportCrashHarnessDriver).Assembly), test) &&
		StringComparer.Ordinal.Equals(CanonicalLocation(typeof(RetailContentGroup).Assembly), ra2);
}

static string RequireAssembly(string pathVariable, string shaVariable, string expectedBasename)
{
	var path = Environment.GetEnvironmentVariable(pathVariable);
	var expectedSha = Environment.GetEnvironmentVariable(shaVariable);
	if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(expectedSha) ||
		expectedSha.Length != 64 || expectedSha.Any(character =>
			!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
		return null;

	try
	{
		if (!Path.IsPathFullyQualified(path) || !StringComparer.Ordinal.Equals(Path.GetFullPath(path), path) ||
			!StringComparer.Ordinal.Equals(Path.GetFileName(path), expectedBasename) ||
			!File.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) ||
			ContainsSymbolicLink(path))
			return null;

		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		using var hash = SHA256.Create();
		var actualSha = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
		return StringComparer.Ordinal.Equals(actualSha, expectedSha) ? path : null;
	}
	catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
		ArgumentException or NotSupportedException)
	{
		return null;
	}
}

static string CanonicalLocation(Assembly assembly)
{
	try
	{
		return string.IsNullOrEmpty(assembly.Location) ? null : Path.GetFullPath(assembly.Location);
	}
	catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
		ArgumentException or NotSupportedException)
	{
		return null;
	}
}

static bool ContainsSymbolicLink(string path)
{
	var current = new DirectoryInfo(path);
	while (current != null)
	{
		if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
			return true;
		current = current.Parent;
	}

	return false;
}

static bool IsOwnerOnlyRegularFile(string path)
{
	if (!File.Exists(path) || Directory.Exists(path) ||
		File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || ContainsSymbolicLink(path))
		return false;

	return IsOwnerOnly(path);
}

static bool IsOwnerOnly(string path)
{
	if (OperatingSystem.IsWindows())
		return true;

	return UnixMode.TryRead(path, Directory.Exists(path), out var mode) && (mode & 0x3Fu) == 0;
}

static class UnixMode
{
	const int DarwinDirectoryFlags = 0x1100100;
	const int DarwinFileFlags = 0x1000104;
	const int LinuxDirectoryFlags = 0xB0000;
	const int LinuxFileFlags = 0xA0800;

	internal static bool TryRead(string path, bool directory, out uint mode)
	{
		mode = 0;
		var isDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
		var architecture = RuntimeInformation.ProcessArchitecture;
		int modeOffset;
		int modeWidth;
		int flags;
		if (isDarwin && architecture is Architecture.Arm64 or Architecture.X64)
		{
			modeOffset = 4;
			modeWidth = 2;
			flags = directory ? DarwinDirectoryFlags : DarwinFileFlags;
		}
		else if (isLinux && architecture == Architecture.Arm64)
		{
			modeOffset = 16;
			modeWidth = 4;
			flags = directory ? LinuxDirectoryFlags : LinuxFileFlags;
		}
		else if (isLinux && architecture == Architecture.X64)
		{
			modeOffset = 24;
			modeWidth = 4;
			flags = directory ? LinuxDirectoryFlags : LinuxFileFlags;
		}
		else
			return false;

		var descriptor = Open(path, flags, 0);
		if (descriptor < 0)
			return false;

		var metadata = Marshal.AllocHGlobal(256);
		try
		{
			for (var offset = 0; offset < 256; offset += sizeof(long))
				Marshal.WriteInt64(metadata, offset, 0);

			if (FStat(descriptor, metadata, isDarwin, architecture) != 0)
				return false;

			mode = modeWidth == 2 ? unchecked((ushort)Marshal.ReadInt16(metadata, modeOffset)) :
				unchecked((uint)Marshal.ReadInt32(metadata, modeOffset));
			return true;
		}
		finally
		{
			Marshal.FreeHGlobal(metadata);
			Close(descriptor);
		}
	}

	static int FStat(int descriptor, IntPtr metadata, bool isDarwin, Architecture architecture) =>
		isDarwin && architecture == Architecture.X64 ?
			FStatDarwinInode64(descriptor, metadata) : FStatBare(descriptor, metadata);

	[DllImport("libc", EntryPoint = "open", SetLastError = true)]
	static extern int Open(string path, int flags, int mode);

	[DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
	static extern int FStatBare(int descriptor, IntPtr metadata);

	[DllImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
	static extern int FStatDarwinInode64(int descriptor, IntPtr metadata);

	[DllImport("libc", EntryPoint = "close", SetLastError = true)]
	static extern int Close(int descriptor);
}
