# 03_GLOSSARY_AND_MAPPINGS.md

本文件定義 Python 原始碼至 C# .NET 8 重構過程中的術語與結構映射規範。

## 1. 術語對照表 (Terminology Mapping)
| Python 概念 | C# 對應術語 | 說明 |
| :--- | :--- | :--- |
| Module | Service / Manager | 核心業務邏輯封裝單位 |
| `dict` (Data) | `record struct` / `record` | 靜態強型別 DTO |
| `Optional[T]` | `T?` | Nullable Reference/Value Types |
| `List[Any]` | `ImmutableArray<T>` | 建議優先使用不可變集合 |
| `def method()` | `async Task` 或 `void` | 依 I/O 邊界決定是否使用 async |
| `try-except` | `try-catch` | 應封裝至 Service 內的錯誤處理 |

## 2. 結構映射範例 (Structural Mapping)
*   **動態物件轉換**：
    *   Python `dict` (含多種型別) -> 定義 `record` 或 `record struct`。
    *   Python `__getattr__` 寫法 -> 轉為明確的 C# 介面定義或 Property。
*   **例外處理對照**：
    *   `ValueError` -> `ArgumentException`
    *   `IOError` -> `IOException`
    *   自定義邏輯錯誤 -> `ApplicationException` 或自定義 `Exception` 類別。

## 3. 型別安全守則
*   嚴禁在公開 API 中使用 `object` 或 `dynamic`。
*   所有參數傳遞必須具備具體型別。
*   若遇到 Python 中的 `None`，在 C# 中必須明確檢查並轉換為預設值或 Nullable。
