# CH32X033 USB Mini Display

Windows 透過 USB Full-Speed 將 80×160 RGB565 畫面分塊傳給 CH32X033F8P6，再由 MCU 即時寫入 ST7735S。MCU 不保存完整 framebuffer。

目前進度：**Phase 2 core — ST7735 streaming、command handler、五色 Host producer 已完成；WCH USBFS platform adapter 待工具鏈與實機整合**。

## 已確認硬體

- MCU：CH32X033F8P6（CH32X035 系列，48 MHz、62 KiB Flash、20 KiB SRAM）
- USB：MCU 內建 USB 2.0 Full-Speed device
- LCD：ST7735S，80×160，SPI
- LCD pins：PA5/SCLK、PA7/MOSI、PA6/DC、PA0/RESET、PC3/CS
- 傳輸：Vendor-Specific USB Bulk / WinUSB，RGB565 block streaming

詳見 [硬體記錄](docs/hardware.md)及[封包協定](docs/protocol.md)。

## 目錄

```text
docs/                   硬體與協定文件
host/                   .NET 8 host protocol library/self-test
firmware/include/       可供 CH32X035 firmware 使用的 protocol API
firmware/src/           無平台依賴的 packet/CRC 實作
firmware/tests/         PC 上可執行的 C self-test
tools/protocol-vectors/ 跨語言測試向量
```

## Build/test

### Firmware protocol（目前可執行）

需要 CMake 3.16+ 與 C11 compiler：

```powershell
cmake -S firmware -B firmware/build
cmake --build firmware/build
ctest --test-dir firmware/build --output-on-failure
```

這只建置 platform-independent parser，不需要 WCH SDK 或開發板。

### Windows host protocol

需要 **.NET 8 SDK**（只有 .NET Desktop Runtime 不足）：

```powershell
dotnet run --project host/MiniDisplay.Protocol.SelfTest
dotnet run --project host/MiniDisplay.ColorTest -- tools/protocol-vectors/color-test.packets
```

已使用 .NET SDK 8.0.424 編譯並通過 protocol self-test；ColorTest 會產生紅、綠、藍、白、黑五幀的 length-prefixed packet stream。

## 後續 Phase

1. Phase 2：接入 WCH CH32X035 EVT USB Device/Bulk 範例、ST7735 driver、純色 Host 測試。
2. Phase 3：WPF、PNG/JPG/BMP、Fit/Fill/Stretch、Preview。
3. Phase 4：GDI desktop capture、monitor/rectangle。
4. Phase 5：MP4/AVI/GIF decoder adapter。
5. Phase 6：Desktop Duplication、雙緩衝與 profiling。

## 燒錄

Phase 2 後使用 WCH-Link（SDI）或 WCHISPTool 支援的 USB ISP 流程；目前尚無可燒錄 firmware image。

## Known issues

- 商品接線圖解析度有限，Phase 2 前應以萬用表或 PCB 實物再次確認 PA6/DC、PC3/CS。
- LCD panel 的 column/row offset 與可用 SPI clock 尚待實機確認。
- WinUSB VID/PID 與 Microsoft OS descriptor 尚未定案。
