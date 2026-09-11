using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Protocols.NModbus;

/// <summary>對應 <see cref="ModbusSerialDevice"/> 的工廠。</summary>
public sealed class ModbusSerialDeviceFactory : IModbusSerialDeviceFactory
{
    public IModbusSerialDevice Create(ModbusSerialDeviceConfig config) => new ModbusSerialDevice(config);
}
