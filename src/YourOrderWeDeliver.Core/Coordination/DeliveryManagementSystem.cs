using YourOrderWeDeliver.Core.Common;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Core.Persistence;
using YourOrderWeDeliver.Core.Pricing;

namespace YourOrderWeDeliver.Core.Coordination;

/// <summary>
/// The single top-level coordinator (A2 CRC 5.10, Singleton pattern). It runs the start-up
/// sequence, owns the one <see cref="DeliveryRegister"/> and <see cref="StaffRoster"/>, and saves
/// the state. It performs no business activities itself.
/// </summary>
public sealed class DeliveryManagementSystem
{
    private static readonly object InstanceLock = new();
    private static DeliveryManagementSystem? _instance;

    private readonly IDataStore _dataStore;

    private DeliveryManagementSystem(IDataStore dataStore, ServiceArea serviceArea, IPricingPolicy pricingPolicy, IClock clock)
    {
        _dataStore = dataStore;
        PricingPolicy = pricingPolicy;
        Clock = clock;

        SystemSnapshot snapshot = dataStore.Load() ?? new SystemSnapshot();
        Roster = SnapshotMapper.RestoreRoster(snapshot);
        Register = SnapshotMapper.RestoreRegister(snapshot, serviceArea, Roster);
        IsReady = true;
    }

    /// <summary>Gets the single instance.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Initialise"/> has not been called.</exception>
    public static DeliveryManagementSystem Instance =>
        _instance ?? throw new InvalidOperationException("The Delivery Management System has not been initialised.");

    /// <summary>Gets the delivery register (R2).</summary>
    public DeliveryRegister Register { get; }

    /// <summary>Gets the staff roster (R3).</summary>
    public StaffRoster Roster { get; }

    /// <summary>Gets the pricing strategy used to suggest quote costs.</summary>
    public IPricingPolicy PricingPolicy { get; }

    /// <summary>Gets the clock used for all time-stamps.</summary>
    public IClock Clock { get; }

    /// <summary>Gets whether start-up completed and the system can be used (R4).</summary>
    public bool IsReady { get; }

    /// <summary>
    /// Creates the single instance and loads saved state (R1). Calling it a second time is an error,
    /// which guarantees there is only one register and one roster.
    /// </summary>
    public static DeliveryManagementSystem Initialise(
        IDataStore dataStore, ServiceArea serviceArea, IPricingPolicy pricingPolicy, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(serviceArea);
        ArgumentNullException.ThrowIfNull(pricingPolicy);
        ArgumentNullException.ThrowIfNull(clock);
        lock (InstanceLock)
        {
            if (_instance is not null)
            {
                throw new InvalidOperationException("The Delivery Management System has already been initialised.");
            }

            _instance = new DeliveryManagementSystem(dataStore, serviceArea, pricingPolicy, clock);
            return _instance;
        }
    }

    /// <summary>Saves the current state of the register and roster.</summary>
    public void SaveChanges() => _dataStore.Save(SnapshotMapper.CreateSnapshot(Register, Roster));

    internal static void ResetForTesting()
    {
        lock (InstanceLock)
        {
            _instance = null;
        }
    }
}
