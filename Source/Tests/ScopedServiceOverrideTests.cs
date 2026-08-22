using PeteTimesSix.ResearchReinvented.Utilities;
using Xunit;

namespace PeteTimesSix.ResearchReinvented.Tests;

public sealed class ScopedServiceOverrideTests
{
	[Fact]
	public void OverrideRestoresTheDefaultService()
	{
		var defaultService = new object();
		var replacement = new object();
		var services = new ScopedServiceOverride<object>(defaultService);

		Assert.Same(defaultService, services.Current);
		using (services.Push(replacement))
			Assert.Same(replacement, services.Current);
		Assert.Same(defaultService, services.Current);
	}

	[Fact]
	public void NestedOverridesRestoreInLastInFirstOutOrder()
	{
		var defaultService = new object();
		var first = new object();
		var second = new object();
		var services = new ScopedServiceOverride<object>(defaultService);

		using (services.Push(first))
		{
			Assert.Same(first, services.Current);
			using (services.Push(second))
				Assert.Same(second, services.Current);
			Assert.Same(first, services.Current);
		}
		Assert.Same(defaultService, services.Current);
	}

	[Fact]
	public void NullServicesAreRejected()
	{
		Assert.Throws<ArgumentNullException>(() => new ScopedServiceOverride<object>(null!));

		var services = new ScopedServiceOverride<object>(new object());
		Assert.Throws<ArgumentNullException>(() => services.Push(null!));
	}
}
