namespace SerialController.Abstractions.Enums;

/// <summary>
/// 序列埠同位檢查方式，獨立於 <c>System.IO.Ports.Parity</c> 定義——Abstractions 不依賴任何
/// 特定傳輸/協定套件的型別，換底層實作（例如之後要換掉 NModbus 或改用別的序列埠函式庫）
/// 不需要動這一層。<c>SerialController.Protocols.NModbus</c> 的實作負責轉換成
/// <c>System.IO.Ports.Parity</c>。
/// </summary>
public enum SerialParity
{
    None,
    Odd,
    Even,
    Mark,
    Space,
}
