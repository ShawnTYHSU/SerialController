using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Core;

/// <summary>
/// 管理「目前系統裡設定了哪些序列 Modbus 裝置」的清單——模組數量不寫死，透過
/// <see cref="AddDevice"/>/<see cref="RemoveDeviceAsync"/> 動態增減，消費端 App 的
/// SettingPage 可以直接把使用者在畫面上編輯的裝置清單反映到這裡。
///
/// 依賴 <see cref="IModbusSerialDeviceFactory"/>（不是具體的 NModbus 實作）建立裝置物件，
/// 遵守本系列專案的依賴方向規則——Core 不能依賴 Protocols.&lt;Impl&gt;，具體要用哪個協定
/// 實作由呼叫端組裝時決定要注入哪個工廠。跟
/// <c>AdamIoController.Core.AdamIoModuleRegistry</c> 是同一種模式，只是管理的是序列 Modbus
/// 裝置而不是 ADAM TCP 模組。
///
/// 「具名清單、依序連線、移除先 Dispose」這套共用行為本身在 <see cref="DeviceRegistry{TDevice}"/>
/// 基底類別（見 <see cref="SerialPortConnectionRegistry"/> 的另一個特化），這裡只放
/// Modbus 裝置專屬的公開 API 形狀跟命名。
/// </summary>
public sealed class ModbusSerialDeviceRegistry : DeviceRegistry<IModbusSerialDevice>
{
    private readonly IModbusSerialDeviceFactory _factory;

    public ModbusSerialDeviceRegistry(IModbusSerialDeviceFactory factory)
        : base((device, token) => device.ConnectAsync(token))
    {
        _factory = factory;
    }

    /// <summary>目前註冊的全部裝置，唯讀快照（<see cref="Dictionary{TKey,TValue}.Values"/>
    /// 的存活集合，不是複製一份，呼叫端不應該長期持有這個集合的參照）。</summary>
    public IReadOnlyCollection<IModbusSerialDevice> Devices => Items;

    /// <summary>依名稱查詢單一裝置，找不到回傳 null（不拋例外——「這個名稱的裝置還沒
    /// 設定」是呼叫端很容易遇到的正常情況，例如設定檔還沒載入完成）。</summary>
    public IModbusSerialDevice? TryGetDevice(string name) => TryGet(name);

    /// <summary>
    /// 新增一台裝置設定並建立（但不連線）對應的物件。<see cref="ModbusSerialDeviceConfig.Name"/>
    /// 必須是這個 Registry 裡目前唯一的名稱，重複會拋 <see cref="ArgumentException"/>——這是
    /// 設定階段的邏輯錯誤，不應該被靜默忽略或覆蓋掉既有的裝置。
    /// </summary>
    public IModbusSerialDevice AddDevice(ModbusSerialDeviceConfig config)
        => Add(config.Name, () => _factory.Create(config), nameof(config));

    /// <summary>移除一台裝置——連線會先關閉（<see cref="IAsyncDisposable.DisposeAsync"/>）
    /// 才從清單移除，避免連線物件變成孤兒。名稱不存在時安全地什麼都不做，不拋例外。</summary>
    public Task RemoveDeviceAsync(string name) => RemoveAsync(name);
}
