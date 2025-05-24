namespace WeighingMachineConnector;

/// <summary>
/// Weight reading from a weighing device
/// </summary>
public class WeightReading
{
    /// <summary>
    /// The weight value
    /// </summary>
    public decimal Value { get; set; }
    
    /// <summary>
    /// The unit of measurement
    /// </summary>
    public WeightUnit Unit { get; set; }
    
    /// <summary>
    /// Timestamp when the reading was taken
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Whether the reading is stable
    /// </summary>
    public bool IsStable { get; set; }
    
    /// <summary>
    /// Additional metadata about the reading
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// Creates a new weight reading
    /// </summary>
    public WeightReading()
    {
        Timestamp = DateTime.UtcNow;
        Metadata = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Creates a new weight reading with the specified value and unit
    /// </summary>
    public WeightReading(decimal value, WeightUnit unit, bool isStable = true) : this()
    {
        Value = value;
        Unit = unit;
        IsStable = isStable;
    }
    
    /// <summary>
    /// Returns a string representation of the weight reading
    /// </summary>
    public override string ToString()
    {
        return $"{Value} {Unit}{(IsStable ? "" : " (unstable)")}, {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
    }
}

/// <summary>
/// Units of weight measurement
/// </summary>
public enum WeightUnit
{
    /// <summary>
    /// Grams
    /// </summary>
    Gram,
    
    /// <summary>
    /// Kilograms
    /// </summary>
    Kilogram,
    
    /// <summary>
    /// Pounds
    /// </summary>
    Pound,
    
    /// <summary>
    /// Ounces
    /// </summary>
    Ounce,
    
    /// <summary>
    /// Milligrams
    /// </summary>
    Milligram,
    
    /// <summary>
    /// Tons
    /// </summary>
    Ton
}

/// <summary>
/// Connection type for weighing devices
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Serial port connection (RS-232, RS-485, etc.)
    /// </summary>
    Serial,
    
    /// <summary>
    /// TCP/IP network connection
    /// </summary>
    Network,
    
    /// <summary>
    /// USB connection
    /// </summary>
    USB,
    
    /// <summary>
    /// Bluetooth connection
    /// </summary>
    Bluetooth,
    
    /// <summary>
    /// Custom connection type
    /// </summary>
    Custom
}

/// <summary>
/// Status of a weighing device connection
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Not connected
    /// </summary>
    Disconnected,
    
    /// <summary>
    /// Connection in progress
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Connected
    /// </summary>
    Connected,
    
    /// <summary>
    /// Error state
    /// </summary>
    Error
}

/// <summary>
/// Device-specific error
/// </summary>
public class WeighingDeviceException : Exception
{
    /// <summary>
    /// Creates a new weighing device exception
    /// </summary>
    public WeighingDeviceException(string message) : base(message)
    {
    }
    
    /// <summary>
    /// Creates a new weighing device exception with an inner exception
    /// </summary>
    public WeighingDeviceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}