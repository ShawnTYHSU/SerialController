namespace SerialController.Abstractions.Enums;

/// <summary>
/// 序列 Modbus 的兩種訊框格式。RTU 是業界對序列 Modbus 的標準（二進位、CRC 校驗），
/// ASCII 較少見（文字、LRC 校驗，除錯較容易但吞吐量低）——兩者都支援，由
/// <see cref="Models.ModbusSerialDeviceConfig.Mode"/> 決定 <c>SerialController.Protocols.NModbus</c>
/// 層要建立哪一種 NModbus master。
/// </summary>
public enum ModbusProtocolMode
{
    Rtu,
    Ascii,
}
