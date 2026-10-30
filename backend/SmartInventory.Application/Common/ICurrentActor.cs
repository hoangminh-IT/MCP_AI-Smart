namespace SmartInventory.Application.Common;

/// <summary>Authenticated execution identity. Background/design-time contexts may have no actor.</summary>
public interface ICurrentActor
{
    int? UserId { get; }
}
