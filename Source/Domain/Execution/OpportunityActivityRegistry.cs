#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PeteTimesSix.ResearchReinvented.Domain.State;

namespace PeteTimesSix.ResearchReinvented.Domain.Execution
{
	public static class BuiltInActivityHandlerIds
	{
		public static readonly ActivityHandlerId Theory = new("rr.theory");
		public static readonly ActivityHandlerId AnalysisBench = new("rr.analysis.bench");
		public static readonly ActivityHandlerId AnalysisFieldThing = new("rr.analysis.field.thing");
		public static readonly ActivityHandlerId AnalysisFieldTerrain = new("rr.analysis.field.terrain");
		public static readonly ActivityHandlerId Social = new("rr.social");
		public static readonly ActivityHandlerId MedicineTend = new("rr.clinical.medicine.tend");
		public static readonly ActivityHandlerId MedicineSurgery = new("rr.clinical.medicine.surgery");
		public static readonly ActivityHandlerId Ingest = new("rr.clinical.ingest");
		public static readonly ActivityHandlerId ObserveIngest = new("rr.clinical.observe-ingest");
		public static readonly ActivityHandlerId Books = new("rr.books");
		public static readonly ActivityHandlerId Tooling = new("rr.tooling");

		public static IReadOnlyList<ActivityHandlerId> All { get; } = Array.AsReadOnly(new[]
		{
			Theory, AnalysisBench, AnalysisFieldThing, AnalysisFieldTerrain, Social,
			MedicineTend, MedicineSurgery, Ingest, ObserveIngest, Books, Tooling,
		});
	}

	public sealed class ActivityHandlerId : IEquatable<ActivityHandlerId>, IComparable<ActivityHandlerId>
	{
		public ActivityHandlerId(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException("Activity handler IDs cannot be empty.", nameof(value));
			Value = value.Trim().Normalize();
		}

		public string Value { get; }
		public bool Equals(ActivityHandlerId? other) => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
		public override bool Equals(object? obj) => Equals(obj as ActivityHandlerId);
		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
		public int CompareTo(ActivityHandlerId? other) => other == null ? 1 : string.Compare(Value, other.Value, StringComparison.Ordinal);
		public override string ToString() => Value;
		public static bool operator ==(ActivityHandlerId? left, ActivityHandlerId? right) => Equals(left, right);
		public static bool operator !=(ActivityHandlerId? left, ActivityHandlerId? right) => !Equals(left, right);
	}

	public enum ActivityQueryStatus
	{
		Available,
		NoMatch,
		MissingHandler,
		DisabledHandler,
		FailedHandler
	}

	public sealed class ActivityHandlerSelfCheck
	{
		private ActivityHandlerSelfCheck(bool available, string? reason)
		{
			Available = available;
			Reason = reason;
		}

		public bool Available { get; }
		public string? Reason { get; }
		public static ActivityHandlerSelfCheck Ready() => new(true, null);
		public static ActivityHandlerSelfCheck Unavailable(string reason) => new(false, Required(reason));
		private static string Required(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("An unavailable handler needs a reason.", nameof(value)) : value;
	}

	public sealed class ActivityHandlerQuery
	{
		public ActivityHandlerQuery(
			DefIdentity project,
			IEnumerable<OpportunitySpecificationState> specifications,
			Func<OpportunitySpecificationState, bool>? runtimeFilter = null)
		{
			Project = project ?? throw new ArgumentNullException(nameof(project));
			Specifications = new ReadOnlyCollection<OpportunitySpecificationState>((specifications ?? throw new ArgumentNullException(nameof(specifications)))
				.Where(item => item.Spec.Project == project)
				.OrderBy(item => item.Spec.Key)
				.ToArray());
			RuntimeFilter = runtimeFilter;
		}

		public DefIdentity Project { get; }
		public IReadOnlyList<OpportunitySpecificationState> Specifications { get; }
		public Func<OpportunitySpecificationState, bool>? RuntimeFilter { get; }
	}

	public interface IOpportunityActivityHandler
	{
		ActivityHandlerId Id { get; }
		ActivityHandlerSelfCheck StartupCheck();
		IEnumerable<OpportunityKey> Select(ActivityHandlerQuery query);
	}

	public sealed class ActivityQueryResult
	{
		public ActivityQueryResult(ActivityHandlerId handlerId, ActivityQueryStatus status, IEnumerable<OpportunityKey>? keys, string? unavailableReason = null)
		{
			HandlerId = handlerId ?? throw new ArgumentNullException(nameof(handlerId));
			Status = status;
			Keys = new ReadOnlyCollection<OpportunityKey>((keys ?? Array.Empty<OpportunityKey>())
				.Where(key => key != null)
				.Distinct()
				.OrderBy(key => key)
				.ToArray());
			UnavailableReason = unavailableReason;
		}

		public ActivityHandlerId HandlerId { get; }
		public ActivityQueryStatus Status { get; }
		public IReadOnlyList<OpportunityKey> Keys { get; }
		public string? UnavailableReason { get; }
		public bool IsHandlerAvailable => Status == ActivityQueryStatus.Available || Status == ActivityQueryStatus.NoMatch;
	}

	public sealed class ActivityHandlerDiagnostic
	{
		public ActivityHandlerDiagnostic(ActivityHandlerId handlerId, ActivityQueryStatus status, string reason)
		{
			HandlerId = handlerId;
			Status = status;
			Reason = reason;
		}

		public ActivityHandlerId HandlerId { get; }
		public ActivityQueryStatus Status { get; }
		public string Reason { get; }
	}

	/// <summary>
	/// Extensible execution boundary. Handlers select stable opportunity keys;
	/// the save-state service remains unaware of activity implementation details.
	/// A broken optional handler is quarantined without affecting other handlers.
	/// </summary>
	public sealed class OpportunityActivityRegistry
	{
		private readonly Dictionary<ActivityHandlerId, Registration> registrations = new();
		private readonly List<ActivityHandlerDiagnostic> diagnostics = new();

		public IReadOnlyList<ActivityHandlerDiagnostic> Diagnostics => new ReadOnlyCollection<ActivityHandlerDiagnostic>(diagnostics.ToArray());

		public void Register(IOpportunityActivityHandler handler, bool enabled = true, string? disabledReason = null)
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			if (registrations.ContainsKey(handler.Id))
				throw new InvalidOperationException($"Activity handler {handler.Id} is already registered.");
			registrations.Add(handler.Id, new Registration(handler, enabled, enabled ? null : RequiredReason(disabledReason)));
		}

		public void SetEnabled(ActivityHandlerId id, bool enabled, string? reason = null)
		{
			if (!registrations.TryGetValue(id, out var registration))
				return;
			registration.Enabled = enabled;
			registration.UnavailableReason = enabled ? null : RequiredReason(reason);
		}

		public void RunStartupChecks()
		{
			foreach (var registration in registrations.Values.OrderBy(item => item.Handler.Id))
			{
				if (!registration.Enabled) continue;
				try
				{
					var check = registration.Handler.StartupCheck() ?? ActivityHandlerSelfCheck.Unavailable("The handler returned no startup result.");
					if (!check.Available)
						Disable(registration, ActivityQueryStatus.DisabledHandler, check.Reason!);
				}
				catch (Exception exception)
				{
					Disable(registration, ActivityQueryStatus.FailedHandler, $"Startup self-check failed: {exception.GetType().Name}: {exception.Message}");
				}
			}
		}

		public ActivityQueryResult Query(ActivityHandlerId id, IOpportunityService service, DefIdentity project, Func<OpportunitySpecificationState, bool>? runtimeFilter = null)
		{
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (service == null) throw new ArgumentNullException(nameof(service));
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (!registrations.TryGetValue(id, out var registration))
				return Unavailable(id, ActivityQueryStatus.MissingHandler, "No activity handler is registered.");
			if (!registration.Enabled)
				return Unavailable(id, registration.FailureStatus ?? ActivityQueryStatus.DisabledHandler, registration.UnavailableReason ?? "The activity handler is disabled.");

			try
			{
				var query = new ActivityHandlerQuery(project, service.SpecificationsFor(project), runtimeFilter);
				var allowed = query.Specifications.ToDictionary(item => item.Spec.Key);
				var selected = (registration.Handler.Select(query) ?? Array.Empty<OpportunityKey>())
					.Where(key => key != null && allowed.TryGetValue(key, out var item) && (runtimeFilter == null || runtimeFilter(item)))
					.Distinct()
					.OrderBy(key => key)
					.ToArray();
				return new ActivityQueryResult(id, selected.Length == 0 ? ActivityQueryStatus.NoMatch : ActivityQueryStatus.Available, selected);
			}
			catch (Exception exception)
			{
				var reason = $"Handler query failed: {exception.GetType().Name}: {exception.Message}";
				Disable(registration, ActivityQueryStatus.FailedHandler, reason);
				return Unavailable(id, ActivityQueryStatus.FailedHandler, reason);
			}
		}

		private ActivityQueryResult Unavailable(ActivityHandlerId id, ActivityQueryStatus status, string reason)
		{
			diagnostics.Add(new ActivityHandlerDiagnostic(id, status, reason));
			return new ActivityQueryResult(id, status, null, reason);
		}

		private void Disable(Registration registration, ActivityQueryStatus status, string reason)
		{
			registration.Enabled = false;
			registration.FailureStatus = status;
			registration.UnavailableReason = reason;
			diagnostics.Add(new ActivityHandlerDiagnostic(registration.Handler.Id, status, reason));
		}

		private static string RequiredReason(string? reason) => string.IsNullOrWhiteSpace(reason)
			? throw new ArgumentException("A disabled handler needs an unavailable reason.", nameof(reason))
			: reason!;

		private sealed class Registration
		{
			public Registration(IOpportunityActivityHandler handler, bool enabled, string? unavailableReason)
			{
				Handler = handler;
				Enabled = enabled;
				UnavailableReason = unavailableReason;
			}

			public IOpportunityActivityHandler Handler { get; }
			public bool Enabled { get; set; }
			public ActivityQueryStatus? FailureStatus { get; set; }
			public string? UnavailableReason { get; set; }
		}
	}

	/// <summary>
	/// Revisioned per-owner query cache used by runtime map indexes. Invalidation
	/// is explicit and testable; no static current-project state is retained.
	/// </summary>
	public sealed class OpportunityQueryIndex<TOwner, TQuery, TValue>
		where TOwner : notnull
		where TQuery : notnull
	{
		private readonly Dictionary<TOwner, Dictionary<TQuery, IReadOnlyList<TValue>>> values;

		public OpportunityQueryIndex(IEqualityComparer<TOwner>? ownerComparer = null, IEqualityComparer<TQuery>? queryComparer = null)
		{
			OwnerComparer = ownerComparer ?? EqualityComparer<TOwner>.Default;
			QueryComparer = queryComparer ?? EqualityComparer<TQuery>.Default;
			values = new Dictionary<TOwner, Dictionary<TQuery, IReadOnlyList<TValue>>>(OwnerComparer);
		}

		public IEqualityComparer<TOwner> OwnerComparer { get; }
		public IEqualityComparer<TQuery> QueryComparer { get; }
		public int Revision { get; private set; }

		public IReadOnlyList<TValue> GetOrAdd(TOwner owner, TQuery query, Func<IEnumerable<TValue>> factory)
		{
			if (owner == null) throw new ArgumentNullException(nameof(owner));
			if (query == null) throw new ArgumentNullException(nameof(query));
			if (factory == null) throw new ArgumentNullException(nameof(factory));
			if (!values.TryGetValue(owner, out var ownerValues))
				values[owner] = ownerValues = new Dictionary<TQuery, IReadOnlyList<TValue>>(QueryComparer);
			if (!ownerValues.TryGetValue(query, out var cached))
				ownerValues[query] = cached = new ReadOnlyCollection<TValue>((factory() ?? Array.Empty<TValue>()).ToArray());
			return cached;
		}

		public void Invalidate(TOwner owner)
		{
			if (owner != null && values.Remove(owner)) Revision++;
		}

		public void InvalidateAll()
		{
			values.Clear();
			Revision++;
		}
	}
}
