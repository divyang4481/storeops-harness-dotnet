namespace StoreOps.Api.Modules.Alerts;

// AlertType in the specification: INVENTORY | SLA_BREACH | SHIFT_HANDOVER | ESCALATION.
public enum AlertType
{
    Inventory,
    SlaBreach,
    ShiftHandover,
    Escalation,
}
