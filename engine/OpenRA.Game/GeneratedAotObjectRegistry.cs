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

namespace OpenRA
{
	public static class GeneratedAotObjectRegistry
	{
		static readonly Dictionary<Type, Func<object>> BasicFactories = new();
		static readonly Dictionary<Type, Func<Dictionary<string, object>, object>> ArgumentFactories = new();

		public static void RegisterBasic(Type type, Func<object> factory)
		{
			BasicFactories[type ?? throw new ArgumentNullException(nameof(type))] =
				factory ?? throw new ArgumentNullException(nameof(factory));
		}

		public static void RegisterWithArgs(Type type, Func<Dictionary<string, object>, object> factory)
		{
			ArgumentFactories[type ?? throw new ArgumentNullException(nameof(type))] =
				factory ?? throw new ArgumentNullException(nameof(factory));
		}

		public static bool TryCreateBasic(Type type, out object value)
		{
			if (BasicFactories.TryGetValue(type, out var factory))
			{
				value = factory();
				return true;
			}

			value = null;
			return false;
		}

		public static bool TryCreateWithArgs(Type type, Dictionary<string, object> args, out object value)
		{
			if (ArgumentFactories.TryGetValue(type, out var factory))
			{
				value = factory(args);
				return true;
			}

			value = null;
			return false;
		}
	}
}
