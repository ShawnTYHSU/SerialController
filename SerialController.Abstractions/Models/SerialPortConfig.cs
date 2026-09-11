using SerialController.Abstractions.Enums;

namespace SerialController.Abstractions.Models;

/// <summary>
/// 描述「一個實體序列埠連線」的設定——純粹傳輸層參數，不含 Modbus slave ID/協定模式
/// （那些屬於 <see cref="ModbusSerialDeviceConfig"/>），讓這份設定可以被任何序列協定
/// 重用，不是只給 Modbus 用。
/// </summary>
/// <param name="PortName">作業系統的序列埠名稱，例如 <c>COM3</c>。</param>
/// <param name="BaudRate">鮑率，預設 9600（RS-232/RS-485 常見預設值）。</param>
/// <param name="DataBits">資料位元數，預設 8。</param>
/// <param name="StopBits">停止位元，預設 <see cref="SerialStopBits.One"/>。</param>
/// <param name="Parity">同位檢查，預設 <see cref="SerialParity.None"/>。</param>
/// <param name="ReadTimeoutMs">讀取逾時（毫秒），預設 1000。</param>
/// <param name="WriteTimeoutMs">寫入逾時（毫秒），預設 1000。</param>
public readonly record struct SerialPortConfig(
    string PortName, int BaudRate = 9600, int DataBits = 8,
    SerialStopBits StopBits = SerialStopBits.One, SerialParity Parity = SerialParity.None,
    int ReadTimeoutMs = 1000, int WriteTimeoutMs = 1000)
{
    /// <summary>建立一筆新設定的預設值，方便 UI 上「新增連線」時有個合理的起始值可以編輯。</summary>
    public static SerialPortConfig CreateDefault() => new(PortName: string.Empty);
}
