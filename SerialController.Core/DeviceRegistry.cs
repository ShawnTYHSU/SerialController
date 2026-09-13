namespace SerialController.Core;

/// <summary>
/// SerialController 的「具名裝置清單」共用基底：先建立（不自動連線）；依名稱查詢，找不到
/// 回傳 null；新增時名稱重複拋 <see cref="ArgumentException"/>；移除前先
/// <see cref="IAsyncDisposable.DisposeAsync"/>；<see cref="ConnectAllAsync"/> 依序連線全部、
/// 個別失敗不中斷；整體 <see cref="DisposeAsync"/> 收尾並清空。
///
/// <see cref="ModbusSerialDeviceRegistry"/>／<see cref="SerialPortConnectionRegistry"/> 都是這個
/// 模式的具體特化，只是元素型別（Modbus 裝置／原始序列連線）不同——2026-09 深化重構前，這兩個
/// 類別逐行複製了同一套邏輯（見 `SerialController` repo 的 `HANDOFF_SUMMARY.md`）。這裡刻意只
/// 收斂內部共用的行為，兩個具體類別各自對外的公開 API 名稱（<c>Devices</c>/<c>Connections</c>、
/// <c>AddDevice</c>/<c>AddConnection</c>…）完全不變，不強迫統一成同一套命名或簽章。
/// </summary>
/// <typeparam name="TDevice">清單管理的裝置/連線型別，須可非同步釋放。</typeparam>
/// <remarks>技術上必須是 <c>public</c>（C# 不允許 public 類別繼承存取範圍更小的基底類別），
/// 但成員全部是 <c>protected</c>，唯二對外可見的只有 <see cref="ConnectAllAsync"/>/
/// <see cref="DisposeAsync"/>——兩者本來就是 <see cref="ModbusSerialDeviceRegistry"/>/
/// <see cref="SerialPortConnectionRegistry"/> 既有的公開方法，不是新增的對外面。</remarks>
public abstract class DeviceRegistry<TDevice> : IAsyncDisposable
    where TDevice : IAsyncDisposable
{
    private readonly Dictionary<string, TDevice> _items = new();
    private readonly Func<TDevice, CancellationToken, Task> _connect;

    /// <param name="connect">依序連線時，對單一裝置實際要呼叫的方法——兩個具體類別的裝置
    /// 介面用不同的方法名稱做同一件事（<c>IModbusSerialDevice.ConnectAsync</c> vs
    /// <c>ISerialPortConnection.OpenAsync</c>），故用委派抽象掉這個差異。</param>
    protected DeviceRegistry(Func<TDevice, CancellationToken, Task> connect) => _connect = connect;

    /// <summary>目前註冊的全部項目，唯讀快照（<see cref="Dictionary{TKey,TValue}.Values"/>
    /// 的存活集合，不是複製一份，呼叫端不應該長期持有這個集合的參照）。</summary>
    protected IReadOnlyCollection<TDevice> Items => _items.Values;

    /// <summary>依名稱查詢單一項目，找不到回傳 null（不拋例外——這個名稱還沒設定是呼叫端
    /// 很容易遇到的正常情況，例如設定檔還沒載入完成）。</summary>
    protected TDevice? TryGet(string name) => _items.GetValueOrDefault(name);

    /// <summary>
    /// 新增一筆項目：先檢查名稱是否重複（重複則拋例外，不呼叫 <paramref name="createDevice"/>，
    /// 避免白白建立一個馬上就要丟棄的裝置物件），確認可以新增才真正建立並存入。
    /// </summary>
    /// <param name="duplicateNameParamName">名稱重複時 <see cref="ArgumentException"/> 要標註的
    /// 參數名稱——兩個具體類別的公開方法簽章不同（<c>AddDevice(config)</c> 標 <c>config</c>，
    /// <c>AddConnection(name, config)</c> 標 <c>name</c>），由呼叫端自己決定。</param>
    protected TDevice Add(string name, Func<TDevice> createDevice, string duplicateNameParamName)
    {
        if (_items.ContainsKey(name))
        {
            throw new ArgumentException($"裝置名稱「{name}」已經存在，不能重複新增，請先移除舊的或改用其他名稱。", duplicateNameParamName);
        }

        TDevice device = createDevice();
        _items[name] = device;
        return device;
    }

    /// <summary>移除一筆項目——先 Dispose 才從清單移除，避免變成孤兒連線。名稱不存在時安全地
    /// 什麼都不做，不拋例外。</summary>
    protected async Task RemoveAsync(string name)
    {
        if (_items.Remove(name, out TDevice? device))
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>依序連線目前註冊的所有項目——刻意依序（不是平行），避免同時開多條序列埠造成
    /// 的資源競爭；個別項目連線失敗不會中斷整個流程，呼叫端事後可以自行檢查各項目的連線狀態。</summary>
    public async Task ConnectAllAsync(CancellationToken token = default)
    {
        foreach (TDevice device in _items.Values)
        {
            await _connect(device, token).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (TDevice device in _items.Values)
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }

        _items.Clear();
    }
}
