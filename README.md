# WeighingMachineConnector.NET

A flexible .NET library for connecting to and reading data from various weighing devices (scales, balances) across different connection types.

## Features

- Connect to weighing devices over multiple transport protocols:
  - Serial (RS-232, RS-485)
  - Network (TCP/IP)
  - USB
  - Bluetooth
- Support for both on-demand and continuous weight reading modes
- Easily extensible architecture for implementing device-specific protocols
- Event-based notifications for weight readings and connection status changes
- Consistent error handling across different device types
- Zero/Tare scale functionality
- Raw command interface for direct device communication

## Supported Device Types

### Serial Port Devices
- Generic RS-232 scales with configurable commands
- Toledo/Mettler scales (P8142)
- Sartorius scales (CW and LP series)
- A&D Weighing scales (FX/FZ/GF series) 
- Ohaus scales (Scout, Explorer, Defender)
- Adam Equipment scales (CBK, GBK series)
- AND scales (GX, GF series)
- CAS Corporation scales (ED, PB series)
- Rice Lake scales (various models)

### Network Devices
- HBM WTX110/120 Weighing Terminals
- Mettler Toledo IND series terminals
- Rice Lake 1280 Enterprise Series

### USB Devices
- Dymo S100/S250 USB scales
- Mettler Toledo PS60 USB scales

### Bluetooth Devices
- AXIS BLE scales
- AND Weighing WB Bluetooth scales

## Getting Started

### Installation

```bash
dotnet add package WeighingMachineConnector
```

### Basic Usage

```csharp
// Create a connection configuration
var config = ConnectionConfig.CreateSerialConfig(
    portName: "COM3",
    baudRate: 9600,
    dataBits: 8,
    parity: System.IO.Ports.Parity.None,
    stopBits: System.IO.Ports.StopBits.One
);

// Create a weighing device instance
var device = new SerialWeighingDevice(
    config: config,
    deviceName: "Laboratory Scale",
    manufacturer: "Mettler Toledo",
    model: "P8142",
    weightCommand: "W\r\n",
    zeroCommand: "Z\r\n",
    parseWeightFunction: response => 
    {
        // Implement parsing logic specific to your device
        // Example for a device that returns "W: 123.45 g"
        var match = Regex.Match(response, @"W:\s*(\d+\.?\d*)\s*(g|kg|lb|oz)");
        if (match.Success)
        {
            decimal weight = decimal.Parse(match.Groups[1].Value);
            string unitStr = match.Groups[2].Value.ToLower();
            
            WeightUnit unit = unitStr switch
            {
                "g" => WeightUnit.Gram,
                "kg" => WeightUnit.Kilogram,
                "lb" => WeightUnit.Pound,
                "oz" => WeightUnit.Ounce,
                _ => WeightUnit.Gram
            };
            
            return new WeightReading(weight, unit);
        }
        return null;
    }
);

// Subscribe to events
device.WeightReadingReceived += (sender, reading) => 
{
    Console.WriteLine($"Received weight: {reading.Value} {reading.Unit}");
};

// Connect to device
await device.ConnectAsync();

// Read weight on demand
var weight = await device.GetWeightAsync();
Console.WriteLine($"Weight: {weight.Value} {weight.Unit}");

// Start continuous reading
await device.StartContinuousReadingAsync();

// Stop continuous reading
await device.StopContinuousReadingAsync();

// Zero/Tare the scale
await device.ZeroScaleAsync();

// Cleanup
device.Dispose();
```

## Creating Custom Device Implementations

You can create your own device implementations by extending the `WeighingDeviceBase` class:

```csharp
public class MyCustomWeighingDevice : WeighingDeviceBase
{
    public MyCustomWeighingDevice(ConnectionConfig config) : base(config)
    {
        // Custom initialization
    }
    
    // Implement abstract methods...
}
```

## Sample Application

The solution includes a sample application that demonstrates how to use the library with different types of devices:

- Serial device with on-demand reading
- Serial device with continuous reading
- Simulated device (for testing without hardware)

## License

MIT License

