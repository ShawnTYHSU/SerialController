using SerialController.Abstractions.Models;

namespace SerialController.Abstractions.Interfaces;

/// <summary>
/// 依一筆 <see cref="ModbusSerialDeviceConfig"/> 建立對應的 <see cref="IModbusSerialDevice"/> 實例。
/// 拆出這個工廠介面是為了讓 <c>SerialController.Core.ModbusSerialDeviceRegistry</c>（Core 層，
/// 不能依賴 Protocols）可以在不知道底層是 NModbus 還是別的 Modbus 函式庫的情況下建立裝置
/// 物件，符合本系列專案一貫的依賴方向規則。
/// </summary>
public interface IModbusSerialDeviceFactory
{
    /// <summary>建立（但不會自動連線）一個對應設定的裝置實例，呼叫端要自己呼叫
    /// <see cref="IModbusSerialDevice.ConnectAsync"/>。</summary>
    IModbusSerialDevice Create(ModbusSerialDeviceConfig config);
}
