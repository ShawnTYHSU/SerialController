using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Protocols.NModbus;

/// <summary>對應 <see cref="SerialPortConnection"/> 的工廠。</summary>
public sealed class SerialPortConnectionFactory : ISerialPortConnectionFactory
{
    public ISerialPortConnection Create(SerialPortConfig config) => new SerialPortConnection(config);
}
