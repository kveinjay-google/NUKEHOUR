#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
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
using OpenRA.Primitives;

namespace OpenRA
{
	public sealed class ObjectCreator : IDisposable
	{
		public static Func<string, object> LoaderFactoryOverride { get; set; }
		// .NET does not support unloading assemblies, so mod libraries will leak across mod changes.
		// This tracks the assemblies that have been loaded since game start so that we don't load multiple copies
		static readonly Dictionary<string, Assembly> ResolvedAssemblies = new();

		readonly Cache<string, Type> typeCache;
		readonly Cache<Type, ConstructorInfo> ctorCache;
		readonly (Assembly Assembly, string Namespace)[] assemblies;

		public ObjectCreator(Manifest manifest, InstalledMods mods)
		{
			typeCache = new Cache<string, Type>(FindType);
			ctorCache = new Cache<Type, ConstructorInfo>(GetCtor);

			// Allow mods to load types from the core Game assembly, and any additional assemblies they specify.
			// Assemblies must exist in the game binary directory next to the main game executable.
			var assemblyList = new List<Assembly>() { typeof(Game).Assembly };
			foreach (var filename in manifest.Assemblies)
				LoadAssembly(assemblyList, Path.Combine(Platform.BinDir, filename));

			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
			assemblies = assemblyList.SelectMany(asm => asm.GetNamespaces().Select(ns => (asm, ns))).ToArray();
		}

		static void LoadAssembly(List<Assembly> assemblyList, string resolvedPath)
		{
			// iOS bundles mod assemblies into the application and does not support
			// unloading or duplicating them through a separate load context. Loading
			// a second copy gives shared trait types different identities, breaking
			// ruleset checks such as MobileInfo lookups.
			if (OperatingSystem.IsIOS())
			{
				var assemblyName = Path.GetFileNameWithoutExtension(resolvedPath);
				var bundledAssembly = AppDomain.CurrentDomain.GetAssemblies()
					.FirstOrDefault(a => a.GetName().Name == assemblyName);
				if (bundledAssembly == null)
					throw new FileNotFoundException($"The bundled iOS mod assembly '{assemblyName}' was not loaded.", resolvedPath);

				assemblyList.Add(bundledAssembly);
				return;
			}

			// .NET doesn't provide any way of querying the metadata of an assembly without either:
			//   (a) loading duplicate data into the application domain, breaking the world.
			//   (b) crashing if the assembly has already been loaded.
			// We can't check the internal name of the assembly, so we'll work off the data instead
			string hash;
			using (var stream = File.OpenRead(resolvedPath))
				hash = CryptoUtil.SHA1Hash(stream);

			if (!ResolvedAssemblies.TryGetValue(hash, out var assembly))
			{
#if NET5_0_OR_GREATER
				var loader = new Support.AssemblyLoader(resolvedPath);
				assembly = loader.LoadDefaultAssembly();
				ResolvedAssemblies.Add(hash, assembly);
#else
				assembly = Assembly.LoadFile(resolvedPath);
				ResolvedAssemblies.Add(hash, assembly);

				// Allow mods to use libraries.
				var assemblyPath = Path.GetDirectoryName(resolvedPath);
				if (assemblyPath != null)
				{
					foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
					{
						var depedencyPath = Path.Combine(assemblyPath, referencedAssembly.Name + ".dll");
						if (File.Exists(depedencyPath))
							LoadAssembly(assemblyList, depedencyPath);
					}
				}
#endif
			}

			assemblyList.Add(assembly);
		}

		Assembly ResolveAssembly(object sender, ResolveEventArgs e)
		{
			foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
				if (a.FullName == e.Name)
					return a;

			return assemblies?.Select(a => a.Assembly).FirstOrDefault(a => a.FullName == e.Name);
		}

		// Only used by the linter to prevent exceptions from being thrown during a lint run
		public static Action<string> MissingTypeAction = null;

		public T CreateObject<T>(string className)
		{
			return CreateObject<T>(className, new Dictionary<string, object>());
		}

		public T CreateObject<T>(string className, Dictionary<string, object> args)
		{
			var type = typeCache[className];
			if (type == null)
			{
				// HACK: The linter does not want to crash but only print an error instead
				if (MissingTypeAction != null)
					MissingTypeAction(className);
				else
					throw new InvalidOperationException($"Cannot locate type: {className}");

				return default;
			}

			var ctor = ctorCache[type];
			if (ctor == null)
				return (T)CreateBasic(type);
			else
				return (T)CreateUsingArgs(ctor, args);
		}

		public Type FindType(string className)
		{
			return assemblies
				.Select(pair => pair.Assembly.GetType(pair.Namespace + "." + className, false))
				.FirstOrDefault(t => t != null);
		}

		public ConstructorInfo GetCtor(Type type)
		{
			const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
			var ctors = type.GetConstructors(Flags).Where(x => x.HasAttribute<UseCtorAttribute>()).ToList();
			if (ctors.Count > 1)
				throw new InvalidOperationException("ObjectCreator: UseCtor on multiple constructors; invalid.");
			return ctors.FirstOrDefault();
		}

		public object CreateBasic(Type type)
		{
			if (OperatingSystem.IsIOS() && GeneratedAotObjectRegistry.TryCreateBasic(type, out var value))
				return value;

			return type.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>());
		}

		public object CreateUsingArgs(ConstructorInfo ctor, Dictionary<string, object> args)
		{
			if (OperatingSystem.IsIOS() && GeneratedAotObjectRegistry.TryCreateWithArgs(ctor.DeclaringType, args, out var value))
				return value;

			var p = ctor.GetParameters();
			var a = new object[p.Length];
			for (var i = 0; i < p.Length; i++)
			{
				var key = p[i].Name;
				if (!args.TryGetValue(key, out var arg)) throw new InvalidOperationException($"ObjectCreator: key `{key}' not found");
				a[i] = arg;
			}

			return ctor.Invoke(a);
		}

		public IEnumerable<Type> GetTypesImplementing<T>()
		{
			var it = typeof(T);
			return GetTypes().Where(t => t != it && it.IsAssignableFrom(t));
		}

		public IEnumerable<Type> GetTypes()
		{
			return assemblies.Select(ma => ma.Assembly).Distinct()
				.SelectMany(ma => ma.GetTypes());
		}

		public TLoader GetLoader<TLoader>(string format, string name)
		{
			var overriddenLoader = LoaderFactoryOverride?.Invoke(format);
			if (overriddenLoader != null)
				return (TLoader)overriddenLoader;

			var loader = FindType(format + "Loader");
			if (loader == null || !loader.GetInterfaces().Contains(typeof(TLoader)))
				throw new InvalidOperationException($"Unable to find a {name} loader for type '{format}'.");

			return (TLoader)CreateBasic(loader);
		}

		public TLoader[] GetLoaders<TLoader>(IEnumerable<string> formats, string name)
		{
			var loaders = new List<TLoader>();
			foreach (var format in formats)
				loaders.Add(GetLoader<TLoader>(format, name));

			return loaders.ToArray();
		}

		~ObjectCreator()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		void Dispose(bool disposing)
		{
			if (disposing)
				AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
		}

		[AttributeUsage(AttributeTargets.Constructor)]
		public sealed class UseCtorAttribute : Attribute { }
	}
}
