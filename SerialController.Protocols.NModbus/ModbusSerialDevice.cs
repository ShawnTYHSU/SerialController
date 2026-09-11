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
        if (string.IsNullOrWhiteSpace(_config.Connection.PortName))
        {
            // 明確擋在真正嘗試開啟之前，理由同 SerialPortConnection.OpenAsync。
            LastError = "尚未設定序列埠名稱";
            return Task.FromResult(false);
        }

        try
        {
            var serialPort = new SerialPort(
                _config.Connection.PortName, _config.Connection.BaudRate,
                ToParity(_config.Connection.Parity), _config.Connection.DataBits,
                ToStopBits(_config.Connection.StopBits))
            {
                ReadTimeout = _config.Connection.ReadTimeoutMs,
                WriteTimeout = _config.Connection.WriteTimeoutMs,
            };
            serialPort.Open();

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
            // 連線失敗是現場部署情境下的常態（埠名打錯、埠被佔用、裝置未接上），不拋例外，
            // 理由同 IAdamIoModule.ConnectAsync 文件註解。
            LastError = ex.Message;
            Disconnect();
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

    private static Parity ToParity(SerialParity parity) => parity switch
    {
        SerialParity.None => Parity.None,
        SerialParity.Odd => Parity.Odd,
        SerialParity.Even => Parity.Even,
        SerialParity.Mark => Parity.Mark,
        SerialParity.Space => Parity.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, "未知的同位檢查設定"),
    };

    private static StopBits ToStopBits(SerialStopBits stopBits) => stopBits switch
    {
        SerialStopBits.One => StopBits.One,
        SerialStopBits.OnePointFive => StopBits.OnePointFive,
        SerialStopBits.Two => StopBits.Two,
        _ => throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, "未知的停止位元設定"),
    };

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        _modbusLock.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
