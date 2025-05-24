namespace WeighingMachineConnector;

/// <summary>
/// Interface for weighing device communications
/// </summary>
public interface IWeighingDevice : IDisposable
{
    /// <summary>
    /// Gets the name of the device
    /// </summary>
    string DeviceName { get; }
    
    /// <summary>
    /// Gets the manufacturer of the device
    /// </summary>
    string Manufacturer { get; }
    
    /// <summary>
    /// Gets the model of the device
    /// </summary>
    string Model { get; }
    
    /// <summary>
    /// Gets the current connection status of the device
    /// </summary>
    ConnectionStatus Status { get; }
    
    /// <summary>
    /// Gets the connection configuration
    /// </summary>
    ConnectionConfig Configuration { get; }
    
    /// <summary>
    /// Gets whether the device supports continuous reading
    /// </summary>
    bool SupportsContinuousReading { get; }
    
    /// <summary>
    /// Event that fires when a new weight reading is available
    /// </summary>
    event EventHandler<WeightReading> WeightReadingReceived;
    
    /// <summary>
    /// Event that fires when the connection status changes
    /// </summary>
    event EventHandler<ConnectionStatus> ConnectionStatusChanged;
    
    /// <summary>
    /// Opens a connection to the weighing device
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes the connection to the weighing device
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Gets the current weight from the weighing device
    /// </summary>
    Task<WeightReading> GetWeightAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts continuous weight reading if supported by the device
    /// </summary>
    Task StartContinuousReadingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops continuous weight reading
    /// </summary>
    Task StopContinuousReadingAsync();
    
    /// <summary>
    /// Zeroes/tares the scale
    /// </summary>
    Task ZeroScaleAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a raw command to the device
    /// </summary>
    Task<string> SendRawCommandAsync(string command, CancellationToken cancellationToken = default);
}