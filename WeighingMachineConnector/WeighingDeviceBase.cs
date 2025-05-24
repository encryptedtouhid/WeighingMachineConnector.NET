namespace WeighingMachineConnector;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Base class for weighing device implementations providing common functionality
/// </summary>
public abstract class WeighingDeviceBase : IWeighingDevice
{
    private readonly object _lockObject = new object();
    private bool _disposed = false;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private bool _isContinuousReadingActive = false;
    
    /// <summary>
    /// Cancellation token source for continuous reading operations
    /// </summary>
    protected CancellationTokenSource? _continuousReadingCts = null;

    /// <summary>
    /// Gets the name of the device
    /// </summary>
    public abstract string DeviceName { get; }
    
    /// <summary>
    /// Gets the manufacturer of the device
    /// </summary>
    public abstract string Manufacturer { get; }
    
    /// <summary>
    /// Gets the model of the device
    /// </summary>
    public abstract string Model { get; }
    
    /// <summary>
    /// Gets the connection configuration
    /// </summary>
    public ConnectionConfig Configuration { get; }
    
    /// <summary>
    /// Gets whether the device supports continuous reading
    /// </summary>
    public abstract bool SupportsContinuousReading { get; }
    
    /// <summary>
    /// Gets the current connection status of the device
    /// </summary>
    public ConnectionStatus Status 
    {
        get => _status;
        protected set
        {
            if (_status != value)
            {
                _status = value;
                ConnectionStatusChanged?.Invoke(this, _status);
            }
        }
    }
    
    /// <summary>
    /// Event that fires when a new weight reading is available
    /// </summary>
    public event EventHandler<WeightReading>? WeightReadingReceived;
    
    /// <summary>
    /// Event that fires when the connection status changes
    /// </summary>
    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
    
    /// <summary>
    /// Creates a new weighing device instance with the specified configuration
    /// </summary>
    /// <param name="configuration">Connection configuration</param>
    protected WeighingDeviceBase(ConnectionConfig configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    
    /// <summary>
    /// Opens a connection to the weighing device
    /// </summary>
    public virtual async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (Status == ConnectionStatus.Connected)
            return true;
            
        Status = ConnectionStatus.Connecting;
        try
        {
            bool result = await OpenConnectionAsync(cancellationToken);
            Status = result ? ConnectionStatus.Connected : ConnectionStatus.Error;
            return result;
        }
        catch (Exception ex)
        {
            Status = ConnectionStatus.Error;
            throw new WeighingDeviceException($"Failed to connect to device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to open the connection
    /// </summary>
    protected abstract Task<bool> OpenConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Closes the connection to the weighing device
    /// </summary>
    public virtual async Task DisconnectAsync()
    {
        ThrowIfDisposed();
        
        if (Status == ConnectionStatus.Disconnected)
            return;
            
        try
        {
            if (_isContinuousReadingActive)
            {
                await StopContinuousReadingAsync();
            }
            
            await CloseConnectionAsync();
            Status = ConnectionStatus.Disconnected;
        }
        catch (Exception ex)
        {
            Status = ConnectionStatus.Error;
            throw new WeighingDeviceException($"Error disconnecting from device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to close the connection
    /// </summary>
    protected abstract Task CloseConnectionAsync();
    
    /// <summary>
    /// Gets the current weight from the weighing device
    /// </summary>
    public async Task<WeightReading> GetWeightAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConnected();
        
        try
        {
            return await ReadWeightAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WeighingDeviceException($"Error reading weight from device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to read the weight
    /// </summary>
    protected abstract Task<WeightReading> ReadWeightAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts continuous weight reading if supported by the device
    /// </summary>
    public virtual async Task StartContinuousReadingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConnected();
        
        if (!SupportsContinuousReading)
            throw new NotSupportedException($"Device {DeviceName} does not support continuous reading");
            
        if (_isContinuousReadingActive)
            return;
            
        try
        {
            _continuousReadingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await StartContinuousReadingInternalAsync(_continuousReadingCts.Token);
            _isContinuousReadingActive = true;
        }
        catch (Exception ex)
        {
            _continuousReadingCts?.Dispose();
            _continuousReadingCts = null;
            throw new WeighingDeviceException($"Error starting continuous reading on device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to start continuous reading
    /// </summary>
    protected abstract Task StartContinuousReadingInternalAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Stops continuous weight reading
    /// </summary>
    public virtual async Task StopContinuousReadingAsync()
    {
        ThrowIfDisposed();
        
        if (!_isContinuousReadingActive)
            return;
            
        try
        {
            _continuousReadingCts?.Cancel();
            await StopContinuousReadingInternalAsync();
        }
        catch (Exception ex)
        {
            throw new WeighingDeviceException($"Error stopping continuous reading on device: {DeviceName}", ex);
        }
        finally
        {
            _continuousReadingCts?.Dispose();
            _continuousReadingCts = null;
            _isContinuousReadingActive = false;
        }
    }
    
    /// <summary>
    /// Implementation-specific method to stop continuous reading
    /// </summary>
    protected abstract Task StopContinuousReadingInternalAsync();
    
    /// <summary>
    /// Zeroes/tares the scale
    /// </summary>
    public async Task ZeroScaleAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConnected();
        
        try
        {
            await ZeroScaleInternalAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WeighingDeviceException($"Error zeroing scale on device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to zero the scale
    /// </summary>
    protected abstract Task ZeroScaleInternalAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Sends a raw command to the device
    /// </summary>
    public async Task<string> SendRawCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConnected();
        
        if (string.IsNullOrEmpty(command))
            throw new ArgumentException("Command cannot be null or empty", nameof(command));
            
        try
        {
            return await SendRawCommandInternalAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WeighingDeviceException($"Error sending command to device: {DeviceName}", ex);
        }
    }
    
    /// <summary>
    /// Implementation-specific method to send a raw command
    /// </summary>
    protected abstract Task<string> SendRawCommandInternalAsync(string command, CancellationToken cancellationToken);
    
    /// <summary>
    /// Raises the WeightReadingReceived event
    /// </summary>
    protected virtual void OnWeightReadingReceived(WeightReading reading)
    {
        WeightReadingReceived?.Invoke(this, reading);
    }
    
    /// <summary>
    /// Ensures that the device is connected
    /// </summary>
    protected void EnsureConnected()
    {
        if (Status != ConnectionStatus.Connected)
            throw new InvalidOperationException($"Device {DeviceName} is not connected");
    }
    
    /// <summary>
    /// Throws if the object has been disposed
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
    
    /// <summary>
    /// Disposes the weighing device
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes the weighing device
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
        {
            // Dispose managed resources
            if (Status == ConnectionStatus.Connected)
            {
                try
                {
                    DisconnectAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore exceptions during disposal
                }
            }
            
            _continuousReadingCts?.Dispose();
            _continuousReadingCts = null;
        }
        
        _disposed = true;
    }
    
    /// <summary>
    /// Finalizer
    /// </summary>
    ~WeighingDeviceBase()
    {
        Dispose(false);
    }
}

