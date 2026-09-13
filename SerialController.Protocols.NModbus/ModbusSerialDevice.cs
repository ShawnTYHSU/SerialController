using System.IO.Ports;
using global::NModbus;
using SerialController.Abstractions.Enums;
using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Protocols.NModbus;

/// <summary>
/// <see cref="IModbusSerialDevice"/> 的 Modbus RTU/ASCII 實作，用 NModbus 套件（
/// <see href="https://github.com/NModbus/NModbus"/>）在序列埠上跑 Modbus master。
///
/// ✅ 2026-09 已透過反射實際列舉 <c>NModbus.dll</c>（3.0.83）的公開型別確認過 API 形狀，
/// 不是憑空假設：<c>ModbusFactory</c>/<c>IModbusFactory</c> 沒有直接吃
/// <c>System.IO.Ports.SerialPort</c> 或現成序列埠 adapter 的多載——套件只內建 TCP/UDP/Socket
/// 用的 adapter（<c>TcpClientAdapter</c>/<c>UdpClientAdapter</c>/<c>SocketAdapter</c>），序列埠要
/// 自己包一層 <c>IStreamResource</c>，見 <see cref="SerialPortStreamResource"/>。實際建立
/// master 用的是 <c>NModbus</c> 命名空間下的擴充方法 <c>ModbusFactory.CreateRtuMaster(IStreamResource)</c>/
/// <c>CreateAsciiMaster(IStreamResource)</c>（定義在 <c>NModbus.FactoryExtensions</c>），不是
/// <c>ModbusFactory</c> 類別本身的方法。
///
/// **序列 Modbus 跟 TCP Modbus 一樣是「一問一答」協定，同一條連線不能同時有多個請求在途
/// 中**——這裡沿用 <c>ModbusAdamIoModule</c> 已經修過的教訓，每一個真正呼叫 <c>_master</c>
/// 的方法都用 <see cref="_modbusLock"/>（<see cref="SemaphoreSlim"/>）包住，不是等實際遇到
/// 併發衝突（悄悄讀到/寫到錯的資料，不拋例外）才回頭補。RS-485 多台裝置共用一條線時，
/// 這個鎖只保護「同一個 <see cref="ModbusSerialDevice"/> 實例」內部的併發——不同實例各自
/// 開自己的 <see cref="SerialPort"/>，見類別頂端「已知限制」說明。
///
/// ⚠️ **已知限制**：這個實作假設每台裝置獨占一個實體序列埠（<see cref="SerialPort"/> 只能被
/// 一個 handle 開啟）。如果現場是 RS-485 多台裝置共用同一條實體線、只靠不同
/// <see cref="ModbusSerialDeviceConfig.SlaveId"/> 區分，目前每個 <see cref="ModbusSerialDevice"/>
/// 各自開自己的 <see cref="SerialPort"/> 會互搶同一個埠而失敗——這種情境需要「多個裝置共用
/// 同一個已開啟連線」的重新設計，目前沒有已知需求，故意不在這一版處理。
/// </summary>
public sealed class ModbusSerialDevice : IModbusSerialDevice
{
    /// <summary>序列化所有對 <see cref="_master"/> 的存取，理由見類別文件註解。</summary>
    private readonly SemaphoreSlim _modbusLock = new(1, 1);

    private readonly ModbusSerialDeviceConfig _config;
    private SerialPort? _serialPort;
    private IModbusSerialMaster? _master;

    public ModbusSerialDevice(ModbusSerialDeviceConfig config) => _config = config;

    public ModbusSerialDeviceConfig Config => _config;

    public bool IsConnected => _serialPort?.IsOpen ?? false;

    public string? LastError { get; private set; }

    public Task<bool> ConnectAsync(CancellationToken token = default)
    {
        if (!SerialPortFactory.TryOpen(_config.Connection, out SerialPort? serialPort, out string? error))
        {
            LastError = error;
            return Task.FromResult(false);
        }

        try
        {
            var adapter = new SerialPortStreamResource(serialPort);
            var factory = new ModbusFactory();
            IModbusSerialMaster master = _config.Mode == ModbusProtocolMode.Ascii
                ? factory.CreateAsciiMaster(adapter)
                : factory.CreateRtuMaster(adapter);

            _serialPort = serialPort;
            _master = master;
            LastError = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            // NModbus master 建立失敗（理論上很少見，SerialPort 本身已經開成功）——這裡直接
            // 關閉/釋放剛開好的 serialPort，不透過 Disconnect()：2026-09 深化重構前的舊版在這裡
            // 呼叫 Disconnect()，但 _serialPort 欄位那時候還沒被賦值（只有連線完全成功後才賦值），
            // 等於這個分支完全沒有真的釋放剛開啟的序列埠——這裡一併修正。
            LastError = ex.Message;
            serialPort.Close();
            serialPort.Dispose();
            return Task.FromResult(false);
        }
    }

    public void Disconnect()
    {
        _master = null;
        _serialPort?.Close();
        _serialPort?.Dispose();
        _serialPort = null;
    }

    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await master.ReadCoilsAsync(_config.SlaveId, startAddress, count).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await master.ReadInputsAsync(_config.SlaveId, startAddress, count).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await master.ReadHoldingRegistersAsync(_config.SlaveId, startAddress, count).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await master.ReadInputRegistersAsync(_config.SlaveId, startAddress, count).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await master.WriteSingleCoilAsync(_config.SlaveId, address, value).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await master.WriteSingleRegisterAsync(_config.SlaveId, address, value).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await master.WriteMultipleCoilsAsync(_config.SlaveId, startAddress, values).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken token = default)
    {
        IModbusSerialMaster master = EnsureConnected();
        await _modbusLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await master.WriteMultipleRegistersAsync(_config.SlaveId, startAddress, values).ConfigureAwait(false);
        }
        finally
        {
            _modbusLock.Release();
        }
    }

    private IModbusSerialMaster EnsureConnected()
    {
        if (_master is null)
        {
            throw new InvalidOperationException($"序列 Modbus 裝置「{_config.Name}」尚未連線，請先呼叫 ConnectAsync 並確認回傳 true。");
        }

        return _master;
    }

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        _modbusLock.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
