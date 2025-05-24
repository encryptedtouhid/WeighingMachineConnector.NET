using System.IO.Ports;
using System.Text.RegularExpressions;
using WeighingMachineConnector;

// Sample application demonstrating how to use the WeighingMachineConnector library
namespace WeighingMachineSample
{
    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("=== Weighing Machine Connector Sample ===");
            Console.WriteLine("This sample demonstrates how to connect to different weighing devices.");
            
            try
            {
                // Demonstrate different device examples
                Console.WriteLine("\nChoose an example to run:");
                Console.WriteLine("1. Serial device with on-demand reading");
                Console.WriteLine("2. Serial device with continuous reading");
                Console.WriteLine("3. Simple simulated device (no hardware needed)");
                Console.WriteLine("4. Demo with predefined weight data (no hardware needed)");
                Console.Write("\nEnter your choice (1-4): ");
                
                var choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        await RunSerialDeviceExampleAsync(false);
                        break;
                    case "2":
                        await RunSerialDeviceExampleAsync(true);
                        break;
                    case "3":
                        await RunSimulatedDeviceExampleAsync();
                        break;
                    case "4":
                        await RunDemoWithPredefinedDataAsync();
                        break;
                    default:
                        Console.WriteLine("Invalid choice, running demo with predefined weight data.");
                        await RunDemoWithPredefinedDataAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        /// <summary>
        /// Example showing how to use a SerialWeighingDevice
        /// </summary>
        static async Task RunSerialDeviceExampleAsync(bool useContinuousReading)
        {
            // List available serial ports
            Console.WriteLine("\n=== Available Serial Ports ===");
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("No serial ports found. Please connect a device and try again.");
                return;
            }
            
            for (int i = 0; i < ports.Length; i++)
            {
                Console.WriteLine($"{i+1}. {ports[i]}");
            }
            
            // Let user select a port
            Console.Write("\nSelect a serial port (number): ");
            if (!int.TryParse(Console.ReadLine(), out int portIndex) || portIndex < 1 || portIndex > ports.Length)
            {
                Console.WriteLine("Invalid selection. Using first available port.");
                portIndex = 1;
            }
            
            string selectedPort = ports[portIndex - 1];
            Console.WriteLine($"Selected port: {selectedPort}");
            
            // Create connection configuration for the serial device
            var config = ConnectionConfig.CreateSerialConfig(
                portName: selectedPort,
                baudRate: 9600,
                dataBits: 8,
                parity: Parity.None,
                stopBits: StopBits.One,
                handshake: Handshake.None
            );
            
            // Create an instance of a generic serial weighing device with standard commands
            // Note: Most real devices will need specific command formats and response parsing
            var device = new SerialWeighingDevice(
                config: config,
                deviceName: "Generic Serial Scale",
                manufacturer: "Generic",
                model: "RS232 Scale",
                weightCommand: "W\r\n",       // Command to request weight, adjust for your device
                zeroCommand: "Z\r\n",         // Command to zero/tare scale, adjust for your device
                parseWeightFunction: ParseWeightReading, // Function to parse the response
                supportsContinuousReading: true,
                continuousStartCommand: "C\r\n", // Command to start continuous mode, adjust for your device
                continuousStopCommand: "S\r\n"   // Command to stop continuous mode, adjust for your device
            );
            
            // Subscribe to events
            device.ConnectionStatusChanged += (_, status) => 
            {
                Console.WriteLine($"Connection status changed: {status}");
            };
            
            device.WeightReadingReceived += (_, reading) => 
            {
                Console.WriteLine($"Weight reading: {reading}");
            };
            
            try
            {
                // Connect to the device
                Console.WriteLine("Connecting to device...");
                bool connected = await device.ConnectAsync();
                
                if (connected)
                {
                    Console.WriteLine("Successfully connected!");
                    Console.WriteLine($"Device: {device.DeviceName} ({device.Manufacturer} {device.Model})");
                    
                    if (useContinuousReading)
                    {
                        // Continuous reading example
                        Console.WriteLine("Starting continuous reading mode...");
                        await device.StartContinuousReadingAsync();
                        
                        Console.WriteLine("Receiving continuous readings for 30 seconds...");
                        Console.WriteLine("Press any key to stop early.");
                        
                        // Wait for either 30 seconds or key press
                        Task delayTask = Task.Delay(30000);
                        Task keyTask = Task.Run(() => { Console.ReadKey(true); });
                        await Task.WhenAny(delayTask, keyTask);
                        
                        Console.WriteLine("Stopping continuous reading...");
                        await device.StopContinuousReadingAsync();
                    }
                    else
                    {
                        // On-demand reading example
                        Console.WriteLine("Press any key to take a weight reading, or ESC to quit");
                        
                        ConsoleKeyInfo key;
                        do
                        {
                            key = Console.ReadKey(true);
                            
                            if (key.Key != ConsoleKey.Escape)
                            {
                                try
                                {
                                    Console.WriteLine("Reading weight...");
                                    WeightReading reading = await device.GetWeightAsync();
                                    Console.WriteLine($"Weight: {reading.Value} {reading.Unit}");
                                    
                                    // Example of zeroing the scale
                                    if (key.Key == ConsoleKey.Z)
                                    {
                                        Console.WriteLine("Zeroing scale...");
                                        await device.ZeroScaleAsync();
                                        Console.WriteLine("Scale zeroed.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error: {ex.Message}");
                                }
                            }
                        } while (key.Key != ConsoleKey.Escape);
                    }
                    
                    // Disconnect when done
                    await device.DisconnectAsync();
                    Console.WriteLine("Disconnected from device");
                }
                else
                {
                    Console.WriteLine("Failed to connect to device.");
                }
            }
            finally
            {
                // Clean up resources
                device.Dispose();
            }
        }
        
        /// <summary>
        /// Example showing how to use a simulated device (requires no hardware)
        /// </summary>
        static async Task RunSimulatedDeviceExampleAsync()
        {
            Console.WriteLine("\n=== Simulated Weighing Device Example ===");
            Console.WriteLine("This example uses a simulated device that doesn't require real hardware.");
            
            // Create a simulated device
            var device = new SimulatedWeighingDevice(
                deviceName: "Simulated Scale",
                manufacturer: "Virtual Scales Inc.",
                model: "VS-2025",
                initialWeight: 0.0m,
                weightUnit: WeightUnit.Kilogram,
                minWeight: 0.0m,
                maxWeight: 100.0m
            );
            
            // Subscribe to events
            device.WeightReadingReceived += (_, reading) => 
            {
                Console.WriteLine($"Weight reading: {reading.Value} {reading.Unit}");
            };
            
            // Connect to the device
            await device.ConnectAsync();
            Console.WriteLine("Connected to simulated device");
            Console.WriteLine($"Device: {device.DeviceName} ({device.Manufacturer} {device.Model})");
            
            // Start continuous reading
            await device.StartContinuousReadingAsync();
            Console.WriteLine("Started continuous reading");
            
            Console.WriteLine("\nAvailable commands:");
            Console.WriteLine("  A: Add 1 kg to the scale");
            Console.WriteLine("  S: Subtract 1 kg from the scale");
            Console.WriteLine("  Z: Zero the scale");
            Console.WriteLine("  Q: Quit\n");
            
            bool running = true;
            while (running)
            {
                var key = Console.ReadKey(true).KeyChar.ToString().ToUpper();
                switch (key)
                {
                    case "A":
                        await device.AddWeightAsync(1.0m);
                        break;
                    case "S":
                        await device.AddWeightAsync(-1.0m);
                        break;
                    case "Z":
                        await device.ZeroScaleAsync();
                        Console.WriteLine("Scale zeroed");
                        break;
                    case "Q":
                        running = false;
                        break;
                }
            }
            
            // Clean up
            await device.StopContinuousReadingAsync();
            await device.DisconnectAsync();
            device.Dispose();
            
            Console.WriteLine("Disconnected from simulated device");
        }
        
        /// <summary>
        /// Example function to parse weight readings from a scale's response
        /// This is highly device-specific and would need to be adjusted for actual devices
        /// </summary>
        static WeightReading? ParseWeightReading(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;
                
            // Example parsing logic for a device that returns data like "W: 1.234 kg"
            // Real devices may use different formats!
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
            
            // Alternative simpler parsing for devices that just return the weight as a number
            if (decimal.TryParse(response.Trim(), out decimal simpleWeight))
            {
                return new WeightReading(simpleWeight, WeightUnit.Gram);
            }
            
            return null;
        }

        /// <summary>
        /// Example showing how to use predefined demo data without any hardware
        /// </summary>
        static async Task RunDemoWithPredefinedDataAsync()
        {
            Console.WriteLine("\n=== Demo with Predefined Weight Data ===");
            Console.WriteLine("This example demonstrates weight readings using predefined data patterns.");
            
            // Create a demo device instance with predefined weight data
            var device = new DemoWeighingDevice(
                deviceName: "Demo Scale",
                manufacturer: "Demo Scales Inc.",
                model: "DEMO-2025",
                weightUnit: WeightUnit.Kilogram
            );
            
            // Subscribe to events
            device.WeightReadingReceived += (_, reading) => 
            {
                Console.WriteLine($"Weight reading: {reading.Value,8:F3} {reading.Unit} ({(reading.IsStable ? "stable" : "unstable")})");
            };
            
            // Connect to the device
            await device.ConnectAsync();
            Console.WriteLine($"Connected to {device.DeviceName} ({device.Manufacturer} {device.Model})");
            
            // Run various weight data pattern demonstrations
            Console.WriteLine("\nDemo Scenarios:");
            Console.WriteLine(" 1: Stable weight readings");
            Console.WriteLine(" 2: Gradually increasing weight");
            Console.WriteLine(" 3: Weight with instability/fluctuations");
            Console.WriteLine(" 4: Step changes in weight");
            Console.WriteLine(" 5: Weight overload simulation");
            Console.WriteLine(" 6: Zero-stability demonstration");
            Console.Write("\nChoose a demo scenario (1-6): ");
            
            var scenarioChoice = Console.ReadLine();
            
            // Prepare and run the selected scenario
            await device.StartContinuousReadingAsync();
            
            switch (scenarioChoice)
            {
                case "2":
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.GradualIncrease);
                    break;
                case "3":
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.Unstable);
                    break;
                case "4":
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.StepChanges);
                    break;
                case "5": 
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.Overload);
                    break;
                case "6":
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.ZeroStability);
                    break;
                default:
                    await device.SetDemoProgramAsync(DemoWeighingDevice.DemoProgram.Stable);
                    break;
            }
            
            Console.WriteLine("\nRunning demo for 30 seconds. Press any key to stop early.");
            
            // Wait for either 30 seconds or key press
            Task delayTask = Task.Delay(30000);
            Task keyTask = Task.Run(() => { Console.ReadKey(true); });
            await Task.WhenAny(delayTask, keyTask);
            
            // Clean up
            await device.StopContinuousReadingAsync();
            await device.DisconnectAsync();
            device.Dispose();
            
            Console.WriteLine("\nDemo completed.");
        }
    }
    
    /// <summary>
    /// A simulated weighing device that doesn't require actual hardware
    /// Useful for testing and demonstration purposes
    /// </summary>
    public class SimulatedWeighingDevice : WeighingDeviceBase
    {
        private decimal _currentWeight;
        private readonly decimal _minWeight;
        private readonly decimal _maxWeight;
        private readonly WeightUnit _weightUnit;
        private readonly Random _random = new Random();
        private readonly string _deviceName;
        private readonly string _manufacturer;
        private readonly string _model;
        private System.Threading.Timer? _continuousReadingTimer;
        
        public override string DeviceName => _deviceName;
        public override string Manufacturer => _manufacturer;
        public override string Model => _model;
        public override bool SupportsContinuousReading => true;
        
        public SimulatedWeighingDevice(
            string deviceName,
            string manufacturer,
            string model,
            decimal initialWeight = 0.0m,
            WeightUnit weightUnit = WeightUnit.Gram,
            decimal minWeight = 0.0m,
            decimal maxWeight = 5000.0m)
            : base(new ConnectionConfig(ConnectionType.Custom, "simulation"))
        {
            _deviceName = deviceName;
            _manufacturer = manufacturer;
            _model = model;
            _currentWeight = initialWeight;
            _weightUnit = weightUnit;
            _minWeight = minWeight;
            _maxWeight = maxWeight;
        }
        
        public async Task AddWeightAsync(decimal weightToAdd)
        {
            _currentWeight += weightToAdd;
            
            // Ensure weight stays within bounds
            _currentWeight = Math.Max(_minWeight, Math.Min(_maxWeight, _currentWeight));
            
            // Simulate a small delay to mimic real device
            await Task.Delay(100);
            
            // Trigger a reading if anyone is listening
            OnWeightReadingReceived(new WeightReading(_currentWeight, _weightUnit));
        }
        
        protected override Task<bool> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            // For simulation, always succeed with connection
            return Task.FromResult(true);
        }
        
        protected override Task CloseConnectionAsync()
        {
            // Nothing to close in a simulation
            return Task.CompletedTask;
        }
        
        protected override Task<WeightReading> ReadWeightAsync(CancellationToken cancellationToken = default)
        {
            // Add a small amount of noise for realism
            decimal noise = (decimal)((_random.NextDouble() - 0.5) * 0.01);
            var reading = new WeightReading(_currentWeight + noise, _weightUnit);
            return Task.FromResult(reading);
        }
        
        protected override Task StartContinuousReadingInternalAsync(CancellationToken cancellationToken)
        {
            // Fix for the async void lambda warning by using a properly handled synchronization context
            _continuousReadingTimer = new System.Threading.Timer(state => 
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    try 
                    {
                        // Perform the weight reading synchronously
                        var reading = ReadWeightAsync().GetAwaiter().GetResult();
                        OnWeightReadingReceived(reading);
                    }
                    catch (Exception ex)
                    {
                        // Log any exceptions that might occur during the timer callback
                        System.Diagnostics.Debug.WriteLine($"Error in continuous reading timer: {ex.Message}");
                    }
                }
            }, null, 0, 1000); // Send a reading every second
            
            return Task.CompletedTask;
        }
        
        protected override Task StopContinuousReadingInternalAsync()
        {
            _continuousReadingTimer?.Dispose();
            _continuousReadingTimer = null;
            return Task.CompletedTask;
        }
        
        protected override Task ZeroScaleInternalAsync(CancellationToken cancellationToken)
        {
            _currentWeight = 0;
            return Task.CompletedTask;
        }
        
        protected override Task<string> SendRawCommandInternalAsync(string command, CancellationToken cancellationToken)
        {
            // Simulate sending a command by returning a fake response
            return Task.FromResult($"ACK: {command}");
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _continuousReadingTimer?.Dispose();
                _continuousReadingTimer = null;
            }
            
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A demo weighing device that provides various predefined weight patterns
    /// Useful for demonstrations and testing without actual hardware
    /// </summary>
    public class DemoWeighingDevice : WeighingDeviceBase
    {
        private decimal _currentWeight;
        private readonly WeightUnit _weightUnit;
        private readonly Random _random = new();
        private readonly string _deviceName;
        private readonly string _manufacturer;
        private readonly string _model;
        private System.Threading.Timer? _continuousReadingTimer;
        private DemoProgram _currentProgram = DemoProgram.Stable;
        private int _demoStepCounter = 0;
        private bool _isStable = true;
        
        public enum DemoProgram
        {
            Stable,
            GradualIncrease,
            Unstable,
            StepChanges,
            Overload,
            ZeroStability
        }
        
        public override string DeviceName => _deviceName;
        public override string Manufacturer => _manufacturer;
        public override string Model => _model;
        public override bool SupportsContinuousReading => true;
        
        public DemoWeighingDevice(
            string deviceName,
            string manufacturer,
            string model,
            WeightUnit weightUnit = WeightUnit.Gram)
            : base(new ConnectionConfig(ConnectionType.Custom, "demo"))
        {
            _deviceName = deviceName;
            _manufacturer = manufacturer;
            _model = model;
            _weightUnit = weightUnit;
            _currentWeight = 0.0m;
        }
        
        public async Task SetDemoProgramAsync(DemoProgram program)
        {
            _currentProgram = program;
            _demoStepCounter = 0;
            
            // Reset weight for the new program
            _currentWeight = program switch
            {
                DemoProgram.Stable => 5.0m,
                DemoProgram.GradualIncrease => 0.0m,
                DemoProgram.Unstable => 2.5m,
                DemoProgram.StepChanges => 0.0m,
                DemoProgram.Overload => 0.0m,
                DemoProgram.ZeroStability => 0.0m,
                _ => 0.0m
            };
            
            // Small pause to simulate program change
            await Task.Delay(500);
        }
        
        private WeightReading GenerateDemoReading()
        {
            decimal weight = _currentWeight;
            bool isStable = _isStable;
            
            // Update the step counter for time-based patterns
            _demoStepCounter++;
            
            // Generate readings based on the selected demo program
            switch (_currentProgram)
            {
                case DemoProgram.Stable:
                    // Stable weight with tiny random noise
                    weight = 5.0m + (decimal)(_random.NextDouble() - 0.5) * 0.002m;
                    isStable = true;
                    break;
                    
                case DemoProgram.GradualIncrease:
                    // Weight gradually increases over time (30 steps to reach 10kg)
                    if (_demoStepCounter <= 30)
                    {
                        weight = (_demoStepCounter / 3.0m);
                        isStable = _demoStepCounter % 3 != 0; // Occasional instability
                    }
                    break;
                    
                case DemoProgram.Unstable:
                    // Weight fluctuates around a central value with significant noise
                    weight = 2.5m + (decimal)(_random.NextDouble() - 0.5) * 0.3m;
                    isStable = _random.Next(10) >= 4; // 60% chance of stability
                    break;
                    
                case DemoProgram.StepChanges:
                    // Weight changes in distinct steps
                    int step = (_demoStepCounter / 5) % 5;
                    weight = step * 2.0m;
                    isStable = (_demoStepCounter % 5) > 1; // Unstable right after change
                    break;
                    
                case DemoProgram.Overload:
                    // Weight gradually increases until overload
                    if (_demoStepCounter <= 40)
                    {
                        weight = _demoStepCounter * 0.5m;
                        isStable = weight < 15.0m; // Becomes unstable near maximum
                        
                        if (weight > 18.0m)
                        {
                            // Simulate overload condition
                            var reading = new WeightReading(0.0m, _weightUnit, false);
                            reading.Metadata!["Overload"] = true;
                            reading.Metadata["Error"] = "Scale capacity exceeded";
                            return reading;
                        }
                    }
                    break;
                    
                case DemoProgram.ZeroStability:
                    // Demonstrates zero drift and stability
                    if (_demoStepCounter % 10 == 0)
                    {
                        // Return to zero periodically
                        weight = 0.0m;
                        isStable = true;
                    }
                    else
                    {
                        // Small drift around zero
                        weight = (decimal)(_random.NextDouble() - 0.5) * 0.05m;
                        isStable = Math.Abs(weight) < 0.02m;
                    }
                    break;
            }
            
            // Store the current state for the next reading
            _currentWeight = weight;
            _isStable = isStable;
            
            // Create and return the weight reading
            return new WeightReading(weight, _weightUnit, isStable);
        }
        
        protected override Task<bool> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            // For demo, always succeed with connection
            return Task.FromResult(true);
        }
        
        protected override Task CloseConnectionAsync()
        {
            // Nothing to close in a demo
            return Task.CompletedTask;
        }
        
        protected override Task<WeightReading> ReadWeightAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GenerateDemoReading());
        }
        
        protected override Task StartContinuousReadingInternalAsync(CancellationToken cancellationToken)
        {
            _continuousReadingTimer = new System.Threading.Timer(state => 
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    try 
                    {
                        var reading = GenerateDemoReading();
                        OnWeightReadingReceived(reading);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in demo reading timer: {ex.Message}");
                    }
                }
            }, null, 0, 1000); // Send a reading every second
            
            return Task.CompletedTask;
        }
        
        protected override Task StopContinuousReadingInternalAsync()
        {
            _continuousReadingTimer?.Dispose();
            _continuousReadingTimer = null;
            return Task.CompletedTask;
        }
        
        protected override Task ZeroScaleInternalAsync(CancellationToken cancellationToken)
        {
            _currentWeight = 0;
            return Task.CompletedTask;
        }
        
        protected override Task<string> SendRawCommandInternalAsync(string command, CancellationToken cancellationToken)
        {
            // Simulate command responses
            if (command.ToUpper().Contains("STATUS"))
                return Task.FromResult($"STATUS: READY, PROGRAM: {_currentProgram}");
                
            return Task.FromResult($"ACK: {command}");
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _continuousReadingTimer?.Dispose();
                _continuousReadingTimer = null;
            }
            
            base.Dispose(disposing);
        }
    }
}

