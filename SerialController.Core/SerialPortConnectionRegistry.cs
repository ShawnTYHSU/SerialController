using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;

namespace SerialController.Core;

/// <summary>
/// 管理「目前系統裡設定了哪些一般（非 Modbus）序列裝置」的清單——跟
/// <see cref="ModbusSerialDeviceRegistry"/> 是完全同一種模式，只是管理的是純位元組收發的
/// <see cref="ISerialPortConnection"/>，不含任何 Modbus 語意。存在的理由：有些現場裝置
/// （例如簡單的文字查詢/回應協定的感測器）不是 Modbus 從站，但消費端 App 仍然希望用同一套
/// 「具名裝置清單、依名稱查詢、依序連線」的管理方式，不需要為了這種裝置另外設計一套完全
/// 不同的管理機制。
///
/// 依賴 <see cref="ISerialPortConnectionFactory"/>（不是具體的
/// <c>SerialController.Protocols.NModbus.SerialPortConnection</c> 實作），遵守本系列專案的
/// 依賴方向規則——Core 不能依賴 Protocols.&lt;Impl&gt;。
///
/// 「具名清單、依序連線、移除先 Dispose」這套共用行為本身在 <see cref="DeviceRegistry{TDevice}"/>
/// 基底類別（見 <see cref="ModbusSerialDeviceRegistry"/> 的另一個特化），這裡只放原始序列連線
/// 專屬的公開 API 形狀跟命名。
/// </summary>
public sealed class SerialPortConnectionRegistry : DeviceRegistry<ISerialPortConnection>
{
    private readonly ISerialPortConnectionFactory _factory;

    public SerialPortConnectionRegistry(ISerialPortConnectionFactory factory)
        : base((connection, token) => connection.OpenAsync(token))
    {
        _factory = factory;
    }

    /// <summary>目前註冊的全部連線，唯讀快照（<see cref="Dictionary{TKey,TValue}.Values"/>
    /// 的存活集合，不是複製一份，呼叫端不應該長期持有這個集合的參照）。</summary>
    public IReadOnlyCollection<ISerialPortConnection> Connections => Items;

    /// <summary>依名稱查詢單一連線，找不到回傳 null（不拋例外，理由同
    /// <see cref="ModbusSerialDeviceRegistry.TryGetDevice"/>）。</summary>
    public ISerialPortConnection? TryGetConnection(string name) => TryGet(name);

    /// <summary>
    /// 新增一筆裝置設定並建立（但不連線）對應的物件。<paramref name="name"/> 必須是這個
    /// Registry 裡目前唯一的名稱，重複會拋 <see cref="ArgumentException"/>，理由同
    /// <see cref="ModbusSerialDeviceRegistry.AddDevice"/>。
    /// </summary>
    public ISerialPortConnection AddConnection(string name, SerialPortConfig config)
        => Add(name, () => _factory.Create(config), nameof(name));

    /// <summary>移除一筆裝置——連線會先關閉才從清單移除，理由同
    /// <see cref="ModbusSerialDeviceRegistry.RemoveDeviceAsync"/>。</summary>
    public Task RemoveConnectionAsync(string name) => RemoveAsync(name);
}
