using System.IO.Ports;
using global::NModbus.IO;

namespace SerialController.Protocols.NModbus;

/// <summary>
/// 把 <see cref="System.IO.Ports.SerialPort"/> 包裝成 NModbus 的 <see cref="IStreamResource"/>，
/// 讓 <c>ModbusFactory.CreateRtuMaster</c>/<c>CreateAsciiMaster</c> 可以在序列埠上運作。
///
/// ⚠️ NModbus 3.0.83 只內建 TCP/UDP/Socket 用的 <c>NModbus.IO.TcpClientAdapter</c>/
/// <c>UdpClientAdapter</c>/<c>SocketAdapter</c>，**沒有**內建序列埠版本的 adapter（跟
/// <c>AdamIoController.Protocols.Modbus</c> 用到的 TCP 那組 API 不是同一組）——這個類別是
/// 透過反射實際列舉 <c>NModbus.dll</c> 的公開型別後才確認需要自己補上，不是憑空假設。
/// </summary>
internal sealed class SerialPortStreamResource(SerialPort serialPort) : IStreamResource
{
    public int InfiniteTimeout => SerialPort.InfiniteTimeout;

    public int ReadTimeout
    {
        get => serialPort.ReadTimeout;
        set => serialPort.ReadTimeout = value;
    }

    public int WriteTimeout
    {
        get => serialPort.WriteTimeout;
        set => serialPort.WriteTimeout = value;
    }

    public void DiscardInBuffer() => serialPort.DiscardInBuffer();

    public int Read(byte[] buffer, int offset, int count) => serialPort.Read(buffer, offset, count);

    public void Write(byte[] buffer, int offset, int count) => serialPort.Write(buffer, offset, count);

    public void Dispose()
    {
        // 刻意不在這裡關閉/釋放 serialPort——這個 adapter 不擁有 SerialPort 的生命週期，
        // 那是 ModbusSerialDevice.Disconnect 的職責（同一個 SerialPort 實例的開關本來就該
        // 由建立它的那一層決定，不是被包裝它的 adapter 意外關掉）。
    }
}
