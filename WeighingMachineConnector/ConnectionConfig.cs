namespace WeighingMachineConnector;

using System.IO.Ports;

/// <summary>
/// Configuration for connecting to a weighing device
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// Type of connection to use
    /// </summary>
    public ConnectionType Type { get; set; }

    /// <summary>
    /// Connection string or path (e.g., COM port name or IP address)
    /// </summary>
    public string ConnectionString { get; set; } = "";
    
    /// <summary>
    /// Port number for network connections
    /// </summary>
    public int? Port { get; set; }
    
    /// <summary>
    /// Baud rate for serial connections
    /// </summary>
    public int BaudRate { get; set; } = 9600;
    
    /// <summary>
    /// Data bits for serial connections
    /// </summary>
    public int DataBits { get; set; } = 8;
    
    /// <summary>
    /// Parity for serial connections
    /// </summary>
    public Parity Parity { get; set; } = Parity.None;
    
    /// <summary>
    /// Stop bits for serial connections
    /// </summary>
    public StopBits StopBits { get; set; } = StopBits.One;
    
    /// <summary>
    /// Flow control for serial connections
    /// </summary>
    public Handshake Handshake { get; set; } = Handshake.None;
    
    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;
    
    /// <summary>
    /// Additional configuration parameters
    /// </summary>
    public Dictionary<string, object> AdditionalParameters { get; set; } = new();

    /// <summary>
    /// Creates a new connection configuration
    /// </summary>
    public ConnectionConfig()
    {
    }
    
    /// <summary>
    /// Creates a new connection configuration for the specified connection type
    /// </summary>
    public ConnectionConfig(ConnectionType type, string connectionString)
    {
        Type = type;
        ConnectionString = connectionString;
    }
    
    /// <summary>
    /// Creates a serial port configuration
    /// </summary>
    public static ConnectionConfig CreateSerialConfig(string portName, 
        int baudRate = 9600, 
        int dataBits = 8,
        Parity parity = Parity.None,
        StopBits stopBits = StopBits.One,
        Handshake handshake = Handshake.None)
    {
        return new ConnectionConfig
        {
            Type = ConnectionType.Serial,
            ConnectionString = portName,
            BaudRate = baudRate,
            DataBits = dataBits,
            Parity = parity,
            StopBits = stopBits,
            Handshake = handshake
        };
    }
    
    /// <summary>
    /// Creates a network configuration
    /// </summary>
    public static ConnectionConfig CreateNetworkConfig(string hostname, int port)
    {
        return new ConnectionConfig
        {
            Type = ConnectionType.Network,
            ConnectionString = hostname,
            Port = port
        };
    }
}