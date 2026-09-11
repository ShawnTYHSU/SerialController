using System.IO.Ports;
using SerialController.Abstractions.Enums;
using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Protocols.NModbus;

/// <summary>
/// <see cref="ISerialPortConnection"/> 的 <c>System.IO.Ports.SerialPort</c> 實作——單純的
/// 原始位元組收發包裝，不含任何 Modbus 語意（那是 <see cref="ModbusSerialDevice"/> 的職責）。
///
/// ⚠️ 這個類別跟 <see cref="ModbusSerialDevice"/> 是兩個獨立的能力，刻意都放在
/// <c>SerialController.Protocols.NModbus</c> 專案裡——這個專案目前是唯一的序列協定實作，
/// 還沒有出現「跨多個 Protocols.&lt;Impl&gt; 共用、但不屬於任何協定契約」的情況，所以沒有
/// 另外拆一個 <c>SerialController.Common</c> 專案，這是刻意的精簡，不是遺漏。如果之後真的
/// 加入第二個非 Modbus 的序列協定實作，且需要重用這個類別，才需要考慮拆出 Common 層。
/// </summary>
public sealed class SerialPortConnection : ISerialPortConnection
{
    private readonly SerialPortConfig _config;
    private SerialPort? _port;

    public SerialPortConnection(SerialPortConfig config) => _config = config;

    public SerialPortConfig Config => _config;

    public bool IsOpen => _port?.IsOpen ?? false;

    public string? LastError { get; private set; }

    public Task<bool> OpenAsync(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(_config.PortName))
        {
            // 明確擋在真正嘗試開啟之前，理由同 ModbusSerialDevice.ConnectAsync——不擋的話
            // SerialPort.Open() 對空字串丟出的例外不容易一眼看懂是「沒設定埠名」。
            LastError = "尚未設定序列埠名稱";
            return Task.FromResult(false);
        }

        try
        {
            var port = new SerialPort(_config.PortName, _config.BaudRate, ToParity(_config.Parity), _config.DataBits, ToStopBits(_config.StopBits))
            {
                ReadTimeout = _config.ReadTimeoutMs,
                WriteTimeout = _config.WriteTimeoutMs,
            };
            port.Open();
            _port = port;
            LastError = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            // 開啟失敗不拋例外（埠名打錯、埠被其他程式佔用、裝置未接上都是現場常態），
            // 理由同 IAdamIoModule.ConnectAsync 文件註解。
            LastError = ex.Message;
            Close();
            return Task.FromResult(false);
        }
    }

    public void Close()
    {
        _port?.Close();
        _port?.Dispose();
        _port = null;
    }

    public async Task WriteAsync(byte[] data, CancellationToken token = default)
    {
        SerialPort port = EnsureOpen();
        await port.BaseStream.WriteAsync(data, token).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadAsync(int maxBytes, CancellationToken token = default)
    {
        SerialPort port = EnsureOpen();
        byte[] buffer = new byte[maxBytes];
        int read = await port.BaseStream.ReadAsync(buffer.AsMemory(0, maxBytes), token).ConfigureAwait(false);
        return buffer[..read];
    }

    private SerialPort EnsureOpen()
    {
        if (_port is null)
        {
            throw new InvalidOperationException($"序列埠「{_config.PortName}」尚未開啟，請先呼叫 OpenAsync 並確認回傳 true。");
        }

        return _port;
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

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
