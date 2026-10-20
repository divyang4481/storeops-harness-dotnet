namespace StoreOps.Api.Modules.Activities;

// Outcome of one SLA sweep: how many new breaches and how many new escalations were raised.
public sealed record SlaSweepResult(int BreachesRaised, int EscalationsRaised);
