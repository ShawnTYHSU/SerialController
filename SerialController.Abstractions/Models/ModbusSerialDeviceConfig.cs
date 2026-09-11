using SerialController.Abstractions.Enums;

namespace SerialController.Abstractions.Models;

/// <summary>
/// 描述「一台透過序列埠掛載的 Modbus 從站裝置」的設定——這批資料是可擴充清單的核心，
/// 這裡只定義「一台裝置」長什麼樣子，「現場有幾台裝置」那份清單存在呼叫端（消費端
/// App），跟 <c>AdamIoController.Abstractions.Models.AdamModuleConfig</c> 是同一種角色分工。
/// </summary>
/// <param name="Name">這台裝置的邏輯名稱（使用者自訂），同一個系統裡必須唯一，
/// <c>SerialController.Core.ModbusSerialDeviceRegistry</c> 用這個當 key。</param>
/// <param name="Connection">底層序列埠傳輸設定。</param>
/// <param name="SlaveId">Modbus 從站位址，預設 1（單一裝置點對點連線的常見預設值；
/// RS-485 多台裝置共用一條線時，每台裝置的 SlaveId 必須不同）。</param>
/// <param name="Mode">RTU 或 ASCII 訊框格式，預設 <see cref="ModbusProtocolMode.Rtu"/>。</param>
/// <param name="PollStartAddress">畫面/輪詢預設讀取的保持暫存器起始位址（0-based），
/// 純粹給 <c>SerialController.Wpf.Controls</c> 的預覽面板用，不影響
/// <see cref="Interfaces.IModbusSerialDevice"/> 本身的讀寫方法（那些方法呼叫端可以自訂
/// 任意位址範圍）。</param>
/// <param name="PollRegisterCount">畫面/輪詢預設讀取的保持暫存器數量，理由同
/// <see cref="PollStartAddress"/>。</param>
public readonly record struct ModbusSerialDeviceConfig(
    string Name, SerialPortConfig Connection, byte SlaveId = 1,
    ModbusProtocolMode Mode = ModbusProtocolMode.Rtu,
    int PollStartAddress = 0, int PollRegisterCount = 8)
{
    /// <summary>建立一筆新設定的預設值，方便 UI 上「新增裝置」時有個合理的起始值可以編輯。</summary>
    public static ModbusSerialDeviceConfig CreateDefault(string name) =>
        new(name, SerialPortConfig.CreateDefault());
}
