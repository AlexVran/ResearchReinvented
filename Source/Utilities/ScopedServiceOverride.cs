using System;

namespace PeteTimesSix.ResearchReinvented.Utilities
{
	internal sealed class ScopedServiceOverride<TService> where TService : class
	{
		private TService current;

		internal ScopedServiceOverride(TService defaultService)
		{
			current = defaultService ?? throw new ArgumentNullException(nameof(defaultService));
		}

		internal TService Current => current;

		internal IDisposable Push(TService replacement)
		{
			if (replacement == null)
				throw new ArgumentNullException(nameof(replacement));

			var previous = current;
			current = replacement;
			return new RestoreScope(this, previous, replacement);
		}

		private sealed class RestoreScope : IDisposable
		{
			private readonly ScopedServiceOverride<TService> owner;
			private readonly TService previous;
			private readonly TService replacement;
			private bool disposed;

			internal RestoreScope(ScopedServiceOverride<TService> owner, TService previous, TService replacement)
			{
				this.owner = owner;
				this.previous = previous;
				this.replacement = replacement;
			}

			public void Dispose()
			{
				if (disposed)
					return;

				if (ReferenceEquals(owner.current, replacement))
					owner.current = previous;
				disposed = true;
			}
		}
	}
}
