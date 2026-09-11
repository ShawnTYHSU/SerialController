using System.Collections.ObjectModel;
using SerialController.Abstractions.Interfaces;
using SerialController.Abstractions.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SerialController.Wpf.Controls;

/// <summary>
/// 包裝單一 <see cref="IModbusSerialDevice"/>，提供畫面需要的連線狀態/Holding Register 預覽/
/// 手動寫入指令。跟 <c>AdamIoController.Wpf.Controls.AdamModuleViewModel</c> 是同一種風格，
/// 但這裡預覽的是通用 Holding Register 清單（<see cref="ModbusSerialDeviceConfig.PollStartAddress"/>/
/// <see cref="ModbusSerialDeviceConfig.PollRegisterCount"/> 決定範圍），不是特定裝置家族的
/// DI/DO——SerialController 不知道消費端接的是什麼型號的 Modbus 從站裝置。
/// </summary>
public sealed partial class ModbusSerialDeviceViewModel : ObservableObject
{
    private readonly IModbusSerialDevice _device;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "尚未連線";

    public ObservableCollection<RegisterViewModel> HoldingRegisters { get; } = new();

    public string Name => _device.Config.Name;

    public string PortName => _device.Config.Connection.PortName;

    public int BaudRate => _device.Config.Connection.BaudRate;

    public ModbusSerialDeviceViewModel(IModbusSerialDevice device)
    {
        _device = device;

        for (int i = 0; i < device.Config.PollRegisterCount; i++)
        {
            HoldingRegisters.Add(new RegisterViewModel(device.Config.PollStartAddress + i));
        }
    }

    /// <summary>連線這台裝置，成功/失敗都會更新 <see cref="IsConnected"/>/<see cref="StatusMessage"/>，
    /// 不拋例外——理由同 <see cref="IModbusSerialDevice.ConnectAsync"/> 介面文件註解。</summary>
    public async Task ConnectAsync(CancellationToken token = default)
    {
        IsConnected = await _device.ConnectAsync(token).ConfigureAwait(true);
        StatusMessage = IsConnected
            ? "已連線"
            : $"連線失敗，請確認序列埠與裝置是否已上電{(_device.LastError is { } err ? $"（{err}）" : string.Empty)}";
    }

    /// <summary>輪詢這台裝置的 Holding Register 預覽範圍，更新到 <see cref="HoldingRegisters"/>。
    /// 讀取失敗（例如連線中途斷線）會把 <see cref="IsConnected"/> 打回 false，理由同
    /// <c>AdamModuleViewModel.RefreshAsync</c>。</summary>
    public async Task RefreshAsync(CancellationToken token = default)
    {
        if (!_device.IsConnected || HoldingRegisters.Count == 0)
        {
            return;
        }

        try
        {
            ushort[] values = await _device.ReadHoldingRegistersAsync(
                (ushort)_device.Config.PollStartAddress, (ushort)_device.Config.PollRegisterCount, token).ConfigureAwait(true);

            for (int i = 0; i < values.Length && i < HoldingRegisters.Count; i++)
            {
                HoldingRegisters[i].Value = values[i];
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"讀取失敗，連線可能已中斷：{ex.Message}";
        }
    }

    /// <summary>手動寫入一個 Holding Register，供畫面上的輸入框/按鈕綁定。寫入失敗只更新
    /// <see cref="StatusMessage"/>，不 revert 畫面上的 <see cref="RegisterViewModel.Value"/>——跟
    /// ADAM DO 按鈕的樂觀更新不同，這裡的值是使用者手動輸入的目標值，不是「目前實際狀態」，
    /// 寫入失敗不代表這個輸入框該恢復成舊值。</summary>
    [RelayCommand]
    private async Task WriteRegister(RegisterViewModel register)
    {
        try
        {
            await _device.WriteSingleRegisterAsync((ushort)register.Address, register.Value).ConfigureAwait(true);
            StatusMessage = $"已寫入暫存器 #{register.Address} = {register.Value}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"寫入暫存器 #{register.Address} 失敗：{ex.Message}";
        }
    }
}
