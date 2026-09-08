#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA2.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AotObjectRegistryCoverageTest
	{
		[Test]
		public void GeneratedRegistryCoversEveryConstructibleEngineAndModType()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.iOS", "GeneratedAotObjectRegistry.g.cs"));
			var missing = new List<string>();
			var assemblies = new[]
			{
				typeof(Game).Assembly,
				typeof(MobileInfo).Assembly,
				typeof(MixLoader).Assembly,
				typeof(IosHighUnitBenchmarkInfo).Assembly
			};

			foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(RequiresFactory))
				if (!source.Contains($"\"{type.FullName}\"", StringComparison.Ordinal))
					missing.Add(type.FullName);

			Assert.That(missing, Is.Empty,
				"Regenerate the iOS AOT object registry. Missing types: " + string.Join(", ", missing));
		}

		static bool RequiresFactory(Type type)
		{
			if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters ||
				type.Namespace == "Microsoft.CodeAnalysis" || typeof(Attribute).IsAssignableFrom(type) || !CanName(type))
				return false;

			const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var useCtor = type.GetConstructors(Flags)
				.SingleOrDefault(c => c.GetCustomAttributesData()
					.Any(a => a.AttributeType.FullName == "OpenRA.ObjectCreator+UseCtorAttribute"));
			if (useCtor != null)
				return CanCall(useCtor);

			var basic = type.GetConstructor(Flags, null, Type.EmptyTypes, null);
			return basic != null && CanCall(basic);
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

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
		}
	}
}
