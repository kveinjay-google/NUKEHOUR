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

using System.Collections.Generic;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class GeneratedAotObjectRegistryTest
	{
		sealed class BasicTarget { }

		sealed class ArgsTarget
		{
			public int Value { get; }
			public ArgsTarget(int value) { Value = value; }
		}

		[Test]
		public void CreatesBasicObjectUsingDirectFactory()
		{
			GeneratedAotObjectRegistry.RegisterBasic(typeof(BasicTarget), static () => new BasicTarget());

			Assert.That(GeneratedAotObjectRegistry.TryCreateBasic(typeof(BasicTarget), out var value), Is.True);
			Assert.That(value, Is.TypeOf<BasicTarget>());
		}

		[Test]
		public void CreatesUseCtorObjectUsingNamedArguments()
		{
			GeneratedAotObjectRegistry.RegisterWithArgs(typeof(ArgsTarget),
				static args => new ArgsTarget((int)args["value"]));

			Assert.That(GeneratedAotObjectRegistry.TryCreateWithArgs(typeof(ArgsTarget),
				new Dictionary<string, object> { ["value"] = 42 }, out var value), Is.True);
			Assert.That(((ArgsTarget)value).Value, Is.EqualTo(42));
		}

		[Test]
		public void MissingTypeReturnsFalseWithoutReflectionFallback()
		{
			Assert.That(GeneratedAotObjectRegistry.TryCreateBasic(typeof(GeneratedAotObjectRegistryTest), out _), Is.False);
		}
	}
}
