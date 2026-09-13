using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using SerialController.Abstractions.Enums;
using SerialController.Abstractions.Models;

namespace SerialController.Protocols.NModbus;

/// <summary>
/// <see cref="ModbusSerialDevice"/> 跟 <see cref="SerialPortConnection"/> 共用的「建構＋開啟
/// <see cref="SerialPort"/>」邏輯——2026-09 深化重構前，這段（含 <see cref="ToParity"/>/
/// <see cref="ToStopBits"/> 兩個列舉轉換）在兩個類別裡逐字重複，只差
/// <see cref="ModbusSerialDevice"/> 多包一層 <c>ModbusSerialDeviceConfig.Connection</c>。
/// 抽到這裡後，兩邊呼叫端各自接上不同的用途（原始位元組收發／NModbus master 建立），
/// 「開埠」這一段完全共用的邏輯只有一份定義。
/// </summary>
internal static class SerialPortFactory
{
    /// <summary>
    /// 依 <paramref name="config"/> 建構並嘗試開啟一個 <see cref="SerialPort"/>。失敗（埠名未設定、
    /// 埠名打錯、埠已被其他程式佔用、裝置未上電等）一律回傳 false + <paramref name="error"/>，
    /// 不拋例外——理由同 <c>IModbusSerialDevice.ConnectAsync</c> 文件註解：這是現場部署情境下的
    /// 常態，不是例外狀況。
    /// </summary>
    internal static bool TryOpen(SerialPortConfig config, [NotNullWhen(true)] out SerialPort? port, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(config.PortName))
        {
            // 明確擋在真正嘗試開啟之前，不擋的話 SerialPort.Open() 對空字串丟出的例外不容易
            // 一眼看懂是「沒設定埠名」。
            port = null;
            error = "尚未設定序列埠名稱";
            return false;
        }

        try
        {
            var candidate = new SerialPort(config.PortName, config.BaudRate, ToParity(config.Parity), config.DataBits, ToStopBits(config.StopBits))
            {
                ReadTimeout = config.ReadTimeoutMs,
                WriteTimeout = config.WriteTimeoutMs,
            };
            candidate.Open();
            port = candidate;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            port = null;
            error = ex.Message;
            return false;
        }
    }

    internal static Parity ToParity(SerialParity parity) => parity switch
    {
        SerialParity.None => Parity.None,
        SerialParity.Odd => Parity.Odd,
        SerialParity.Even => Parity.Even,
        SerialParity.Mark => Parity.Mark,
        SerialParity.Space => Parity.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, "未知的同位檢查設定"),
    };

    internal static StopBits ToStopBits(SerialStopBits stopBits) => stopBits switch
    {
        SerialStopBits.One => StopBits.One,
        SerialStopBits.OnePointFive => StopBits.OnePointFive,
        SerialStopBits.Two => StopBits.Two,
        _ => throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, "未知的停止位元設定"),
    };
}
