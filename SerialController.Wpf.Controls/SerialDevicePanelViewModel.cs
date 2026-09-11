using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using SerialController.Abstractions.Interfaces;
using SerialController.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SerialController.Wpf.Controls;

/// <summary>
/// 整個序列 Modbus 裝置監控/測試面板的 ViewModel，管理多台裝置（透過
/// <see cref="ModbusSerialDeviceRegistry"/>，裝置數量本身可變）。跟
/// <c>AdamIoController.Wpf.Controls.AdamIoPanelViewModel</c> 是同一種模式：集中一個
/// <see cref="DispatcherTimer"/> 依序輪詢所有裝置，不是每個
/// <see cref="ModbusSerialDeviceViewModel"/> 各自開一個計時器。
///
/// 跟 ADAM 版本的差異：這裡故意不做 ConnectionSummaryState→主題色 Brush 轉換器那一套——
/// 那個機制依賴消費端（EIBG_Assemble）在 SwallowBase_DLL 的 Color.xaml 裡定義的
/// <c>Brush-AdamIoPanelView-*</c> 資源鍵值，這個專案還沒有對應的主題資源，硬做只會在找不到
/// 資源時退回灰色，沒有實際意義。<see cref="StatusMessage"/> 這一版只是純文字，之後真的要做
/// 主題色分級，再比照 ADAM 版本補上對應的 Brush 資源鍵值跟 Converter。
/// </summary>
public sealed partial class SerialDevicePanelViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ModbusSerialDeviceRegistry _registry;
    private readonly DispatcherTimer _pollTimer;

    public ObservableCollection<ModbusSerialDeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>輪詢間隔，預設 500ms——沿用本專案系列既有的 IO 面板輪詢節奏（見
    /// <c>AdamIoController.Wpf.Controls.AdamIoPanelViewModel</c> 文件註解），不是另外挑一個
    /// 不同的數字。</summary>
    public SerialDevicePanelViewModel(ModbusSerialDeviceRegistry registry)
    {
        _registry = registry;
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += async (_, _) => await PollAllAsync().ConfigureAwait(true);
    }

    /// <summary>把 <see cref="Devices"/> 這個畫面顯示用的清單，跟目前
    /// <see cref="ModbusSerialDeviceRegistry"/> 裡實際註冊的裝置同步，理由同
    /// <c>AdamIoPanelViewModel.RefreshModuleList</c>。</summary>
    public void RefreshDeviceList()
    {
        Devices.Clear();
        foreach (IModbusSerialDevice device in _registry.Devices)
        {
            Devices.Add(new ModbusSerialDeviceViewModel(device));
        }
    }

    /// <summary>依序連線 <see cref="Devices"/> 目前列出的所有裝置，全部連線完成後開始常駐輪詢。
    /// 呼叫前應該先呼叫過 <see cref="RefreshDeviceList"/>。</summary>
    [RelayCommand]
    private async Task ConnectAll() => await ConnectAllAsync().ConfigureAwait(true);

    /// <summary>跟 <see cref="ConnectAllCommand"/> 做同一件事，但是一個普通的公開方法，供非 UI
    /// 觸發的呼叫端使用（例如 App 啟動時由 Composition Root 直接呼叫），理由同
    /// <c>AdamIoPanelViewModel.ConnectAllAsync</c>。</summary>
    public async Task ConnectAllAsync()
    {
        StatusMessage = "連線中...";
        foreach (ModbusSerialDeviceViewModel deviceVm in Devices)
        {
            await deviceVm.ConnectAsync().ConfigureAwait(true);
        }

        int connectedCount = Devices.Count(d => d.IsConnected);
        StatusMessage = Devices.Count switch
        {
            0 => "沒有設定任何序列 Modbus 裝置",
            _ when connectedCount == Devices.Count => $"連線成功：{connectedCount}/{Devices.Count} 台裝置已連線",
            _ when connectedCount == 0 => $"連線失敗：{connectedCount}/{Devices.Count} 台裝置已連線",
            _ => $"部分連線失敗：{connectedCount}/{Devices.Count} 台裝置已連線",
        };

        _pollTimer.Start();
    }

    private async Task PollAllAsync()
    {
        foreach (ModbusSerialDeviceViewModel deviceVm in Devices)
        {
            await deviceVm.RefreshAsync().ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _pollTimer.Stop();
        await _registry.DisposeAsync().ConfigureAwait(false);
    }
}
