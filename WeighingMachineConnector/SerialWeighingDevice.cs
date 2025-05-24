namespace WeighingMachineConnector;

using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implementation of a weighing device that communicates over a serial port
/// </summary>
public class SerialWeighingDevice : WeighingDeviceBase
{
    private SerialPort? _serialPort;
    private readonly string _deviceName;
    private readonly string _manufacturer;
    private readonly string _model;
    private readonly bool _supportsContinuousReading;
    private readonly Encoding _encoding;
    private readonly string _weightCommand;
    private readonly string _zeroCommand;
    private readonly string _continuousStartCommand;
    private readonly string _continuousStopCommand;
    private readonly Func<string, WeightReading?> _parseWeightFunction;
    private Task? _continuousReadingTask;

    /// <summary>
    /// Gets the name of the device
    /// </summary>
    public override string DeviceName => _deviceName;
    
    /// <summary>
    /// Gets the manufacturer of the device
    /// </summary>
    public override string Manufacturer => _manufacturer;
    
    /// <summary>
    /// Gets the model of the device
    /// </summary>
    public override string Model => _model;
    
    /// <summary>
    /// Gets whether the device supports continuous reading
    /// </summary>
    public override bool SupportsContinuousReading => _supportsContinuousReading;

    /// <summary>
    /// Creates a new serial weighing device
    /// </summary>
    /// <param name="config">Serial connection configuration</param>
    /// <param name="deviceName">Name of the device</param>
    /// <param name="manufacturer">Manufacturer of the device</param>
    /// <param name="model">Model of the device</param>
    /// <param name="weightCommand">Command to request a weight reading</param>
    /// <param name="zeroCommand">Command to zero/tare the scale</param>
    /// <param name="parseWeightFunction">Function to parse weight readings from device responses</param>
    /// <param name="supportsContinuousReading">Whether the device supports continuous reading</param>
    /// <param name="continuousStartCommand">Command to start continuous reading (if supported)</param>
    /// <param name="continuousStopCommand">Command to stop continuous reading (if supported)</param>
    /// <param name="encoding">Encoding for serial communication (default is ASCII)</param>
    public SerialWeighingDevice(
        ConnectionConfig config,
        string deviceName,
        string manufacturer,
        string model,
        string weightCommand,
        string zeroCommand,
        Func<string, WeightReading?> parseWeightFunction,
        bool supportsContinuousReading = false,
        string continuousStartCommand = "",
        string continuousStopCommand = "",
        Encoding? encoding = null)
        : base(config)
    {
        if (config.Type != ConnectionType.Serial)
            throw new ArgumentException("Configuration must be for a serial connection", nameof(config));
            
        _deviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        _manufacturer = manufacturer ?? throw new ArgumentNullException(nameof(manufacturer));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _weightCommand = weightCommand ?? throw new ArgumentNullException(nameof(weightCommand));
        _zeroCommand = zeroCommand ?? throw new ArgumentNullException(nameof(zeroCommand));
        _parseWeightFunction = parseWeightFunction ?? throw new ArgumentNullException(nameof(parseWeightFunction));
        _supportsContinuousReading = supportsContinuousReading;
        _continuousStartCommand = continuousStartCommand;
        _continuousStopCommand = continuousStopCommand;
        _encoding = encoding ?? Encoding.ASCII;
        
        if (_supportsContinuousReading && (string.IsNullOrEmpty(_continuousStartCommand) || string.IsNullOrEmpty(_continuousStopCommand)))
        {
            throw new ArgumentException("Continuous reading commands must be provided if continuous reading is supported");
        }
    }
    
    /// <summary>
    /// Opens the serial connection
    /// </summary>
    protected override async Task<bool> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_serialPort != null)
        {
            await CloseConnectionAsync();
        }

        try
        {
            _serialPort = new SerialPort
            {
                PortName = Configuration.ConnectionString,
                BaudRate = Configuration.BaudRate,
                DataBits = Configuration.DataBits,
                Parity = Configuration.Parity,
                StopBits = Configuration.StopBits,
                Handshake = Configuration.Handshake,
                ReadTimeout = Configuration.ReadTimeoutMs,
                WriteTimeout = Configuration.ReadTimeoutMs,
                Encoding = _encoding
            };
            
            try
            {
                _serialPort.Open();
                
                // Allow some time for the device to initialize after connection
                await Task.Delay(500, cancellationToken);
                
                // Check if communication works by sending the weight command and expecting a response
                var testResult = await SendCommandAsync(_weightCommand, cancellationToken);
                return !string.IsNullOrEmpty(testResult);
            }
            catch (UnauthorizedAccessException)
            {
                throw new WeighingDeviceException($"Access denied to port {Configuration.ConnectionString}. The port may be in use by another application.");
            }
            catch (IOException)
            {
                throw new WeighingDeviceException($"Error accessing port {Configuration.ConnectionString}. The port may not exist or the hardware is not connected.");
            }
            catch (TimeoutException)
            {
                throw new WeighingDeviceException($"Timeout while trying to communicate with device on port {Configuration.ConnectionString}. No compatible device may be connected.");
            }
            catch (Exception ex) when (ex is not WeighingDeviceException)
            {
                throw new WeighingDeviceException($"Failed to connect to weighing device on port {Configuration.ConnectionString}: {ex.Message}", ex);
            }
        }
        catch (Exception)
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
            throw;
        }
    }
    
    /// <summary>
    /// Closes the serial connection
    /// </summary>
    protected override Task CloseConnectionAsync()
    {
        if (_serialPort != null)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            _serialPort.Dispose();
            _serialPort = null;
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Reads the weight from the device
    /// </summary>
    protected override async Task<WeightReading> ReadWeightAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendCommandAsync(_weightCommand, cancellationToken);
        
        var weightReading = _parseWeightFunction(response);
        if (weightReading == null)
        {
            throw new WeighingDeviceException($"Failed to parse weight reading from response: {response}");
        }
        
        return weightReading;
    }
    
    /// <summary>
    /// Starts continuous reading
    /// </summary>
    protected override async Task StartContinuousReadingInternalAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync(_continuousStartCommand, cancellationToken);
        
        _continuousReadingTask = Task.Run(async () => 
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        // Read data from the serial port
                        string response = await ReadSerialDataAsync(cancellationToken);
                        
                        if (!string.IsNullOrEmpty(response))
                        {
                            var weightReading = _parseWeightFunction(response);
                            if (weightReading != null)
                            {
                                OnWeightReadingReceived(weightReading);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Log or handle read errors
                        await Task.Delay(1000, cancellationToken); // Back off on error
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, ignore
            }
            catch (Exception ex)
            {
                // Log background task exception
                System.Diagnostics.Debug.WriteLine($"Continuous reading error: {ex}");
            }
        }, cancellationToken);
    }
    
    /// <summary>
    /// Stops continuous reading
    /// </summary>
    protected override async Task StopContinuousReadingInternalAsync()
    {
        await SendCommandAsync(_continuousStopCommand, CancellationToken.None);
        
        // Wait for the continuous reading task to complete
        if (_continuousReadingTask != null)
        {
            try
            {
                await Task.WhenAny(_continuousReadingTask, Task.Delay(2000));
            }
            catch
            {
                // Ignore exceptions during task cleanup
            }
            
            _continuousReadingTask = null;
        }
    }
    
    /// <summary>
    /// Zeroes/tares the scale
    /// </summary>
    protected override async Task ZeroScaleInternalAsync(CancellationToken cancellationToken)
    {
        await SendCommandAsync(_zeroCommand, cancellationToken);
    }
    
    /// <summary>
    /// Sends a raw command to the device
    /// </summary>
    protected override async Task<string> SendRawCommandInternalAsync(string command, CancellationToken cancellationToken)
    {
        return await SendCommandAsync(command, cancellationToken);
    }
    
    /// <summary>
    /// Sends a command to the device and reads the response
    /// </summary>
    private async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open");
        }
        
        // Clear any existing data in the buffer
        _serialPort.DiscardInBuffer();
        
        // Add line termination if needed
        string fullCommand = command;
        if (!command.EndsWith("\r") && !command.EndsWith("\n"))
        {
            fullCommand += "\r\n";
        }
        
        // Send the command
        byte[] commandBytes = _encoding.GetBytes(fullCommand);
        await _serialPort.BaseStream.WriteAsync(commandBytes, 0, commandBytes.Length, cancellationToken);
        await _serialPort.BaseStream.FlushAsync(cancellationToken);
        
        // Read the response
        return await ReadSerialDataAsync(cancellationToken);
    }
    
    /// <summary>
    /// Reads data from the serial port
    /// </summary>
    private async Task<string> ReadSerialDataAsync(CancellationToken cancellationToken)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open");
        }
        
        // Give the device time to respond
        await Task.Delay(100, cancellationToken);
        
        // Prepare a buffer to read the data
        byte[] buffer = new byte[4096];
        StringBuilder response = new StringBuilder();
        
        // Continue reading until no more data is available or timeout
        var readTask = Task.Run(async () => 
        {
            try
            {
                int bytesRead;
                int totalBytesRead = 0;
                
                // Read until timeout or buffer full
                while (totalBytesRead < buffer.Length)
                {
                    if (_serialPort.BytesToRead == 0)
                    {
                        // If no more bytes, short pause and check again
                        await Task.Delay(50, cancellationToken);
                        if (_serialPort.BytesToRead == 0)
                        {
                            // If still no bytes, we're done
                            break;
                        }
                    }
                    
                    bytesRead = await _serialPort.BaseStream.ReadAsync(
                        buffer, totalBytesRead, Math.Min(buffer.Length - totalBytesRead, _serialPort.BytesToRead), 
                        cancellationToken);
                    
                    if (bytesRead == 0)
                        break;
                        
                    totalBytesRead += bytesRead;
                }
                
                if (totalBytesRead > 0)
                {
                    return _encoding.GetString(buffer, 0, totalBytesRead);
                }
                
                return string.Empty;
            }
            catch (TimeoutException)
            {
                return string.Empty;
            }
        });
        
        // Set a timeout for the read operation
        if (await Task.WhenAny(readTask, Task.Delay(Configuration.ReadTimeoutMs, cancellationToken)) == readTask)
        {
            return await readTask;
        }
        else
        {
            throw new TimeoutException("Timeout waiting for device response");
        }
    }
    
    /// <summary>
    /// Disposes the serial weighing device
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose the serial port if it exists
            _serialPort?.Dispose();
            _serialPort = null;
        }
        
        base.Dispose(disposing);
    }
}
